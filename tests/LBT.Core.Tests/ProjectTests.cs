using Xunit;

namespace LBT.Tests;

public class ProjectTests
{
    public ProjectTests()
    {
        // Reset state before each test
        BuildSystem.Reset();
    }

    [Fact]
    public void Create_ShouldRegisterProject()
    {
        // Arrange & Act
        var project = Project.Create("TestApp");

        // Assert
        Assert.NotNull(project);
        Assert.Equal("TestApp", project.Name);
        Assert.Equal(ProjectType.Executable, project.Type);
        Assert.Contains("TestApp", BuildSystem.Projects.Keys);
    }

    [Fact]
    public void CreateStaticLibrary_ShouldSetCorrectType()
    {
        // Arrange & Act
        var lib = Project.CreateStaticLibrary("MyLib");

        // Assert
        Assert.Equal(ProjectType.StaticLibrary, lib.Type);
    }

    [Fact]
    public void CreateSharedLibrary_ShouldSetCorrectType()
    {
        // Arrange & Act
        var lib = Project.CreateSharedLibrary("MyDll");

        // Assert
        Assert.Equal(ProjectType.SharedLibrary, lib.Type);
    }

    [Fact]
    public void AddDefine_ShouldAddDefines()
    {
        // Arrange
        var project = Project.Create("TestApp");

        // Act
        project.AddDefine("DEBUG", "VERSION=1");

        // Assert
        Assert.Contains("DEBUG", project.Defines);
        Assert.Contains("VERSION=1", project.Defines);
    }

    [Fact]
    public void Link_ShouldAddLibraries()
    {
        // Arrange
        var project = Project.Create("TestApp");

        // Act
        project.Link("MathLib", "NetworkLib");

        // Assert
        Assert.Contains("MathLib", project.LinkedLibraries);
        Assert.Contains("NetworkLib", project.LinkedLibraries);
    }

    [Fact]
    public void GetOutputFileName_Executable_Windows()
    {
        // Arrange
        var project = Project.Create("TestApp");

        // Act
        var fileName = project.GetOutputFileName(BuildConfiguration.Release);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("TestApp.exe", fileName);
        }
        else
        {
            Assert.Equal("TestApp", fileName);
        }
    }

    [Fact]
    public void GetOutputFileName_Debug_ShouldHaveSuffix()
    {
        // Arrange
        var project = Project.Create("TestApp");

        // Act
        var fileName = project.GetOutputFileName(BuildConfiguration.Debug);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("TestApp_d.exe", fileName);
        }
        else
        {
            Assert.Equal("TestApp_d", fileName);
        }
    }

    [Fact]
    public void FluentApi_ShouldChain()
    {
        // Arrange & Act
        var project = Project.Create("TestApp")
            .AddDefine("DEBUG")
            .AddCompilerFlags("-Wall")
            .Link("SomeLib");

        // Assert
        Assert.Single(project.Defines);
        Assert.Single(project.CompilerFlags);
        Assert.Single(project.LinkedLibraries);
    }
}
