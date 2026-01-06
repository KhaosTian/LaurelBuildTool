using System.Threading.Channels;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using LBT.Cache;
using LBT.Toolchain;

namespace LBT.Scheduler;

/// <summary>
/// Compile task.
/// </summary>
public class CompileTask
{
    /// <summary>
    /// Gets or initializes the source file to compile.
    /// </summary>
    public required FileItem SourceFile { get; init; }

    /// <summary>
    /// Gets or initializes the output object file path.
    /// </summary>
    public required string ObjectFile { get; init; }

    /// <summary>
    /// Gets or initializes the compilation options.
    /// </summary>
    public required CompileOptions Options { get; init; }
}

/// <summary>
/// Compile result.
/// </summary>
public class CompileResult
{
    /// <summary>
    /// Gets or initializes the compile task.
    /// </summary>
    public required CompileTask Task { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the compilation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or initializes the standard output.
    /// </summary>
    public string Output { get; init; } = "";

    /// <summary>
    /// Gets or initializes the error output.
    /// </summary>
    public string Error { get; init; } = "";

    /// <summary>
    /// Gets or initializes the compilation duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or initializes the list of header file dependencies.
    /// </summary>
    public List<string> HeaderDependencies { get; init; } = new();
}

/// <summary>
/// Parallel compilation scheduler.
/// </summary>
public class CompileScheduler
{
    private readonly Toolchain.Toolchain _toolchain;
    private readonly BuildCacheManager _cache;
    private readonly int _maxParallelism;
    private readonly BuildConfiguration _configuration;

    private int _completedCount;
    private int _totalCount;
    private int _failedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompileScheduler"/> class.
    /// </summary>
    /// <param name="toolchain">The C++ toolchain to use.</param>
    /// <param name="cache">The build cache manager.</param>
    /// <param name="configuration">The build configuration.</param>
    /// <param name="maxParallelism">The maximum number of parallel compilations (defaults to processor count).</param>
    public CompileScheduler(
        Toolchain.Toolchain toolchain,
        BuildCacheManager cache,
        BuildConfiguration configuration,
        int? maxParallelism = null)
    {
        _toolchain = toolchain;
        _cache = cache;
        _configuration = configuration;
        _maxParallelism = maxParallelism ?? Environment.ProcessorCount;
    }

    /// <summary>
    /// Compiles the project.
    /// </summary>
    /// <param name="project">The project to compile.</param>
    /// <returns>True if compilation succeeded; otherwise, false.</returns>
    public async Task<bool> CompileProjectAsync(Project project)
    {
        var sourceFiles = project.SourceFiles
            .Where(f => f.IsCppSource || f.IsCSource)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No source files found[/]");
            return true;
        }

        // All projects output to a unified build directory under the project root
        var rootDir = BuildSystem.RootDirectory;
        var outputDir = Path.Combine(rootDir, "build", _configuration.ToString().ToLower());
        var objDir = Path.Combine(outputDir, "obj");
        Directory.CreateDirectory(objDir);

        // Create compile tasks
        var tasks = new List<CompileTask>();

        foreach (var source in sourceFiles)
        {
            var objectFile = source.GetObjectFilePath(outputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(objectFile)!);

            var options = new CompileOptions
            {
                SourceFile = source.FullPath,
                OutputFile = objectFile,
                Configuration = _configuration,
                IncludeDirs = project.IncludeDirs.ToList(),
                Defines = project.Defines.ToList(),
                ExtraFlags = project.CompilerFlags.ToList(),
                GenerateDependencies = true,
                DependencyFile = Path.ChangeExtension(objectFile, ".d")
            };

            // Add exported header directories from dependency projects
            foreach (var depName in project.DistinctDependencies())
            {
                var depProject = BuildSystem.GetProject(depName);
                if (depProject != null)
                {
                    options.IncludeDirs.AddRange(depProject.ExportIncludeDirs);
                }
            }

            // Check if recompilation is needed
            var compilerArgs = string.Join(" ", _toolchain.GetCompileCommand(options).Arguments);
            var needsRebuild = await _cache.NeedsRebuildAsync(
                source.FullPath,
                objectFile,
                compilerArgs,
                _toolchain.Name
            );

            if (needsRebuild)
            {
                tasks.Add(new CompileTask
                {
                    SourceFile = source,
                    ObjectFile = objectFile,
                    Options = options
                });
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Skipped: {source.RelativePath}[/]");
            }
        }

        if (tasks.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]All files are up to date[/]");
            return true;
        }

        _totalCount = tasks.Count;
        _completedCount = 0;
        _failedCount = 0;

        AnsiConsole.MarkupLine($"[blue]Compiling {tasks.Count} files (parallelism: {_maxParallelism})[/]");

        // Use Channel for task distribution
        var taskChannel = Channel.CreateBounded<CompileTask>(_maxParallelism * 2);
        var resultChannel = Channel.CreateUnbounded<CompileResult>();

        // Start worker threads
        var workers = Enumerable.Range(0, _maxParallelism)
            .Select(_ => ProcessTasksAsync(taskChannel.Reader, resultChannel.Writer))
            .ToArray();

        // Dispatch tasks
        var dispatcher = Task.Run(async () =>
        {
            foreach (var task in tasks)
            {
                await taskChannel.Writer.WriteAsync(task);
            }
            taskChannel.Writer.Complete();
        });

        // Collect results
        var results = new List<CompileResult>();
        var collector = Task.Run(async () =>
        {
            await foreach (var result in resultChannel.Reader.ReadAllAsync())
            {
                results.Add(result);
                ReportProgress(result);

                if (result.Success)
                {
                    // Update cache
                    var compilerArgs = string.Join(" ", _toolchain.GetCompileCommand(result.Task.Options).Arguments);
                    await _cache.RecordCompilationAsync(
                        result.Task.SourceFile.FullPath,
                        result.Task.ObjectFile,
                        compilerArgs,
                        _toolchain.Name,
                        result.HeaderDependencies
                    );
                }
            }
        });

        // Wait for all work to complete
        await dispatcher;
        await Task.WhenAll(workers);
        resultChannel.Writer.Complete();
        await collector;

        // Output summary
        AnsiConsole.WriteLine();
        if (_failedCount > 0)
        {
            AnsiConsole.MarkupLine($"[red]Compilation failed: {_failedCount}/{_totalCount}[/]");
            return false;
        }

        AnsiConsole.MarkupLine($"[green]Compilation successful: {_completedCount}/{_totalCount}[/]");
        return true;
    }

    private async Task ProcessTasksAsync(
        ChannelReader<CompileTask> reader,
        ChannelWriter<CompileResult> writer)
    {
        await foreach (var task in reader.ReadAllAsync())
        {
            var result = await CompileFileAsync(task);
            await writer.WriteAsync(result);
        }
    }

    private async Task<CompileResult> CompileFileAsync(CompileTask task)
    {
        var command = _toolchain.GetCompileCommand(task.Options);
        var startTime = DateTime.UtcNow;

        try
        {
            var cliCommand = Cli.Wrap(command.Executable)
                .WithArguments(command.Arguments)
                .WithWorkingDirectory(task.SourceFile.Project.Directory)
                .WithValidation(CommandResultValidation.None);

            // If the toolchain requires specific environment variables (e.g., MSVC), apply them
            if (_toolchain.BuildEnvironment != null)
            {
                cliCommand = cliCommand.WithEnvironmentVariables(_toolchain.BuildEnvironment);
            }

            var result = await cliCommand.ExecuteBufferedAsync();

            var duration = DateTime.UtcNow - startTime;
            // MSVC returns 0 on success, but consider it failed if the file was not generated
            var success = result.ExitCode == 0 && File.Exists(task.Options.OutputFile);

            // Parse header file dependencies
            var headers = ParseHeaderDependencies(
                result.StandardOutput + result.StandardError,
                task.Options.DependencyFile
            );

            return new CompileResult
            {
                Task = task,
                Success = success,
                Output = result.StandardOutput,
                Error = result.StandardError,
                Duration = duration,
                HeaderDependencies = headers
            };
        }
        catch (Exception ex)
        {
            return new CompileResult
            {
                Task = task,
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private List<string> ParseHeaderDependencies(string output, string? depFile)
    {
        var headers = new List<string>();

        // Try parsing from .d file (GCC/Clang)
        if (depFile != null && File.Exists(depFile))
        {
            var content = File.ReadAllText(depFile);
            // Format: target: dep1 dep2 dep3 \
            //              dep4 dep5
            var deps = content
                .Replace("\\\n", " ")
                .Replace("\\\r\n", " ")
                .Split(':')
                .LastOrDefault()?
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (deps != null)
            {
                headers.AddRange(deps.Where(d => d.EndsWith(".h") || d.EndsWith(".hpp")));
            }
        }

        // Parse from MSVC /showIncludes output
        if (_toolchain.Type == ToolchainType.MSVC)
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                // Format: Note: including file: C:\path\to\header.h
                if (line.Contains("Note: including file:"))
                {
                    var path = line.Split("Note: including file:").LastOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        headers.Add(path);
                    }
                }
            }
        }

        return headers.Distinct().ToList();
    }

    private void ReportProgress(CompileResult result)
    {
        Interlocked.Increment(ref _completedCount);

        if (result.Success)
        {
            var time = result.Duration.TotalSeconds.ToString("F1");
            AnsiConsole.MarkupLine(
                $"[green][[{_completedCount}/{_totalCount}]][/] {result.Task.SourceFile.RelativePath} ({time}s)"
            );
        }
        else
        {
            Interlocked.Increment(ref _failedCount);
            AnsiConsole.MarkupLine(
                $"[red][[{_completedCount}/{_totalCount}]] Failed: {result.Task.SourceFile.RelativePath}[/]"
            );
            // Output command line for debugging
            var cmd = _toolchain.GetCompileCommand(result.Task.Options);
            AnsiConsole.MarkupLine($"[dim]Command: {EscapeMarkup(cmd.ToString())}[/]");
            if (!string.IsNullOrEmpty(result.Error))
            {
                AnsiConsole.MarkupLine($"[red]{EscapeMarkup(result.Error)}[/]");
            }
            if (!string.IsNullOrEmpty(result.Output))
            {
                AnsiConsole.MarkupLine($"[yellow]{EscapeMarkup(result.Output)}[/]");
            }
        }
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}
