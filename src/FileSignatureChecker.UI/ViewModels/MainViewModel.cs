using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSignatureChecker.Core.Models;
using FileSignatureChecker.Core.Services;

namespace FileSignatureChecker.UI.ViewModels;

/// <summary>
/// Main view model for the digital signature checker application
/// Implements MVVM pattern using CommunityToolkit.Mvvm
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DigitalSignatureService _signatureService;
    private CancellationTokenSource? _cancellationTokenSource;
    private Stopwatch? _scanStopwatch;

    public MainViewModel()
    {
        _signatureService = new DigitalSignatureService();
        UpdateCanStartCheck();
        UpdateVersionInfo();
    }

    #region Observable Properties

    /// <summary>
    /// Selected folder path to scan
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCheckCommand))]
    private string _folderPath = "Select a directory to scan...";

    /// <summary>
    /// File types to check (comma-separated extensions)
    /// </summary>
    [ObservableProperty]
    private string _fileTypes = "exe,dll,winmd";

    /// <summary>
    /// Whether to include subdirectories in scan
    /// </summary>
    [ObservableProperty]
    private bool _includeSubdirectories = true;

    /// <summary>
    /// Status message displayed at the bottom
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Ready to scan";

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    [ObservableProperty]
    private int _progressPercentage;

    /// <summary>
    /// Progress status text
    /// </summary>
    [ObservableProperty]
    private string _progressStatus = "Initializing scan...";

    /// <summary>
    /// Current file being processed
    /// </summary>
    [ObservableProperty]
    private string _progressFile = "File: ";

    /// <summary>
    /// Count of signed files found during scan
    /// </summary>
    [ObservableProperty]
    private string _progressSigned = "Signed: 0";

    /// <summary>
    /// Count of unsigned files found during scan
    /// </summary>
    [ObservableProperty]
    private string _progressUnsigned = "Unsigned: 0";

    /// <summary>
    /// Whether scanning is in progress
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotScanning))]
    [NotifyPropertyChangedFor(nameof(ProgressPanelVisibility))]
    [NotifyPropertyChangedFor(nameof(ResultsPanelVisibility))]
    [NotifyPropertyChangedFor(nameof(CancelButtonVisibility))]
    [NotifyCanExecuteChangedFor(nameof(StartCheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private bool _isScanning;

    /// <summary>
    /// Collection of signed files
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _signedFiles = new();

    /// <summary>
    /// Collection of unsigned files
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _unsignedFiles = new();

    /// <summary>
    /// Count of signed files
    /// </summary>
    [ObservableProperty]
    private string _signedCount = "Count: 0";

    /// <summary>
    /// Count of unsigned files
    /// </summary>
    [ObservableProperty]
    private string _unsignedCount = "Count: 0";

    /// <summary>
    /// Application version information
    /// </summary>
    [ObservableProperty]
    private string _version = "v1.0.0";

    #endregion

    #region Computed Properties

    /// <summary>
    /// Inverse of IsScanning for UI binding
    /// </summary>
    public bool IsNotScanning => !IsScanning;

    /// <summary>
    /// Visibility of progress panel
    /// </summary>
    public Visibility ProgressPanelVisibility => IsScanning ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Visibility of results panel
    /// </summary>
    public Visibility ResultsPanelVisibility => IsScanning ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Visibility of cancel button
    /// </summary>
    public Visibility CancelButtonVisibility => IsScanning ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    /// <summary>
    /// Command to select folder for scanning
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectFolder))]
    private void SelectFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select directory to scan for digital signatures",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderPath = dialog.SelectedPath;
            UpdateCanStartCheck();
        }
    }

    /// <summary>
    /// Determines if folder can be selected
    /// </summary>
    private bool CanSelectFolder() => !IsScanning;

    /// <summary>
    /// Command to start signature check
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartCheck))]
    private async Task StartCheckAsync()
    {
        if (!ValidateInput())
            return;

        await PerformSignatureCheckAsync();
    }

    /// <summary>
    /// Determines if check can be started
    /// </summary>
    private bool CanStartCheck()
    {
        return !IsScanning && 
               !string.IsNullOrWhiteSpace(FolderPath) && 
               FolderPath != "Select a directory to scan...";
    }

    /// <summary>
    /// Command to clear results
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        ClearResults();
        StatusMessage = "Results cleared";
    }

    /// <summary>
    /// Determines if results can be cleared
    /// </summary>
    private bool CanClear() => !IsScanning;

    /// <summary>
    /// Command to cancel ongoing scan
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Validate user input before starting scan
    /// </summary>
    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || FolderPath == "Select a directory to scan...")
        {
            StatusMessage = "❌ Please select a directory to scan first";
            return false;
        }

        if (!Directory.Exists(FolderPath))
        {
            StatusMessage = "❌ The selected directory does not exist";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FileTypes))
        {
            StatusMessage = "❌ Please specify file types to check";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Perform the signature checking process
    /// </summary>
    private async Task PerformSignatureCheckAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _scanStopwatch = Stopwatch.StartNew();

        try
        {
            // Clear previous results
            ClearResults();

            // Setup UI for scanning
            IsScanning = true;
            ProgressPercentage = 0;
            ProgressStatus = "Initializing scan...";
            ProgressFile = "File: ";
            ProgressSigned = "Signed: 0";
            ProgressUnsigned = "Unsigned: 0";

            // Prepare scan parameters
            var parameters = new SignatureCheckParameters
            {
                FolderPath = FolderPath,
                FileTypes = FileTypes.Split(',')
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray(),
                IncludeSubdirectories = IncludeSubdirectories
            };

            // Create progress reporter
            var progress = new Progress<SignatureCheckProgress>(OnProgressChanged);

            // Start the scan - run on background thread to avoid UI blocking
            var result = await Task.Run(
                () => _signatureService.CheckDirectoryAsync(
                    parameters,
                    progress,
                    _cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            // Handle results on UI thread
            HandleScanResult(result);
        }
        catch (OperationCanceledException)
        {
            _scanStopwatch?.Stop();
            var elapsedTime = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
            StatusMessage = $"⚠️ Scan cancelled by user (Time elapsed: {FormatElapsedTime(elapsedTime)})";
        }
        catch (Exception ex)
        {
            _scanStopwatch?.Stop();
            var elapsedTime = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
            StatusMessage = $"❌ Error: {ex.Message} (Time elapsed: {FormatElapsedTime(elapsedTime)})";
        }
        finally
        {
            IsScanning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Handle progress updates during scanning
    /// </summary>
    private void OnProgressChanged(SignatureCheckProgress progress)
    {
        ProgressPercentage = progress.ProgressPercentage;
        ProgressFile = $"File: {progress.CurrentFile}";
        ProgressSigned = $"Signed: {progress.SignedCount}";
        ProgressUnsigned = $"Unsigned: {progress.UnsignedCount}";

        // Display current scan progress and elapsed time
        var currentElapsed = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
        ProgressStatus = $"Scanning... ({progress.ProgressPercentage}%) - {FormatElapsedTime(currentElapsed)}";
    }

    /// <summary>
    /// Handle scan completion and results
    /// </summary>
    private void HandleScanResult(SignatureCheckResult result)
    {
        _scanStopwatch?.Stop();
        var elapsedTime = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;

        if (result.IsCancelled)
        {
            StatusMessage = $"⚠️ Scan was cancelled (Time elapsed: {FormatElapsedTime(elapsedTime)})";
            return;
        }

        if (result.HasError)
        {
            StatusMessage = $"❌ {result.Message} (Time elapsed: {FormatElapsedTime(elapsedTime)})";
            return;
        }

        // Update results
        UpdateResultsDisplay(result);

        // Set status message with summary and elapsed time
        UpdateStatusWithSummary(result, elapsedTime);
    }

    /// <summary>
    /// Update the results display in the UI
    /// </summary>
    private void UpdateResultsDisplay(SignatureCheckResult result)
    {
        // Update signed files
        SignedFiles.Clear();
        foreach (var file in result.SignedFiles)
        {
            SignedFiles.Add(Path.GetFileName(file));
        }

        // Update unsigned files
        UnsignedFiles.Clear();
        foreach (var file in result.UnsignedFiles)
        {
            UnsignedFiles.Add(Path.GetFileName(file));
        }

        // Update counts
        SignedCount = $"Count: {result.SignedFiles.Count}";
        UnsignedCount = $"Count: {result.UnsignedFiles.Count}";
    }

    /// <summary>
    /// Update status message with scan summary and elapsed time
    /// </summary>
    private void UpdateStatusWithSummary(SignatureCheckResult result, TimeSpan elapsedTime)
    {
        var timeString = FormatElapsedTime(elapsedTime);

        if (result.UnsignedFiles.Count == 0 && result.SignedFiles.Count > 0)
        {
            StatusMessage = $"✅ Excellent! All {result.SignedFiles.Count} files have valid digital signatures (Time: {timeString})";
        }
        else if (result.UnsignedFiles.Count > 0)
        {
            StatusMessage = $"⚠️ Found {result.UnsignedFiles.Count} unsigned files out of {result.TotalFilesChecked} total files (Time: {timeString})";
        }
        else if (result.TotalFilesChecked == 0)
        {
            StatusMessage = $"ℹ️ No files found matching the specified criteria (Time: {timeString})";
        }
        else
        {
            StatusMessage = $"{result.Message} (Time: {timeString})";
        }
    }

    /// <summary>
    /// Clear all results and reset counters
    /// </summary>
    private void ClearResults()
    {
        SignedFiles.Clear();
        UnsignedFiles.Clear();
        SignedCount = "Count: 0";
        UnsignedCount = "Count: 0";
        ProgressPercentage = 0;
    }

    /// <summary>
    /// Update command execution state
    /// </summary>
    private void UpdateCanStartCheck()
    {
        StartCheckCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Update version information from assembly
    /// </summary>
    private void UpdateVersionInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                Version = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch
        {
            Version = "v1.0.0";
        }
    }

    /// <summary>
    /// Format elapsed time for display
    /// </summary>
    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalDays >= 1)
        {
            return $"{elapsed:d\\d\\ h\\h\\ m\\m\\ s\\s}";
        }
        else if (elapsed.TotalHours >= 1)
        {
            return $"{elapsed:h\\h\\ m\\m\\ s\\s}";
        }
        else if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed:m\\m\\ s\\s}";
        }
        else if (elapsed.TotalSeconds >= 1)
        {
            return $"{elapsed.TotalSeconds:F1}s";
        }
        else
        {
            return $"{elapsed.TotalMilliseconds:F0}ms";
        }
    }

    #endregion
}
