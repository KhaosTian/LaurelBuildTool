using Xunit;

namespace LBT.Tests;

public class BuildSystemTests
{
    public BuildSystemTests()
    {
        BuildSystem.Reset();
    }

    [Fact]
    public void RegisterProject_ShouldAddToProjects()
    {
        // Arrange & Act
        var project = Project.Create("App1");
        var lib = Project.CreateStaticLibrary("Lib1");

        // Assert
        Assert.Equal(2, BuildSystem.Projects.Count);
        Assert.Same(project, BuildSystem.GetProject("App1"));
        Assert.Same(lib, BuildSystem.GetProject("Lib1"));
    }

    [Fact]
    public void GetProject_NotExists_ShouldReturnNull()
    {
        // Act
        var result = BuildSystem.GetProject("NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Reset_ShouldClearAll()
    {
        // Arrange
        Project.Create("App1");
        Project.Create("App2");

        // Act
        BuildSystem.Reset();

        // Assert
        Assert.Empty(BuildSystem.Projects);
    }

    [Fact]
    public void RootDirectory_ShouldBeAbsolute()
    {
        // Arrange
        var testPath = "some/relative/path";

        // Act
        BuildSystem.RootDirectory = testPath;

        // Assert
        Assert.True(Path.IsPathRooted(BuildSystem.RootDirectory));
    }
}
