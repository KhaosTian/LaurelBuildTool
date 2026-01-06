using System.CommandLine;
using Spectre.Console;
using LBT;

namespace LBT.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("LBT - Modern C++ Build System");

        // build command
        var buildCommand = new Command("build", "Build the project");
        var configOption = new Option<string>(
            new[] { "-c", "--config" },
            () => "debug",
            "Build configuration (debug/release)"
        );
        buildCommand.AddOption(configOption);
        buildCommand.SetHandler(async (config) =>
        {
            await RunBuildAsync("build", config);
        }, configOption);

        // clean command
        var cleanCommand = new Command("clean", "Clean build artifacts");
        cleanCommand.SetHandler(async () =>
        {
            await RunBuildAsync("clean", "debug");
        });

        // init command
        var initCommand = new Command("init", "Initialize a new project");
        initCommand.SetHandler(() =>
        {
            InitProject();
        });

        // run command
        var runCommand = new Command("run", "Build and run the project");
        runCommand.AddOption(configOption);
        runCommand.SetHandler(async (config) =>
        {
            await RunBuildAsync("run", config);
        }, configOption);

        rootCommand.AddCommand(buildCommand);
        rootCommand.AddCommand(cleanCommand);
        rootCommand.AddCommand(initCommand);
        rootCommand.AddCommand(runCommand);

        // Execute build by default
        rootCommand.SetHandler(async () =>
        {
            await RunBuildAsync("build", "debug");
        });

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> RunBuildAsync(string command, string config)
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            AnsiConsole.MarkupLine("[red]Error: Cannot find build.cs file[/]");
            return 1;
        }

        // Set the project root directory
        BuildSystem.RootDirectory = projectRoot;

        // Generate shadow project for IDE IntelliSense (silent)
        try
        {
            ShadowProjectGenerator.Generate(projectRoot);
        }
        catch
        {
            // Ignore errors, shadow project generation is optional
        }

        // Execute build using the embedded BuildEngine
        var engine = new BuildEngine();
        return await engine.ExecuteAsync(new[] { command, "-c", config });
    }

    static void InitProject()
    {
        var currentDir = Environment.CurrentDirectory;
        var buildFile = Path.Combine(currentDir, "build.cs");

        if (File.Exists(buildFile))
        {
            AnsiConsole.MarkupLine("[yellow]build.cs already exists[/]");
            return;
        }

        var template = """
            using LBT;

            // Create a project
            var app = Project.Create("MyApp");

            // Add source files
            app.AddFiles("src/*.cpp");

            // Add include directories
            app.AddIncludeDir("include");

            // The script only defines the project; lbt will automatically execute the build
            """;

        File.WriteAllText(buildFile, template);
        AnsiConsole.MarkupLine("[green]Created build.cs[/]");

        // Generate shadow project for IDE IntelliSense
        AnsiConsole.MarkupLine("[blue]Generating shadow project for IDE...[/]");
        ShadowProjectGenerator.Generate(currentDir);
        AnsiConsole.MarkupLine("[green]Created .lbt/LBT.Shadow.csproj (for IDE IntelliSense)[/]");

        // Create example directory structure
        Directory.CreateDirectory(Path.Combine(currentDir, "src"));
        Directory.CreateDirectory(Path.Combine(currentDir, "include"));

        var mainCpp = Path.Combine(currentDir, "src", "main.cpp");
        if (!File.Exists(mainCpp))
        {
            File.WriteAllText(mainCpp, """
                #include <iostream>

                int main() {
                    std::cout << "Hello, LBT!" << std::endl;
                    return 0;
                }
                """);
            AnsiConsole.MarkupLine("[green]Created src/main.cpp[/]");
        }
    }

    static string? FindProjectRoot()
    {
        var current = Environment.CurrentDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "build.cs")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }
}
