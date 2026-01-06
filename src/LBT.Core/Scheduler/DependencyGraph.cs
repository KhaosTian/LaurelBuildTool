namespace LBT.Scheduler;

/// <summary>
/// Dependency graph (DAG) builder.
/// </summary>
public class DependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _dependencies = new();
    private readonly Dictionary<string, Project> _projectMap = new();

    /// <summary>
    /// Adds a project.
    /// </summary>
    /// <param name="project">The project to add.</param>
    public void AddProject(Project project)
    {
        _projectMap[project.Name] = project;
        _dependencies[project.Name] = new HashSet<string>();
    }

    /// <summary>
    /// Adds a dependency relationship.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="dependsOn">The project it depends on.</param>
    public void AddDependency(string projectName, string dependsOn)
    {
        if (!_dependencies.ContainsKey(projectName))
        {
            _dependencies[projectName] = new HashSet<string>();
        }
        _dependencies[projectName].Add(dependsOn);
    }

    /// <summary>
    /// Detects circular dependencies.
    /// </summary>
    /// <param name="cyclePath">The detected cycle path, if any.</param>
    /// <returns>True if a cycle is detected; otherwise, false.</returns>
    public bool HasCycle(out List<string>? cyclePath)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var project in _dependencies.Keys)
        {
            if (DetectCycle(project, visited, recursionStack, path))
            {
                cyclePath = new List<string>(path);
                return true;
            }
        }

        cyclePath = null;
        return false;
    }

    private bool DetectCycle(
        string project,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path)
    {
        if (recursionStack.Contains(project))
        {
            path.Add(project);
            return true;
        }

        if (visited.Contains(project))
        {
            return false;
        }

        visited.Add(project);
        recursionStack.Add(project);
        path.Add(project);

        if (_dependencies.TryGetValue(project, out var deps))
        {
            foreach (var dep in deps)
            {
                if (DetectCycle(dep, visited, recursionStack, path))
                {
                    return true;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(project);
        return false;
    }

    /// <summary>
    /// Topological sort - returns the build order.
    /// </summary>
    /// <returns>The list of projects in build order.</returns>
    public List<Project> GetBuildOrder()
    {
        var inDegree = new Dictionary<string, int>();
        var result = new List<Project>();

        // Initialize in-degree
        foreach (var project in _dependencies.Keys)
        {
            inDegree[project] = 0;
        }

        // Calculate in-degree
        foreach (var (project, deps) in _dependencies)
        {
            foreach (var dep in deps)
            {
                if (inDegree.ContainsKey(dep))
                {
                    inDegree[project]++;
                }
            }
        }

        // Find nodes with in-degree of 0
        var queue = new Queue<string>();
        foreach (var (project, degree) in inDegree)
        {
            if (degree == 0)
            {
                queue.Enqueue(project);
            }
        }

        // BFS topological sort
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (_projectMap.TryGetValue(current, out var project))
            {
                result.Add(project);
            }

            // Decrease the in-degree of other projects that depend on this one
            foreach (var (proj, deps) in _dependencies)
            {
                if (deps.Contains(current))
                {
                    inDegree[proj]--;
                    if (inDegree[proj] == 0)
                    {
                        queue.Enqueue(proj);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a dependency graph from the BuildSystem.
    /// </summary>
    /// <returns>The constructed dependency graph.</returns>
    public static DependencyGraph Build()
    {
        var graph = new DependencyGraph();

        foreach (var project in BuildSystem.Projects.Values)
        {
            graph.AddProject(project);
        }

        foreach (var project in BuildSystem.Projects.Values)
        {
            foreach (var libName in project.LinkedLibraries)
            {
                if (BuildSystem.Projects.ContainsKey(libName))
                {
                    graph.AddDependency(project.Name, libName);
                }
            }
        }

        return graph;
    }
}
