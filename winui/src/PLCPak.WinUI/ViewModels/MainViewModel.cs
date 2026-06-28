using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PlcPakAppContext _app;
    private readonly UiDispatcher _ui;
    private readonly UiModeNotifier _uiMode;
    private CompressConfig _config;
    private bool _hasCompressed;
    private Func<nint>? _getWindowHandle;
    private ProgressDisplayMode _progressMode = ProgressDisplayMode.Ready;

    private enum ProgressDisplayMode
    {
        Ready,
        Scanning,
        ScanningSamples,
        Percent
    }

    public MainViewModel(PlcPakAppContext app)
    {
        _app = app;
        _ui = UiDispatcher.ForCurrentThread();
        _uiMode = new UiModeNotifier(app, () =>
        {
            OnPropertyChanged(nameof(IsProfessionalMode));
            OnPropertyChanged(nameof(IsSimpleMode));
        });
        LocalizationService.LanguageChanged += OnLanguageChanged;
        _config = app.Config.Load();
        RecentProjects = new ObservableCollection<string>(_config.RecentProjects);
        SourceItems = new ObservableCollection<string>();
        _app.Pipeline.TasksChanged += () => _ui.Run(UpdateTaskStatus);

        SevenZOutputName = string.Empty;
        TarZstOutputName = DateTime.Now.ToString("yyMMdd_01");
        VolumeSizeMB = _config.VolumeSizeMB;
        VolumeSizeText = _config.VolumeSizeMB.ToString();
        TaskStatus = T("quick.page.taskStatus.initial");
        UpdateAdSampleCount();
        UpdateTotalSize();
        UpdateSourceListState();
        InitializeToolStatus();
    }

    public void SetWindowHandleProvider(Func<nint> provider) => _getWindowHandle = provider;

    public ObservableCollection<string> SourceItems { get; }
    public ObservableCollection<string> RecentProjects { get; }

    public string QuickPageTitle => T("quick.page.title");
    public string QuickSimpleHint => T("quick.page.simpleHint");
    public string QuickHeroTitle => T("quick.page.heroTitle");
    public string QuickHeroFlow => T("quick.page.heroFlow");
    public string QuickRecentProjectsLabel => T("quick.page.recentProjects");
    public string QuickSettingsLabel => T("quick.page.settings");
    public string QuickSourceListHeader => T("quick.page.sourceListHeader");
    public string QuickAddFolderLabel => T("quick.page.addFolder");
    public string QuickAddFileLabel => T("quick.page.addFile");
    public string QuickRemoveSelectedLabel => T("quick.page.removeSelected");
    public string QuickSelfPurchaseLabel => T("quick.page.selfPurchase");
    public string QuickSevenZTitle => T("quick.page.sevenZTitle");
    public string QuickEnableCompressionLabel => T("quick.page.enableCompression");
    public string QuickOutputNameLabel => T("quick.page.outputName");
    public string QuickSevenZOutputPlaceholder => T("quick.page.sevenZOutputPlaceholder");
    public string QuickVolumeSizeMbLabel => T("quick.page.volumeSizeMb");
    public string QuickVolumeSizeHint => T("quick.page.volumeSizeHint");
    public string QuickTarZstTitle => T("quick.page.tarZstTitle");
    public string QuickAdSamplesTitle => T("quick.page.adSamplesTitle");
    public string QuickAdDropHint => T("quick.page.adDropHint");
    public string QuickBrowseSamplesLabel => T("quick.page.browseSamples");
    public string QuickSyncManifestLabel => T("quick.page.syncManifest");
    public string QuickPruneLargeSamplesLabel => T("quick.page.pruneLargeSamples");
    public string QuickPreviewCleanupLabel => T("quick.page.previewCleanup");
    public string QuickExportLogLabel => T("quick.page.exportLog");
    public string QuickExecuteLabel => T("quick.page.execute");

    [ObservableProperty] private string _taskStatus = string.Empty;
    [ObservableProperty] private string _totalSizeText = string.Empty;
    [ObservableProperty] private bool _selfPurchase;
    [ObservableProperty] private bool _enable7z = true;
    [ObservableProperty] private bool _enableTarZst = true;
    [ObservableProperty] private string _sevenZOutputName = string.Empty;
    [ObservableProperty] private string _tarZstOutputName = string.Empty;
    [ObservableProperty] private int _volumeSizeMB = 1900;
    [ObservableProperty] private string _volumeSizeText = "1900";

    partial void OnVolumeSizeMBChanged(int value) => VolumeSizeText = value.ToString();

    partial void OnVolumeSizeTextChanged(string value)
    {
        if (int.TryParse(value, out var mb) && mb is >= 100 and <= 10000 && mb != VolumeSizeMB)
            VolumeSizeMB = mb;
    }

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isProgressIndeterminate;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private bool _enable7zOption = true;
    [ObservableProperty] private bool _enableTarOption = true;
    [ObservableProperty] private bool _autoPruneLargeSamples = true;
    [ObservableProperty] private string _adSampleCountText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecute))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQuickSourceCount))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    [ObservableProperty] private string? _selectedRecentProject;
    [ObservableProperty] private string _quickDropFeedbackText = string.Empty;
    [ObservableProperty] private string _quickAdDropFeedbackText = string.Empty;

    private CancellationTokenSource? _sourceDropFeedbackCts;
    private CancellationTokenSource? _adDropFeedbackCts;

    public bool CanExecute => !IsBusy;
    public bool IsSourceListEmpty => SourceItems.Count == 0;
    public string QuickSourceCountText => string.Format(T("quick.page.sourceCount"), SourceItems.Count);
    public string QuickSourceCountLoadingHint => T("quick.page.sourceCountLoading");
    public bool ShowQuickSourceCount => !IsRefreshing;
    public string QuickSourceEmptyHint => T("quick.page.sourceEmpty");
    public string QuickRefreshingHint => T("quick.page.refreshing");
    public bool IsProfessionalMode => _uiMode.IsProfessionalMode;
    public bool IsSimpleMode => _uiMode.IsSimpleMode;
    public CompressConfig CurrentConfig => _config;

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            await AddSourcePathsAsync([folder.Path]);
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count == 0)
            return;

        if (_hasCompressed)
        {
            SourceItems.Clear();
            _hasCompressed = false;
            AppendLog(T("quick.page.log.clearedList"));
        }

        foreach (var file in files.Where(f => !SourceItems.Contains(f.Path)))
            SourceItems.Add(file.Path);

        UpdateTotalSize();
        UpdateTaskStatus();
        UpdateSourceListState();
    }

    [RelayCommand]
    private void RemoveSelected(IList<object>? selectedItems)
    {
        if (selectedItems is null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems.OfType<string>().ToList())
        {
            SourceItems.Remove(item);
            _app.Pipeline.RemoveTask(item);
        }

        UpdateTotalSize();
        UpdateTaskStatus();
        UpdateSourceListState();
    }

    [RelayCommand]
    private async Task SelectRecentProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentProject) || !Directory.Exists(SelectedRecentProject))
            return;
        await AddSourcePathsAsync([SelectedRecentProject]);
    }

    public void ApplySettings(CompressConfig config)
    {
        _config = config;
        VolumeSizeMB = config.VolumeSizeMB;
        _app.Config.Save(config);
        RecentProjects.Clear();
        foreach (var project in config.RecentProjects)
            RecentProjects.Add(project);
        AppendLog(T("quick.page.log.settingsSaved"));
    }

    [RelayCommand]
    private async Task PreviewCleanupAsync()
    {
        if (SourceItems.Count == 0)
            return;

        var lines = new List<string>();
        foreach (var item in SourceItems.Where(Directory.Exists))
        {
            var preview = await Task.Run(() => _app.Cleanup.Invoke(item, _config, previewOnly: true));
            lines.Add(T("quick.page.preview.sectionHeader", item));
            lines.Add(T("quick.page.preview.scannedMatched", preview.TotalScanned, preview.TotalMatched));
            lines.AddRange(preview.MatchedFiles.Select(m => T("quick.page.preview.willDelete", m)));
        }

        PreviewRequested?.Invoke(lines.Count > 0 ? string.Join(Environment.NewLine, lines) : T("quick.page.preview.noMatch"));
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.FileTypeChoices.Add(T("quick.page.picker.logFileType"), [".txt"]);
        picker.SuggestedFileName = $"PLCPak-log-{DateTime.Now:yyyyMMdd-HHmmss}";
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        await Windows.Storage.FileIO.WriteTextAsync(file, LogText);
        AppendLog(T("quick.page.log.exported", file.Path));
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SourceItems.Count == 0)
        {
            MessageRequested?.Invoke(T("quick.page.error.addSources"), T("quick.page.error.title"));
            return;
        }

        if (!Enable7z && !EnableTarZst)
        {
            MessageRequested?.Invoke(T("quick.page.error.pickFormat"), T("quick.page.error.title"));
            return;
        }

        if (VolumeSizeMB is < 100 or > 10000)
        {
            MessageRequested?.Invoke(T("quick.page.error.volumeRange"), T("quick.page.error.title"));
            return;
        }

        foreach (var item in SourceItems)
        {
            if (!File.Exists(item) && !Directory.Exists(item))
            {
                MessageRequested?.Invoke(T("quick.page.error.pathNotFound", item), T("quick.page.error.title"));
                return;
            }
        }

        IsBusy = true;
        try
        {
            LogText = string.Empty;
            AppendLog("===============================================");
            AppendLog(T("quick.page.log.executeBanner"));
            AppendLog("===============================================");

            var request = new GuiExecuteRequest
            {
                SourcePaths = SourceItems.ToList(),
                Enable7z = Enable7z,
                EnableTarZst = EnableTarZst,
                SelfPurchase = SelfPurchase,
                SevenZOutputName = NormalizeSevenZOutputName(SevenZOutputName),
                TarZstOutputName = TarZstOutputName,
                VolumeSizeMB = VolumeSizeMB
            };

            await _app.ExecuteOneClickAsync(
                request,
                confirmation => ConfirmCleanupRequested!.Invoke(confirmation),
                AppendLog,
                percent => _ui.Run(() =>
                {
                    if (percent > 0)
                    {
                        IsProgressIndeterminate = false;
                        ProgressValue = percent;
                        SetProgressPercent(percent);
                    }
                })).ConfigureAwait(true);

            _hasCompressed = true;
            _config = _app.Config.Load();
            RecentProjects.Clear();
            foreach (var project in _config.RecentProjects)
                RecentProjects.Add(project);
            UpdateTaskStatus();
            MessageRequested?.Invoke(T("quick.page.done.compressComplete"), T("quick.page.done.title"));
        }
        catch (Exception ex)
        {
            AppendLog(T("quick.page.log.fatalError", ex.Message));
            MessageRequested?.Invoke(T("quick.page.error.compressFailed", ex.Message), T("quick.page.error.title"));
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            SetProgressReady();
        }
    }

    private bool CanRefresh() => !IsBusy && !IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            await Task.Yield();
            RefreshCore();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void RefreshCore()
    {
        UpdateAdSampleCount();
        UpdateTaskStatus();
        UpdateTotalSize();
        UpdateSourceListState();
    }

    private void UpdateSourceListState()
    {
        OnPropertyChanged(nameof(IsSourceListEmpty));
        OnPropertyChanged(nameof(QuickSourceCountText));
    }

    [RelayCommand]
    private void OpenAdSamplesFolder()
    {
        try
        {
            var dir = _app.Paths.ResolveAdSamplesRoot();
            Directory.CreateDirectory(dir);
            AppendLog(T("quick.page.log.samplesOpen", dir));

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendLog(T("quick.page.log.samplesOpenError", ex.Message));
            MessageRequested?.Invoke(T("quick.page.error.openSamplesFolder", ex.Message), T("quick.page.error.title"));
        }
    }

    [RelayCommand]
    private async Task SyncAdManifestAsync()
    {
        try
        {
            IsBusy = true;
            SetProgressScanningSamples();
            var stats = await Task.Run(() => _app.Manifest.SyncFromFolder(AutoPruneLargeSamples, onLog: AppendLog));
            _ui.Run(() =>
            {
                UpdateAdSampleCount();
                AppendLog(T("quick.page.log.samplesScanDone", stats.Added, stats.Updated, stats.Pruned));
            });
        }
        catch (Exception ex)
        {
            AppendLog(T("quick.page.log.samplesError", ex.Message));
            MessageRequested?.Invoke(ex.Message, T("quick.page.error.scanSamplesFailed"));
        }
        finally
        {
            _ui.Run(() =>
            {
                IsBusy = false;
                SetProgressReady();
            });
        }
    }

    public async Task HandleDroppedPathsAsync(IReadOnlyList<string> paths)
        => await AddSourcePathsAsync(paths);

    public async Task HandleDroppedAdSamplesAsync(IReadOnlyList<string> paths)
    {
        try
        {
            var stats = await Task.Run(() => _app.Manifest.ImportPaths(paths, AutoPruneLargeSamples, onLog: AppendLog));
            _ui.Run(() =>
            {
                UpdateAdSampleCount();
                AppendLog(T("quick.page.log.samplesImportDone", stats.Added, stats.Updated, stats.Skipped, stats.Pruned));
                _ = ShowAdDropFeedbackAsync(stats.Added + stats.Updated);
            });
        }
        catch (Exception ex)
        {
            AppendLog(T("quick.page.log.samplesError", ex.Message));
            MessageRequested?.Invoke(ex.Message, T("quick.page.error.importSamplesFailed"));
        }
    }

    private async Task AddSourcePathsAsync(IReadOnlyList<string> paths)
    {
        if (_hasCompressed)
        {
            SourceItems.Clear();
            _hasCompressed = false;
            AppendLog(T("quick.page.log.clearedList"));
        }

        var addedCount = 0;
        foreach (var path in paths)
        {
            if (SourceItems.Contains(path))
                continue;

            SourceItems.Add(path);
            addedCount++;
            if (Directory.Exists(path))
            {
                IsProgressIndeterminate = true;
                SetProgressScanning();
                AppendLog(T("quick.page.log.queuePreview", path));
                _ = _app.Pipeline.StartPreviewScanAsync(path, _config).ContinueWith(t =>
                {
                    _ui.Run(() =>
                    {
                        if (t.IsFaulted)
                            AppendLog(T("quick.page.log.scanFailed", path));
                        else
                            AppendLog(T("quick.page.log.scanDone", path));
                        IsProgressIndeterminate = false;
                        SetProgressReady();
                        UpdateTaskStatus();
                    });
                }, TaskScheduler.Default);
            }
            else
            {
                AppendLog(T("quick.page.log.fileAdded", path));
            }
        }

        UpdateTotalSize();
        UpdateTaskStatus();
        UpdateSourceListState();
        if (addedCount > 0)
            _ = ShowSourceDropFeedbackAsync(addedCount);
        await Task.CompletedTask;
    }

    private async Task ShowSourceDropFeedbackAsync(int count)
    {
        QuickDropFeedbackText = string.Format(T("quick.page.dropAdded"), count);
        _sourceDropFeedbackCts?.Cancel();
        _sourceDropFeedbackCts?.Dispose();
        _sourceDropFeedbackCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(2500, _sourceDropFeedbackCts.Token);
            QuickDropFeedbackText = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ShowAdDropFeedbackAsync(int importedCount)
    {
        if (importedCount <= 0)
            return;

        QuickAdDropFeedbackText = string.Format(T("quick.page.adDropAdded"), importedCount);
        _adDropFeedbackCts?.Cancel();
        _adDropFeedbackCts?.Dispose();
        _adDropFeedbackCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(2500, _adDropFeedbackCts.Token);
            QuickAdDropFeedbackText = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateTaskStatus()
    {
        if (_app.Pipeline.Tasks.Count == 0)
        {
            TaskStatus = SourceItems.Count == 0
                ? T("quick.page.taskStatus.initial")
                : T("quick.page.taskStatus.empty");
            return;
        }

        var lines = _app.Pipeline.Tasks.Values.Select(t =>
            T("quick.page.taskStatus.line", TaskStateLabel(t.State), t.Matched, Path.GetFileName(t.Path)));
        TaskStatus = string.Join(Environment.NewLine, lines);
    }

    private void UpdateTotalSize()
    {
        long pc = 0, apk = 0;
        foreach (var path in SourceItems)
        {
            if (Directory.Exists(path))
                pc += CompressService.GetFolderSize(path);
            else if (File.Exists(path))
            {
                var size = new FileInfo(path).Length;
                if (path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    apk += size;
                else
                    pc += size;
            }
        }

        TotalSizeText = apk > 0 && pc > 0
            ? T("quick.page.totalSize.both", CompressService.FormatFileSize(pc), CompressService.FormatFileSize(apk))
            : apk > 0
                ? T("quick.page.totalSize.androidOnly", CompressService.FormatFileSize(apk))
                : pc > 0
                    ? T("quick.page.totalSize.pcOnly", CompressService.FormatFileSize(pc))
                    : T("quick.page.totalSize.zero");
    }

    private void UpdateAdSampleCount()
    {
        var (files, folders) = _app.GetAdSampleCounts();
        AdSampleCountText = T("quick.page.adSampleCount", files, folders);
    }

    private void InitializeToolStatus()
    {
        AppendLog("===============================================");
        AppendLog($"    {AppVersion.DisplayName}");
        AppendLog("===============================================");
        AppendLog(string.Empty);
        AppendLog(T("quick.page.log.checkingTools"));

        var sevenZip = _app.SevenZip.FindSevenZip();
        if (sevenZip is not null)
        {
            AppendLog(T("quick.page.log.sevenZipFound", sevenZip));
            Enable7zOption = true;
        }
        else
        {
            AppendLog(T("quick.page.log.sevenZipNotFound"));
            Enable7zOption = false;
            Enable7z = false;
        }

        if (sevenZip is not null && _app.SevenZip.TestTarZstdAvailable(sevenZip))
        {
            AppendLog(T("quick.page.log.tarZstSupported"));
            EnableTarOption = true;
        }
        else
        {
            AppendLog(T("quick.page.log.tarZstUnsupported"));
            EnableTarOption = false;
            EnableTarZst = false;
        }

        if (_app.Manifest.TestManifestStale(_config.ManifestStaleDays))
            AppendLog(T("quick.page.log.manifestStale"));

        if (RecentProjects.Count > 0)
            AppendLog(T("quick.page.log.recentProject", RecentProjects[0]));

        AppendLog(string.Empty);
        AppendLog(T("quick.page.log.readyHint"));
        SetProgressReady();
    }

    private void SetProgressReady()
    {
        _progressMode = ProgressDisplayMode.Ready;
        ProgressText = T("quick.page.progress.ready");
    }

    private void SetProgressScanning()
    {
        _progressMode = ProgressDisplayMode.Scanning;
        ProgressText = T("quick.page.progress.scanning");
    }

    private void SetProgressScanningSamples()
    {
        _progressMode = ProgressDisplayMode.ScanningSamples;
        ProgressText = T("quick.page.progress.scanningSamples");
    }

    private void SetProgressPercent(double percent)
    {
        _progressMode = ProgressDisplayMode.Percent;
        ProgressText = T("quick.page.progress.percent", (int)percent);
    }

    private void RefreshProgressText()
    {
        ProgressText = _progressMode switch
        {
            ProgressDisplayMode.Scanning => T("quick.page.progress.scanning"),
            ProgressDisplayMode.ScanningSamples => T("quick.page.progress.scanningSamples"),
            ProgressDisplayMode.Percent => T("quick.page.progress.percent", (int)ProgressValue),
            _ => T("quick.page.progress.ready")
        };
    }

    private static string TaskStateLabel(PipelineTaskState state) => state switch
    {
        PipelineTaskState.PendingScan => LocalizationService.T("quick.page.taskState.pendingScan"),
        PipelineTaskState.PendingConfirm => LocalizationService.T("quick.page.taskState.pendingConfirm"),
        PipelineTaskState.NoAds => LocalizationService.T("quick.page.taskState.noAds"),
        PipelineTaskState.Cleaned => LocalizationService.T("quick.page.taskState.cleaned"),
        PipelineTaskState.Compressed => LocalizationService.T("quick.page.taskState.compressed"),
        _ => state.ToString()
    };

    private static string NormalizeSevenZOutputName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        if (name == "默认使用文件夹名称"
            || name == LocalizationService.T("quick.page.sevenZOutputPlaceholder"))
            return string.Empty;

        return name.Trim();
    }

    private static string T(string key) => LocalizationService.T(key);

    private static string T(string key, params object[] args) => string.Format(LocalizationService.T(key), args);

    private void AppendLog(string message)
    {
        _ui.Run(() =>
        {
            var line = message.EndsWith(Environment.NewLine, StringComparison.Ordinal) ? message : message + Environment.NewLine;
            LogText += line;
        });
    }

    private void InitializePicker(object picker)
    {
        var hwnd = _getWindowHandle?.Invoke() ?? 0;
        if (hwnd == 0)
            return;

        switch (picker)
        {
            case Windows.Storage.Pickers.FileOpenPicker openPicker:
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                break;
            case Windows.Storage.Pickers.FolderPicker folderPicker:
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                break;
            case Windows.Storage.Pickers.FileSavePicker savePicker:
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                break;
        }
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(QuickPageTitle));
        OnPropertyChanged(nameof(QuickSimpleHint));
        OnPropertyChanged(nameof(QuickHeroTitle));
        OnPropertyChanged(nameof(QuickHeroFlow));
        OnPropertyChanged(nameof(QuickRecentProjectsLabel));
        OnPropertyChanged(nameof(QuickSettingsLabel));
        OnPropertyChanged(nameof(QuickSourceListHeader));
        OnPropertyChanged(nameof(QuickAddFolderLabel));
        OnPropertyChanged(nameof(QuickAddFileLabel));
        OnPropertyChanged(nameof(QuickRemoveSelectedLabel));
        OnPropertyChanged(nameof(QuickSelfPurchaseLabel));
        OnPropertyChanged(nameof(QuickSevenZTitle));
        OnPropertyChanged(nameof(QuickEnableCompressionLabel));
        OnPropertyChanged(nameof(QuickOutputNameLabel));
        OnPropertyChanged(nameof(QuickSevenZOutputPlaceholder));
        OnPropertyChanged(nameof(QuickVolumeSizeMbLabel));
        OnPropertyChanged(nameof(QuickVolumeSizeHint));
        OnPropertyChanged(nameof(QuickTarZstTitle));
        OnPropertyChanged(nameof(QuickAdSamplesTitle));
        OnPropertyChanged(nameof(QuickAdDropHint));
        OnPropertyChanged(nameof(QuickBrowseSamplesLabel));
        OnPropertyChanged(nameof(QuickSyncManifestLabel));
        OnPropertyChanged(nameof(QuickPruneLargeSamplesLabel));
        OnPropertyChanged(nameof(QuickPreviewCleanupLabel));
        OnPropertyChanged(nameof(QuickExportLogLabel));
        OnPropertyChanged(nameof(QuickExecuteLabel));
        OnPropertyChanged(nameof(QuickSourceEmptyHint));
        OnPropertyChanged(nameof(QuickRefreshingHint));
        OnPropertyChanged(nameof(QuickSourceCountLoadingHint));

        UpdateTaskStatus();
        UpdateTotalSize();
        UpdateAdSampleCount();
        UpdateSourceListState();
        RefreshProgressText();
    }

    public event Action<string, string>? MessageRequested;
    public event Action<string>? PreviewRequested;
    public event Func<CleanupConfirmation, Task<bool>>? ConfirmCleanupRequested;
}