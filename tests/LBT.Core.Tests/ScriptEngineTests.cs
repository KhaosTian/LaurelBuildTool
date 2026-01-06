using Xunit;
using System.IO;

namespace LBT.Tests;

public class ScriptEngineTests : IDisposable
{
    private readonly string _testDir;

    public ScriptEngineTests()
    {
        // Reset state before each test
        BuildSystem.Reset();

        // Create temporary directory for test scripts
        _testDir = Path.Combine(Path.GetTempPath(), "LBTTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task Execute_ShouldLoadBuildScript()
    {
        // Arrange
        var buildCs = Path.Combine(_testDir, "build.cs");
        File.WriteAllText(buildCs, """
            using LBT;

            SetName("TestProject");
            SetVersion("1.0.0");
            SetLanguages("c++17");

            Target("test")
                .AddFiles("src/*.cpp");
            """);

        var engine = new ScriptEngine();

        // Act
        await engine.LoadScriptAsync(buildCs);

        // Assert
        Assert.Equal("TestProject", BuildSystem.ProjectName);
        Assert.Equal("1.0.0", BuildSystem.Version);
        Assert.Single(BuildSystem.Projects);
        Assert.NotNull(BuildSystem.GetProject("test"));
    }

    [Fact]
    public async Task Include_ShouldLoadSubModuleScript()
    {
        // Arrange - Create main build.cs
        var buildCs = Path.Combine(_testDir, "build.cs");
        File.WriteAllText(buildCs, """
            using LBT;

            SetName("MainProject");
            Include("submodule");
            """);

        // Arrange - Create submodule build.cs
        var subDir = Path.Combine(_testDir, "submodule");
        Directory.CreateDirectory(subDir);
        var subBuildCs = Path.Combine(subDir, "build.cs");
        File.WriteAllText(subBuildCs, """
            using LBT;

            Target("sublib")
                .SetKind("static")
                .AddFiles("lib/*.cpp");
            """);

        var engine = new ScriptEngine();

        // Act
        BuildSystem.RootDirectory = _testDir;

        // Load main script (which calls Include("submodule"))
        await engine.LoadScriptAsync(buildCs);

        // Manually load included scripts (simulating BuildEngine behavior)
        foreach (var includePath in BuildSystem.GetIncludedPaths())
        {
            await engine.LoadScriptAsync(includePath);
        }

        // Assert
        Assert.NotNull(BuildSystem.GetProject("sublib"));
        Assert.Equal(ProjectType.StaticLibrary, BuildSystem.GetProject("sublib")!.Type);
    }

    [Fact]
    public async Task Execute_MultipleTargets_ShouldRegisterAll()
    {
        // Arrange
        var buildCs = Path.Combine(_testDir, "build.cs");
        File.WriteAllText(buildCs, """
            using LBT;

            Target("app1").AddFiles("app1/*.cpp");
            Target("app2").AddFiles("app2/*.cpp");
            Target("lib").SetKind("static").AddFiles("lib/*.cpp");
            """);

        var engine = new ScriptEngine();

        // Act
        await engine.LoadScriptAsync(buildCs);

        // Assert
        Assert.Equal(3, BuildSystem.Projects.Count);
        Assert.NotNull(BuildSystem.GetProject("app1"));
        Assert.NotNull(BuildSystem.GetProject("app2"));
        Assert.NotNull(BuildSystem.GetProject("lib"));
    }

    [Fact]
    public async Task Execute_WithGlobalDefines_ShouldApplyToAllProjects()
    {
        // Arrange
        var buildCs = Path.Combine(_testDir, "build.cs");
        File.WriteAllText(buildCs, """
            using LBT;

            AddDefines("GLOBAL_DEFINE", "VERSION=1.0");

            Target("app1").AddFiles("app1/*.cpp");
            Target("app2").AddFiles("app2/*.cpp");
            """);

        var engine = new ScriptEngine();

        // Act
        await engine.LoadScriptAsync(buildCs);

        // Assert
        Assert.Contains("GLOBAL_DEFINE", BuildSystem.GlobalDefines);
        Assert.Contains("VERSION=1.0", BuildSystem.GlobalDefines);
    }

    [Fact]
    public async Task Execute_WithCppStandard_ShouldSetStandard()
    {
        // Arrange
        var buildCs = Path.Combine(_testDir, "build.cs");
        File.WriteAllText(buildCs, """
            using LBT;

            SetLanguages("c++20");
            Target("app").AddFiles("app/*.cpp");
            """);

        var engine = new ScriptEngine();

        // Act
        await engine.LoadScriptAsync(buildCs);

        // Assert
        Assert.Equal(CppStandard.Cpp20, BuildSystem.CppStandard);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        BuildSystem.Reset();
    }
}
