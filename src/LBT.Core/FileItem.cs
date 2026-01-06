namespace LBT;

/// <summary>
/// Represents a source file.
/// </summary>
public class FileItem
{
    /// <summary>
    /// The full path of the file.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// The file name (without path).
    /// </summary>
    public string FileName => Path.GetFileName(FullPath);

    /// <summary>
    /// The file extension.
    /// </summary>
    public string Extension => Path.GetExtension(FullPath);

    /// <summary>
    /// The project this file belongs to.
    /// </summary>
    public Project Project { get; }

    /// <summary>
    /// The path relative to the project directory.
    /// </summary>
    public string RelativePath => Path.GetRelativePath(Project.Directory, FullPath);

    /// <summary>
    /// Indicates whether this is a C++ source file.
    /// </summary>
    public bool IsCppSource => Extension.ToLowerInvariant() switch
    {
        ".cpp" or ".cxx" or ".cc" or ".c++" => true,
        _ => false
    };

    /// <summary>
    /// Indicates whether this is a C source file.
    /// </summary>
    public bool IsCSource => Extension.Equals(".c", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indicates whether this is a header file.
    /// </summary>
    public bool IsHeader => Extension.ToLowerInvariant() switch
    {
        ".h" or ".hpp" or ".hxx" or ".hh" or ".h++" => true,
        _ => false
    };

    /// <summary>
    /// Gets the corresponding object file path.
    /// </summary>
    /// <param name="outputDir">The output directory.</param>
    /// <returns>The object file path.</returns>
    public string GetObjectFilePath(string outputDir)
    {
        var objName = Path.GetFileNameWithoutExtension(FullPath) +
            (OperatingSystem.IsWindows() ? ".obj" : ".o");

        // Preserve directory structure
        var relativeDir = Path.GetDirectoryName(RelativePath) ?? "";
        return Path.Combine(outputDir, "obj", relativeDir, objName);
    }

    internal FileItem(string fullPath, Project project)
    {
        FullPath = Path.GetFullPath(fullPath);
        Project = project;
    }

    /// <summary>
    /// Returns a string representation of the file item.
    /// </summary>
    /// <returns>The relative path of the file.</returns>
    public override string ToString() => RelativePath;
}
