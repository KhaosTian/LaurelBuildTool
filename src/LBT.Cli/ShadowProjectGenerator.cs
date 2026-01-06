namespace LBT.Cli;

/// <summary>
/// Shadow project generator.
/// </summary>
public static class ShadowProjectGenerator
{
    /// <summary>
    /// Generates a shadow project in the specified directory.
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    public static void Generate(string projectRoot)
    {
        var lbtDir = Path.Combine(projectRoot, ".lbt");
        Directory.CreateDirectory(lbtDir);

        // Generate Shadow project (for IDE IntelliSense only)
        GenerateShadowProject(projectRoot, lbtDir);

        // Generate .gitignore
        var gitignore = Path.Combine(lbtDir, ".gitignore");
        File.WriteAllText(gitignore, "*\n");
    }

    private static void GenerateShadowProject(string projectRoot, string lbtDir)
    {
        // Try to find LBT.Core.csproj for development (in LBT repo)
        string? coreProjectPath = null;
        try
        {
            var coreProjectAbsolutePath = FindLBTCoreProject(projectRoot);
            coreProjectPath = GetRelativePath(lbtDir, coreProjectAbsolutePath);
        }
        catch (FileNotFoundException)
        {
            // Published version: no csproj available
        }

        // Find all build.cs files in the project
        var buildCsFiles = Directory.GetFiles(projectRoot, "build.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".lbt"))
            .ToList();

        // Generate a separate shadow project for each build.cs file to avoid top-level statements conflict
        foreach (var buildCs in buildCsFiles)
        {
            var targetName = ExtractTargetName(buildCs);
            var shadowCsprojName = $"LBT.Shadow.{targetName}.csproj";
            var shadowCsprojPath = Path.Combine(lbtDir, shadowCsprojName);

            var relativeBuildCs = GetRelativePath(Path.GetDirectoryName(shadowCsprojPath)!, buildCs);

            var projectReference = coreProjectPath != null
                ? $"  <ItemGroup>\n    <ProjectReference Include=\"{coreProjectPath}\" />\n  </ItemGroup>"
                : "";

            var shadowCsproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>disable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                  </PropertyGroup>

                {projectReference}

                  <ItemGroup>
                    <Using Include="LBT.BuildSystem" Static="true" />
                    <Using Include="LBT.Project" Static="true" />
                    <Using Include="LBT" />
                  </ItemGroup>

                  <ItemGroup>
                    <Compile Include="{relativeBuildCs}" />
                  </ItemGroup>
                </Project>
                """;

            File.WriteAllText(shadowCsprojPath, shadowCsproj);
        }
    }

    private static string FindLBTCoreProject(string projectRoot)
    {
        // Try to find LBT.Core.csproj by searching upward
        var currentDir = new DirectoryInfo(projectRoot);
        var maxLevels = 5; // Don't search too far up

        for (int i = 0; i < maxLevels; i++)
        {
            var coreCsproj = Path.Combine(currentDir.FullName, "src", "LBT.Core", "LBT.Core.csproj");
            if (File.Exists(coreCsproj))
            {
                return coreCsproj;
            }

            if (currentDir.Parent == null)
                break;

            currentDir = currentDir.Parent;
        }

        // Fallback: assume we're in the LBT repository
        throw new FileNotFoundException("Cannot find LBT.Core.csproj");
    }

    private static string GetRelativePath(string from, string to)
    {
        var fromUri = new Uri(from.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var toUri = new Uri(to.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var relativeUri = fromUri.MakeRelativeUri(toUri);
        return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar)).TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string ExtractTargetName(string buildCsPath)
    {
        var content = File.ReadAllText(buildCsPath);

        // Try to find Target("name") or Target("name")
        var match = System.Text.RegularExpressions.Regex.Match(content, @"Target\s*\(\s*""([^""]+)""");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Fallback: use filename if no Target found
        var fileName = Path.GetFileNameWithoutExtension(buildCsPath);
        return fileName.Equals("build", StringComparison.OrdinalIgnoreCase)
            ? "root"
            : fileName;
    }
}
