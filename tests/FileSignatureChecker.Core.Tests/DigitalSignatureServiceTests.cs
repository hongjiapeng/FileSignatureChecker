using Xunit;
using FileSignatureChecker.Core.Models;
using FileSignatureChecker.Core.Services;

namespace FileSignatureChecker.Core.Tests;

/// <summary>
/// Tests for DigitalSignatureService
/// </summary>
public class DigitalSignatureServiceTests
{
    private readonly DigitalSignatureService _service;

    public DigitalSignatureServiceTests()
    {
        _service = new DigitalSignatureService();
    }

    [Fact]
    public void IsFileSigned_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = @"C:\NonExistent\File.exe";

        // Act
        var result = _service.IsFileSigned(nonExistentFile);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SignatureCheckParameters_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var parameters = new SignatureCheckParameters();

        // Assert
        Assert.Equal(string.Empty, parameters.FolderPath);
        Assert.Empty(parameters.FileTypes);
        Assert.False(parameters.IncludeSubdirectories);
    }

    [Fact]
    public void SignatureCheckResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new SignatureCheckResult();

        // Assert
        Assert.Empty(result.SignedFiles);
        Assert.Empty(result.UnsignedFiles);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(0, result.TotalFilesChecked);
        Assert.False(result.IsCancelled);
        Assert.False(result.HasError);
    }

    [Fact]
    public void SignatureCheckProgress_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var progress = new SignatureCheckProgress();

        // Assert
        Assert.Equal(0, progress.ProgressPercentage);
        Assert.Equal(string.Empty, progress.CurrentFile);
        Assert.Equal(0, progress.SignedCount);
        Assert.Equal(0, progress.UnsignedCount);
        Assert.Equal(0, progress.TotalFiles);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsFileSigned_WithInvalidPath_ReturnsFalse(string? filePath)
    {
        // Act
        var result = _service.IsFileSigned(filePath!);

        // Assert
        Assert.False(result);
    }
}
