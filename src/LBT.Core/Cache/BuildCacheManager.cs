using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace LBT.Cache;

/// <summary>
/// Build cache manager.
/// </summary>
public class BuildCacheManager : IDisposable
{
    private readonly BuildCacheContext _context;
    private readonly string _cacheDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildCacheManager"/> class.
    /// </summary>
    /// <param name="projectRoot">The project root directory path.</param>
    public BuildCacheManager(string projectRoot)
    {
        _cacheDir = Path.Combine(projectRoot, ".lbt");
        Directory.CreateDirectory(_cacheDir);

        var dbPath = Path.Combine(_cacheDir, "cache.db");
        _context = new BuildCacheContext(dbPath);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Computes the SHA256 hash of file content.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The SHA256 hash as a hex string.</returns>
    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Computes the hash of a string.
    /// </summary>
    /// <param name="content">The string content.</param>
    /// <returns>The SHA256 hash as a hex string.</returns>
    public static string ComputeStringHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Gets or updates the source file cache.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The source file cache entry.</returns>
    public async Task<SourceFileCache> GetOrUpdateSourceFileCacheAsync(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fileInfo = new FileInfo(fullPath);

        var cached = await _context.SourceFiles.FindAsync(fullPath);

        // If cache exists and file is unmodified, return directly
        if (cached != null &&
            cached.LastModified == fileInfo.LastWriteTimeUtc &&
            cached.FileSize == fileInfo.Length)
        {
            return cached;
        }

        // Recompute hash
        var hash = ComputeFileHash(fullPath);

        if (cached == null)
        {
            cached = new SourceFileCache
            {
                FilePath = fullPath,
                ContentHash = hash,
                LastModified = fileInfo.LastWriteTimeUtc,
                FileSize = fileInfo.Length
            };
            _context.SourceFiles.Add(cached);
        }
        else
        {
            cached.ContentHash = hash;
            cached.LastModified = fileInfo.LastWriteTimeUtc;
            cached.FileSize = fileInfo.Length;
        }

        await _context.SaveChangesAsync();
        return cached;
    }

    /// <summary>
    /// Checks whether the compile unit needs recompilation.
    /// </summary>
    /// <param name="sourceFile">The source file path.</param>
    /// <param name="objectFile">The object file path.</param>
    /// <param name="compilerArgs">The compiler arguments.</param>
    /// <param name="toolchainName">The toolchain name.</param>
    /// <returns>True if rebuild is needed; otherwise, false.</returns>
    public async Task<bool> NeedsRebuildAsync(
        string sourceFile,
        string objectFile,
        string compilerArgs,
        string toolchainName)
    {
        var sourcePath = Path.GetFullPath(sourceFile);
        var objectPath = Path.GetFullPath(objectFile);

        // Object file does not exist, needs compilation
        if (!File.Exists(objectPath))
        {
            return true;
        }

        // Get cached compile unit information
        var cached = await _context.CompileUnits.FindAsync(objectPath);
        if (cached == null)
        {
            return true;
        }

        // Toolchain changed
        if (cached.ToolchainName != toolchainName)
        {
            return true;
        }

        // Compiler arguments changed
        var argsHash = ComputeStringHash(compilerArgs);
        if (cached.CompilerArgsHash != argsHash)
        {
            return true;
        }

        // Source file changed
        var sourceCache = await GetOrUpdateSourceFileCacheAsync(sourcePath);
        if (cached.SourceHash != sourceCache.ContentHash)
        {
            return true;
        }

        // Check dependent header files
        var dependencies = await _context.HeaderDependencies
            .Where(d => d.SourceFilePath == sourcePath && !d.IsSystemHeader)
            .ToListAsync();

        foreach (var dep in dependencies)
        {
            if (!File.Exists(dep.HeaderFilePath))
            {
                return true; // Dependency file was deleted
            }

            var depCache = await GetOrUpdateSourceFileCacheAsync(dep.HeaderFilePath);
            // Simplified handling here; should compare combined hash in practice
        }

        // Compute dependencies hash
        var currentDepsHash = await ComputeDependenciesHashAsync(sourcePath);
        if (cached.DependenciesHash != currentDepsHash)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records successful compilation.
    /// </summary>
    /// <param name="sourceFile">The source file path.</param>
    /// <param name="objectFile">The object file path.</param>
    /// <param name="compilerArgs">The compiler arguments.</param>
    /// <param name="toolchainName">The toolchain name.</param>
    /// <param name="headerDependencies">The header file dependencies.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RecordCompilationAsync(
        string sourceFile,
        string objectFile,
        string compilerArgs,
        string toolchainName,
        IEnumerable<string> headerDependencies)
    {
        var sourcePath = Path.GetFullPath(sourceFile);
        var objectPath = Path.GetFullPath(objectFile);

        // Get source file hash
        var sourceCache = await GetOrUpdateSourceFileCacheAsync(sourcePath);

        // Update header file dependencies
        var existingDeps = await _context.HeaderDependencies
            .Where(d => d.SourceFilePath == sourcePath)
            .ToListAsync();
        _context.HeaderDependencies.RemoveRange(existingDeps);

        foreach (var header in headerDependencies)
        {
            var headerPath = Path.GetFullPath(header);
            var isSystem = IsSystemHeader(headerPath);

            _context.HeaderDependencies.Add(new HeaderDependencyCache
            {
                SourceFilePath = sourcePath,
                HeaderFilePath = headerPath,
                IsSystemHeader = isSystem
            });
        }

        // Compute dependencies hash
        var depsHash = await ComputeDependenciesHashAsync(sourcePath);

        // Update compile unit record
        var cached = await _context.CompileUnits.FindAsync(objectPath);
        if (cached == null)
        {
            cached = new CompileUnitCache
            {
                ObjectFilePath = objectPath,
                SourceFilePath = sourcePath,
                SourceHash = sourceCache.ContentHash,
                CompilerArgsHash = ComputeStringHash(compilerArgs),
                DependenciesHash = depsHash,
                CompiledAt = DateTime.UtcNow,
                ToolchainName = toolchainName
            };
            _context.CompileUnits.Add(cached);
        }
        else
        {
            cached.SourceHash = sourceCache.ContentHash;
            cached.CompilerArgsHash = ComputeStringHash(compilerArgs);
            cached.DependenciesHash = depsHash;
            cached.CompiledAt = DateTime.UtcNow;
            cached.ToolchainName = toolchainName;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Computes the combined hash of all dependency files.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <returns>The combined hash of all dependencies.</returns>
    private async Task<string> ComputeDependenciesHashAsync(string sourcePath)
    {
        var dependencies = await _context.HeaderDependencies
            .Where(d => d.SourceFilePath == sourcePath && !d.IsSystemHeader)
            .OrderBy(d => d.HeaderFilePath)
            .ToListAsync();

        var sb = new StringBuilder();
        foreach (var dep in dependencies)
        {
            if (File.Exists(dep.HeaderFilePath))
            {
                var cache = await GetOrUpdateSourceFileCacheAsync(dep.HeaderFilePath);
                sb.Append(cache.ContentHash);
            }
        }

        return ComputeStringHash(sb.ToString());
    }

    /// <summary>
    /// Determines whether a header is a system header file.
    /// </summary>
    /// <param name="headerPath">The header file path.</param>
    /// <returns>True if it is a system header; otherwise, false.</returns>
    private static bool IsSystemHeader(string headerPath)
    {
        // Simple check: headers outside project directory are treated as system headers
        var commonSystemPaths = new[]
        {
            "/usr/include",
            "/usr/local/include",
            "Program Files",
            "Windows Kits",
            "Microsoft Visual Studio"
        };

        return commonSystemPaths.Any(p => headerPath.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearCacheAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM SourceFiles");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM CompileUnits");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM HeaderDependencies");
    }

    /// <summary>
    /// Releases all resources used by the <see cref="BuildCacheManager"/>.
    /// </summary>
    public void Dispose()
    {
        _context.Dispose();
    }
}
