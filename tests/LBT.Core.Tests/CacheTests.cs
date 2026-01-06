using Xunit;

using LBT.Cache;

namespace LBT.Tests;

public class CacheTests
{
    [Fact]
    public void ComputeStringHash_SameInput_SameOutput()
    {
        // Arrange
        var input = "test string";

        // Act
        var hash1 = BuildCacheManager.ComputeStringHash(input);
        var hash2 = BuildCacheManager.ComputeStringHash(input);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeStringHash_DifferentInput_DifferentOutput()
    {
        // Arrange & Act
        var hash1 = BuildCacheManager.ComputeStringHash("input1");
        var hash2 = BuildCacheManager.ComputeStringHash("input2");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeStringHash_ShouldReturnHexString()
    {
        // Arrange & Act
        var hash = BuildCacheManager.ComputeStringHash("test");

        // Assert
        Assert.Matches("^[0-9A-F]+$", hash);
        Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
    }
}
