using System.Collections.Generic;

namespace FileSignatureChecker.Core.Models;

/// <summary>
/// Parameters for signature checking operation
/// </summary>
public class SignatureCheckParameters
{
    /// <summary>
    /// Directory path to check
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// File extensions to check (without dot)
    /// </summary>
    public string[] FileTypes { get; set; } = [];

    /// <summary>
    /// Whether to include subdirectories
    /// </summary>
    public bool IncludeSubdirectories { get; set; }
}

/// <summary>
/// Progress information during signature checking
/// </summary>
public class SignatureCheckProgress
{
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// Currently processing file name
    /// </summary>
    public string CurrentFile { get; set; } = string.Empty;

    /// <summary>
    /// Count of signed files found so far
    /// </summary>
    public int SignedCount { get; set; }

    /// <summary>
    /// Count of unsigned files found so far
    /// </summary>
    public int UnsignedCount { get; set; }

    /// <summary>
    /// Total number of files to process
    /// </summary>
    public int TotalFiles { get; set; }
}

/// <summary>
/// Result of signature checking operation
/// </summary>
public class SignatureCheckResult
{
    /// <summary>
    /// List of files with valid signatures
    /// </summary>
    public List<string> SignedFiles { get; set; } = [];

    /// <summary>
    /// List of files without valid signatures
    /// </summary>
    public List<string> UnsignedFiles { get; set; } = [];

    /// <summary>
    /// Result message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Total number of files checked
    /// </summary>
    public int TotalFilesChecked { get; set; }

    /// <summary>
    /// Whether the operation was cancelled
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Whether an error occurred during the operation
    /// </summary>
    public bool HasError { get; set; }
}
