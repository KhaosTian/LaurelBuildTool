using Xunit;

using LBT.Scheduler;

namespace LBT.Tests;

public class DependencyGraphTests
{
    public DependencyGraphTests()
    {
        BuildSystem.Reset();
    }

    [Fact]
    public void Build_ShouldCreateGraphFromProjects()
    {
        // Arrange
        Project.Create("App");
        Project.CreateStaticLibrary("Lib");

        // Act
        var graph = DependencyGraph.Build();
        var order = graph.GetBuildOrder();

        // Assert
        Assert.Equal(2, order.Count);
    }

    [Fact]
    public void GetBuildOrder_ShouldRespectDependencies()
    {
        // Arrange
        var lib = Project.CreateStaticLibrary("Lib");
        var app = Project.Create("App").Link("Lib");

        // Act
        var graph = DependencyGraph.Build();
        var order = graph.GetBuildOrder();

        // Assert
        var libIndex = order.FindIndex(p => p.Name == "Lib");
        var appIndex = order.FindIndex(p => p.Name == "App");
        Assert.True(libIndex < appIndex, "Lib should be built before App");
    }

    [Fact]
    public void HasCycle_NoCycle_ShouldReturnFalse()
    {
        // Arrange
        Project.CreateStaticLibrary("Lib");
        Project.Create("App").Link("Lib");

        // Act
        var graph = DependencyGraph.Build();
        var hasCycle = graph.HasCycle(out var path);

        // Assert
        Assert.False(hasCycle);
        Assert.Null(path);
    }

    [Fact]
    public void AddDependency_Cycle_ShouldDetect()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.AddProject(Project.Create("A"));
        graph.AddProject(Project.Create("B"));
        graph.AddProject(Project.Create("C"));

        graph.AddDependency("A", "B");
        graph.AddDependency("B", "C");
        graph.AddDependency("C", "A"); // Creates cycle

        BuildSystem.Reset();

        // Act
        var hasCycle = graph.HasCycle(out var path);

        // Assert
        Assert.True(hasCycle);
        Assert.NotNull(path);
    }
}
