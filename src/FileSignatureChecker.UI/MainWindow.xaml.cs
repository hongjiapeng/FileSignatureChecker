using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Diagnostics;
using FileSignatureChecker.Core.Services;
using FileSignatureChecker.Core.Models;

namespace FileSignatureChecker.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DigitalSignatureService _signatureService;
    private CancellationTokenSource? _cancellationTokenSource;
    private List<string> _signedFiles = new();
    private List<string> _unsignedFiles = new();
    private Stopwatch? _scanStopwatch;

    public MainWindow()
    {
        InitializeComponent();
        _signatureService = new DigitalSignatureService();
        UpdateUI();
    }

    /// <summary>
    /// Handle folder selection button click
    /// </summary>
    private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select directory to scan for digital signatures",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtFolderPath.Text = dialog.SelectedPath;
            UpdateUI();
        }
    }

    /// <summary>
    /// Handle start check button click
    /// </summary>
    private async void BtnStartCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput())
            return;

        await StartSignatureCheckAsync();
    }

    /// <summary>
    /// Handle clear results button click
    /// </summary>
    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ClearResults();
        txtStatus.Text = "Results cleared";
        UpdateUI();
    }

    /// <summary>
    /// Handle cancel button click
    /// </summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Validate user input before starting scan
    /// </summary>
    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(txtFolderPath.Text))
        {
            txtStatus.Text = "❌ Please select a directory to scan first";
            return false;
        }

        if (!Directory.Exists(txtFolderPath.Text))
        {
            txtStatus.Text = "❌ The selected directory does not exist";
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtFileTypes.Text))
        {
            txtStatus.Text = "❌ Please specify file types to check";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Start the signature checking process
    /// </summary>
    private async Task StartSignatureCheckAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _scanStopwatch = Stopwatch.StartNew(); // 开始计时
        
        try
        {
            // Clear previous results
            ClearResults();

            // Setup UI for scanning
            SetScanningMode(true);

            // Prepare scan parameters
            var parameters = new SignatureCheckParameters
            {
                FolderPath = txtFolderPath.Text,
                FileTypes = txtFileTypes.Text.Split(',')
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray(),
                IncludeSubdirectories = chkIncludeSubdirectories.IsChecked == true
            };

            // Create progress reporter
            var progress = new Progress<SignatureCheckProgress>(OnProgressChanged);

            // Start the scan
            var result = await _signatureService.CheckDirectoryAsync(
                parameters, 
                progress, 
                _cancellationTokenSource.Token);

            // Handle results
            HandleScanResult(result);
        }
        catch (OperationCanceledException)
        {
            _scanStopwatch?.Stop();
            var elapsedTime = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
            txtStatus.Text = $"⚠️ Scan cancelled by user (Time elapsed: {FormatElapsedTime(elapsedTime)})";
        }
        catch (Exception ex)
        {
            _scanStopwatch?.Stop();
            var elapsedTime = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
            txtStatus.Text = $"❌ Error: {ex.Message} (Time elapsed: {FormatElapsedTime(elapsedTime)})";
        }
        finally
        {
            SetScanningMode(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Handle progress updates during scanning
    /// </summary>
    private void OnProgressChanged(SignatureCheckProgress progress)
    {
        progressBar.Value = progress.ProgressPercentage;
        txtProgressFile.Text = $"File: {progress.CurrentFile}";
        txtProgressSigned.Text = $"Signed: {progress.SignedCount}";
        txtProgressUnsigned.Text = $"Unsigned: {progress.UnsignedCount}";
        
        // 显示当前扫描进度和已用时间
        var currentElapsed = _scanStopwatch?.Elapsed ?? TimeSpan.Zero;
        txtProgressStatus.Text = $"Scanning... ({progress.ProgressPercentage}%) - {FormatElapsedTime(currentElapsed)}";
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
            txtStatus.Text = $"⚠️ Scan was cancelled (Time elapsed: {FormatElapsedTime(elapsedTime)})";
            return;
        }

        if (result.HasError)
        {
            txtStatus.Text = $"❌ {result.Message} (Time elapsed: {FormatElapsedTime(elapsedTime)})";
            return;
        }

        // Update results
        _signedFiles = result.SignedFiles;
        _unsignedFiles = result.UnsignedFiles;

        // Update UI
        UpdateResultsDisplay();
        
        // Set status message with summary and elapsed time
        UpdateStatusWithSummary(result, elapsedTime);
    }

    /// <summary>
    /// Update status message with scan summary and elapsed time
    /// </summary>
    private void UpdateStatusWithSummary(SignatureCheckResult result, TimeSpan elapsedTime)
    {
        var timeString = FormatElapsedTime(elapsedTime);
        
        if (_unsignedFiles.Count == 0 && _signedFiles.Count > 0)
        {
            txtStatus.Text = $"✅ Excellent! All {_signedFiles.Count} files have valid digital signatures (Time: {timeString})";
        }
        else if (_unsignedFiles.Count > 0)
        {
            txtStatus.Text = $"⚠️ Found {_unsignedFiles.Count} unsigned files out of {result.TotalFilesChecked} total files (Time: {timeString})";
        }
        else if (result.TotalFilesChecked == 0)
        {
            txtStatus.Text = $"ℹ️ No files found matching the specified criteria (Time: {timeString})";
        }
        else
        {
            txtStatus.Text = $"{result.Message} (Time: {timeString})";
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

    /// <summary>
    /// Update the results display in the UI
    /// </summary>
    private void UpdateResultsDisplay()
    {
        // Update signed files list
        lstSignedFiles.Items.Clear();
        foreach (var file in _signedFiles)
        {
            lstSignedFiles.Items.Add(Path.GetFileName(file));
        }

        // Update unsigned files list
        lstUnsignedFiles.Items.Clear();
        foreach (var file in _unsignedFiles)
        {
            lstUnsignedFiles.Items.Add(Path.GetFileName(file));
        }

        // Update counts
        txtSignedCount.Text = $"Count: {_signedFiles.Count}";
        txtUnsignedCount.Text = $"Count: {_unsignedFiles.Count}";
    }

    /// <summary>
    /// Clear all results and reset counters
    /// </summary>
    private void ClearResults()
    {
        _signedFiles.Clear();
        _unsignedFiles.Clear();
        lstSignedFiles.Items.Clear();
        lstUnsignedFiles.Items.Clear();
        txtSignedCount.Text = "Count: 0";
        txtUnsignedCount.Text = "Count: 0";
        progressBar.Value = 0;
    }

    /// <summary>
    /// Update UI state based on current data
    /// </summary>
    private void UpdateUI()
    {
        btnStartCheck.IsEnabled = !string.IsNullOrWhiteSpace(txtFolderPath.Text);
    }

    /// <summary>
    /// Set UI mode for scanning state
    /// </summary>
    private void SetScanningMode(bool isScanning)
    {
        btnStartCheck.IsEnabled = !isScanning;
        btnSelectFolder.IsEnabled = !isScanning;
        txtFileTypes.IsEnabled = !isScanning;
        chkIncludeSubdirectories.IsEnabled = !isScanning;
        
        btnCancel.Visibility = isScanning ? Visibility.Visible : Visibility.Collapsed;
        progressPanel.Visibility = isScanning ? Visibility.Visible : Visibility.Collapsed;
        resultsPanel.Visibility = isScanning ? Visibility.Collapsed : Visibility.Visible;

        if (isScanning)
        {
            progressBar.Value = 0;
            txtProgressStatus.Text = "Initializing scan...";
            txtProgressFile.Text = "File: ";
            txtProgressSigned.Text = "Signed: 0";
            txtProgressUnsigned.Text = "Unsigned: 0";
        }
    }
}
