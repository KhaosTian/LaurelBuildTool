using Microsoft.EntityFrameworkCore;

namespace LBT.Cache;

/// <summary>
/// Build cache database context.
/// </summary>
public class BuildCacheContext : DbContext
{
    private readonly string _dbPath;

    /// <summary>
    /// Gets the source files cache set.
    /// </summary>
    public DbSet<SourceFileCache> SourceFiles => Set<SourceFileCache>();

    /// <summary>
    /// Gets the compile units cache set.
    /// </summary>
    public DbSet<CompileUnitCache> CompileUnits => Set<CompileUnitCache>();

    /// <summary>
    /// Gets the header dependencies cache set.
    /// </summary>
    public DbSet<HeaderDependencyCache> HeaderDependencies => Set<HeaderDependencyCache>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildCacheContext"/> class.
    /// </summary>
    /// <param name="dbPath">The database file path.</param>
    public BuildCacheContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    /// <summary>
    /// Configures the database context to use SQLite.
    /// </summary>
    /// <param name="options">The options builder.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    /// <summary>
    /// Configures the entity models and relationships.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Source file cache
        modelBuilder.Entity<SourceFileCache>(entity =>
        {
            entity.HasKey(e => e.FilePath);
            entity.HasIndex(e => e.LastModified);
        });

        // Compile unit cache
        modelBuilder.Entity<CompileUnitCache>(entity =>
        {
            entity.HasKey(e => e.ObjectFilePath);
            entity.HasIndex(e => e.SourceFilePath);
        });

        // Header dependencies
        modelBuilder.Entity<HeaderDependencyCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceFilePath);
            entity.HasIndex(e => e.HeaderFilePath);
        });
    }
}

/// <summary>
/// Source file cache record.
/// </summary>
public class SourceFileCache
{
    /// <summary>
    /// The full file path (primary key).
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The SHA256 hash of the file content.
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// The last modification time.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public long FileSize { get; set; }
}

/// <summary>
/// Compile unit cache record.
/// </summary>
public class CompileUnitCache
{
    /// <summary>
    /// The object file path (primary key).
    /// </summary>
    public required string ObjectFilePath { get; set; }

    /// <summary>
    /// The source file path.
    /// </summary>
    public required string SourceFilePath { get; set; }

    /// <summary>
    /// The hash of the source file.
    /// </summary>
    public required string SourceHash { get; set; }

    /// <summary>
    /// The hash of the compiler arguments.
    /// </summary>
    public required string CompilerArgsHash { get; set; }

    /// <summary>
    /// The combined hash of all dependency files.
    /// </summary>
    public required string DependenciesHash { get; set; }

    /// <summary>
    /// The compilation time.
    /// </summary>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// The toolchain used for compilation.
    /// </summary>
    public required string ToolchainName { get; set; }
}

/// <summary>
/// Header file dependency cache.
/// </summary>
public class HeaderDependencyCache
{
    /// <summary>
    /// The auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The source file path.
    /// </summary>
    public required string SourceFilePath { get; set; }

    /// <summary>
    /// The path of the dependent header file.
    /// </summary>
    public required string HeaderFilePath { get; set; }

    /// <summary>
    /// Indicates whether this is a system header file.
    /// </summary>
    public bool IsSystemHeader { get; set; }
}
