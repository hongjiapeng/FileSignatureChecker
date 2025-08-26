using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileSignatureChecker.Services;

/// <summary>
/// Service for digital signature detection and validation
/// </summary>
public class DigitalSignatureService
{
    /// <summary>
    /// Check digital signatures for files in a directory
    /// </summary>
    /// <param name="parameters">Check parameters</param>
    /// <param name="progressCallback">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Check result</returns>
    public async Task<SignatureCheckResult> CheckDirectoryAsync(
        SignatureCheckParameters parameters,
        IProgress<SignatureCheckProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var signedFiles = new List<string>();
        var unsignedFiles = new List<string>();

        try
        {
            // Get all files to check
            var searchOption = parameters.IncludeSubdirectories ?
                SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var allFiles = new List<string>();
            foreach (var extension in parameters.FileTypes)
            {
                var pattern = $"*.{extension}";
                try
                {
                    var files = Directory.GetFiles(parameters.FolderPath, pattern, searchOption);
                    allFiles.AddRange(files);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories without access permission
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    // Skip non-existent directories
                    continue;
                }
            }

            // Remove duplicates
            allFiles = allFiles.Distinct().ToList();

            if (allFiles.Count == 0)
            {
                return new SignatureCheckResult
                {
                    SignedFiles = signedFiles,
                    UnsignedFiles = unsignedFiles,
                    Message = "No files found matching the specified criteria",
                    TotalFilesChecked = 0
                };
            }

            // Check each file's signature status
            for (int i = 0; i < allFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = allFiles[i];
                var progress = (double)(i + 1) / allFiles.Count * 100;

                try
                {
                    if (IsFileSigned(file))
                    {
                        signedFiles.Add(file);
                    }
                    else
                    {
                        unsignedFiles.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    // If detection fails, categorize as unsigned
                    unsignedFiles.Add($"{file} (Detection failed: {ex.Message})");
                }

                // Report progress
                progressCallback?.Report(new SignatureCheckProgress
                {
                    ProgressPercentage = (int)progress,
                    CurrentFile = Path.GetFileName(file),
                    SignedCount = signedFiles.Count,
                    UnsignedCount = unsignedFiles.Count,
                    TotalFiles = allFiles.Count
                });

                // Add small delay to prevent UI blocking
                if (i % 10 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return new SignatureCheckResult
            {
                SignedFiles = signedFiles,
                UnsignedFiles = unsignedFiles,
                Message = $"Check completed! Processed {allFiles.Count} files",
                TotalFilesChecked = allFiles.Count
            };
        }
        catch (OperationCanceledException)
        {
            return new SignatureCheckResult
            {
                SignedFiles = signedFiles,
                UnsignedFiles = unsignedFiles,
                Message = "Operation was cancelled",
                TotalFilesChecked = signedFiles.Count + unsignedFiles.Count,
                IsCancelled = true
            };
        }
        catch (Exception ex)
        {
            return new SignatureCheckResult
            {
                SignedFiles = signedFiles,
                UnsignedFiles = unsignedFiles,
                Message = $"Error during check: {ex.Message}",
                TotalFilesChecked = signedFiles.Count + unsignedFiles.Count,
                HasError = true
            };
        }
    }

    /// <summary>
    /// Check if a file has a valid digital signature
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <returns>True if file has a valid digital signature</returns>
    public bool IsFileSigned(string filePath)
    {
        try
        {
            // First check if file exists
            if (!File.Exists(filePath))
                return false;

            // Use WinVerifyTrust API for accurate detection
            return IsFileSignedUsingWinVerifyTrust(filePath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Check file signature using WinVerifyTrust API
    /// </summary>
    private static bool IsFileSignedUsingWinVerifyTrust(string filePath)
    {
        try
        {
            // Create file information structure
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            // Create trust data structure with standard configuration
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,               // No UI
                fdwRevocationChecks = WTD_REVOKE_NONE,  // No revocation check (faster)
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>()),
                dwStateAction = WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = null,
                dwProvFlags = 0,                        // Use default flags
                dwUIContext = 0
            };

            Marshal.StructureToPtr(fileInfo, data.pFile, false);

            try
            {
                var actionGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;
                var result = WinVerifyTrust(IntPtr.Zero, ref actionGuid, ref data);

                // WinVerifyTrust return values:
                // 0 (ERROR_SUCCESS) = Valid signature
                // Other values = Invalid or no signature
                return result == 0;
            }
            finally
            {
                // Cleanup state
                data.dwStateAction = WTD_STATEACTION_CLOSE;
                var actionGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;
                WinVerifyTrust(IntPtr.Zero, ref actionGuid, ref data);

                if (data.pFile != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(data.pFile);
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    #region Win32 API Definitions

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WINTRUST_DATA pWVTData);

    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    #endregion
}
