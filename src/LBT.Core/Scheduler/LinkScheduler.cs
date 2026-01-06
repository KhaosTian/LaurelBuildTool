using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using LBT.Toolchain;

namespace LBT.Scheduler;

/// <summary>
/// Link scheduler.
/// </summary>
public class LinkScheduler
{
    private readonly Toolchain.Toolchain _toolchain;
    private readonly BuildConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkScheduler"/> class.
    /// </summary>
    /// <param name="toolchain">The C++ toolchain to use.</param>
    /// <param name="configuration">The build configuration.</param>
    public LinkScheduler(Toolchain.Toolchain toolchain, BuildConfiguration configuration)
    {
        _toolchain = toolchain;
        _configuration = configuration;
    }

    /// <summary>
    /// Links the project.
    /// </summary>
    /// <param name="project">The project to link.</param>
    /// <returns>True if linking succeeded; otherwise, false.</returns>
    public async Task<bool> LinkProjectAsync(Project project)
    {
        // All projects output to a unified build directory under the project root
        var rootDir = BuildSystem.RootDirectory;
        var outputDir = Path.Combine(rootDir, "build", _configuration.ToString().ToLower());
        var objDir = Path.Combine(outputDir, "obj");

        // Collect all object files
        var objectFiles = new List<string>();
        foreach (var source in project.SourceFiles.Where(f => f.IsCppSource || f.IsCSource))
        {
            var objFile = source.GetObjectFilePath(outputDir);
            if (File.Exists(objFile))
            {
                objectFiles.Add(objFile);
            }
        }

        if (objectFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No object files to link[/]");
            return false;
        }

        // Output file path
        var outputFile = Path.Combine(outputDir, project.GetOutputFileName(_configuration));

        // Collect dependency libraries
        var libraries = new List<string>();
        var libraryDirs = new List<string>();

        // Process Dependencies (other targets)
        foreach (var depName in project.DistinctDependencies())
        {
            var depProject = BuildSystem.GetProject(depName);
            if (depProject != null)
            {
                // Internal project dependency - all use the same build directory
                libraryDirs.Add(outputDir);

                if (depProject.Type == ProjectType.StaticLibrary)
                {
                    var libFile = Path.Combine(outputDir, depProject.GetOutputFileName(_configuration));
                    if (File.Exists(libFile))
                    {
                        objectFiles.Add(libFile); // Add static library directly to linking
                    }
                }
                else if (depProject.Type == ProjectType.SharedLibrary)
                {
                    // For shared libraries, add the import library (.lib file on Windows)
                    var libFile = Path.Combine(outputDir, depProject.GetImportLibraryFileName(_configuration));
                    if (File.Exists(libFile))
                    {
                        objectFiles.Add(libFile);
                    }
                    else
                    {
                        libraries.Add(depName);
                    }
                }
                // Interface targets don't need linking
            }
            else
            {
                // Dependency not found in BuildSystem, treat as external library
                libraries.Add(depName);
            }
        }

        var options = new LinkOptions
        {
            ObjectFiles = objectFiles,
            OutputFile = outputFile,
            OutputType = project.Type,
            Configuration = _configuration,
            Libraries = libraries,
            LibraryDirs = libraryDirs,
            ExtraFlags = project.LinkerFlags.ToList()
        };

        var command = _toolchain.GetLinkCommand(options);

        AnsiConsole.MarkupLine($"[blue]Linking: {project.Name}[/]");

        try
        {
            var startTime = DateTime.UtcNow;

            var cliCommand = Cli.Wrap(command.Executable)
                .WithArguments(command.Arguments)
                .WithWorkingDirectory(project.Directory)
                .WithValidation(CommandResultValidation.None);

            // If the toolchain requires specific environment variables (e.g., MSVC), apply them
            if (_toolchain.BuildEnvironment != null)
            {
                cliCommand = cliCommand.WithEnvironmentVariables(_toolchain.BuildEnvironment);
            }

            var result = await cliCommand.ExecuteBufferedAsync();

            var duration = DateTime.UtcNow - startTime;

            if (result.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[red]Linking failed[/]");
                if (!string.IsNullOrEmpty(result.StandardError))
                {
                    AnsiConsole.MarkupLine($"[red]{EscapeMarkup(result.StandardError)}[/]");
                }
                if (!string.IsNullOrEmpty(result.StandardOutput))
                {
                    AnsiConsole.MarkupLine($"[red]{EscapeMarkup(result.StandardOutput)}[/]");
                }
                return false;
            }

            var fileSize = new FileInfo(outputFile).Length;
            var sizeStr = FormatFileSize(fileSize);

            AnsiConsole.MarkupLine(
                $"[green]Completed: {Path.GetFileName(outputFile)} ({sizeStr}, {duration.TotalSeconds:F1}s)[/]"
            );

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Link error: {ex.Message}[/]");
            return false;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}
