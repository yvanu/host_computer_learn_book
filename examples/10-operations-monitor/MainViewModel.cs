using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace HostComputer.IndustrialMonitor;

/// <summary>UI state: no SQL, Modbus frame processing or synchronous disk I/O.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly SqliteHistoryStore _store = new();
    private readonly Dictionary<string, DeviceSession> _sessions;
    private DevicePanel? _selectedDevice;
    private string _thresholdText = "32.0";
    private string _pageStatus = "初始化本地数据库…";
    private bool _initialized, _querying, _exporting, _saving, _acknowledging, _cleaning, _auditLoading;
    private readonly List<long?> _pageCursors = [null];
    private int _pageIndex;
    private bool _hasNext;

    public ObservableCollection<DevicePanel> Devices { get; } =
    [
        new DevicePanel("mock-1", "模拟设备 01", 1502),
        new DevicePanel("mock-2", "模拟设备 02", 1503)
    ];
    public ObservableCollection<HistoryRow> History { get; } = new();
    public ObservableCollection<string> Events { get; } = new();
    public ObservableCollection<string> Audit { get; } = new();

    public DevicePanel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(value, _selectedDevice)) return;
            _selectedDevice = value;
            Notify();
            _thresholdText = value?.Threshold.ToString("F1", CultureInfo.InvariantCulture) ?? "32.0";
            Notify(nameof(ThresholdText));
            ResetHistory();
            Audit.Clear();
            PageStatus = "选择设备后点击“查询历史”（游标分页）";
            RefreshCommands();
        }
    }

    public string ThresholdText
    {
        get => _thresholdText;
        set { _thresholdText = value; Notify(); }
    }
    public string PageStatus
    {
        get => _pageStatus;
        private set { _pageStatus = value; Notify(); }
    }
    public string StorageLocation => SqliteHistoryStore.DatabasePath;
    public bool IsInitialized => _initialized;

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand SaveThresholdCommand { get; }
    public RelayCommand QueryHistoryCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand AcknowledgeCommand { get; }
    public RelayCommand RefreshAuditCommand { get; }
    public RelayCommand CleanupCommand { get; }

    public MainViewModel()
    {
        _sessions = Devices.ToDictionary(d => d.Id,
            d => new DeviceSession(d, _store, (_, _) => { }, OnLog, RefreshCommands));
        ConnectCommand = new RelayCommand(
            () => { if (SelectedDevice is { } d) _sessions[d.Id].Start(); },
            () => _initialized && SelectedDevice is { } d && !_sessions[d.Id].IsRunning);
        DisconnectCommand = new RelayCommand(
            () => { if (SelectedDevice is { } d) _sessions[d.Id].Stop(); },
            () => _initialized && SelectedDevice is { } d && _sessions[d.Id].IsRunning);
        SaveThresholdCommand = new RelayCommand(() => _ = SaveThresholdAsync(),
            () => _initialized && !_saving && SelectedDevice is not null);
        QueryHistoryCommand = new RelayCommand(() => _ = LoadHistoryAsync(),
            () => _initialized && !_querying && SelectedDevice is not null);
        PreviousPageCommand = new RelayCommand(() => _ = ChangePageAsync(-1),
            () => _initialized && !_querying && _pageIndex > 0);
        NextPageCommand = new RelayCommand(() => _ = ChangePageAsync(1),
            () => _initialized && !_querying && _hasNext);
        ExportCommand = new RelayCommand(() => _ = ExportAsync(),
            () => _initialized && !_exporting && SelectedDevice is not null);
        AcknowledgeCommand = new RelayCommand(() => _ = AcknowledgeAsync(),
            () => _initialized && !_acknowledging && SelectedDevice is not null);
        RefreshAuditCommand = new RelayCommand(() => _ = RefreshAuditAsync(),
            () => _initialized && !_auditLoading && SelectedDevice is not null);
        CleanupCommand = new RelayCommand(() => _ = CleanupAsync(),
            () => _initialized && !_cleaning);

        foreach (var device in Devices)
            device.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DevicePanel.Status)) RefreshCommands();
            };
        SelectedDevice = Devices[0];
        Log("请先启动：python simulator.py --devices 2 --dynamic");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            var settings = await _store.LoadThresholdsAsync();
            foreach (var device in Devices)
                if (settings.TryGetValue(device.Id, out double value) &&
                    double.IsFinite(value) && value >= 20 && value <= 80)
                    device.ChangeThreshold(value);

            if (SelectedDevice is { } selected)
            {
                ThresholdText = selected.Threshold.ToString("F1", CultureInfo.InvariantCulture);
            }
            _initialized = true;
            Notify(nameof(IsInitialized));
            PageStatus = "本地数据库已就绪，选择设备后查询历史";
            Log("已恢复设备报警阈值；操作审计与报警记录会持久化到 SQLite。");
        }
        catch (Exception ex)
        {
            PageStatus = "数据库初始化失败：" + ex.Message;
            Log("无法操作数据库：" + ex.Message);
        }
        finally { RefreshCommands(); }
    }

    private void OnLog(DevicePanel device, string message) => Log($"[{device.Name}] {message}");

    private void Log(string message)
    {
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (Events.Count > 40) Events.RemoveAt(Events.Count - 1);
    }

    private async Task SaveThresholdAsync()
    {
        if (_saving || SelectedDevice is not { } device) return;
        if (!double.TryParse(ThresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value) || value is < 20 or > 80)
        {
            Log("阈值无效：请填写 20.0～80.0 的数字，使用英文小数点");
            return;
        }
        _saving = true;
        RefreshCommands();
        try
        {
            await _store.SaveThresholdAsync(device.Id, value);
            device.ChangeThreshold(value);
            Log($"[{device.Name}] 报警阈值已持久化为 {value:F1} ℃，下次采样生效");
        }
        catch (Exception ex) { Log("无法保存阈值：" + ex.Message); }
        finally { _saving = false; RefreshCommands(); }
    }

    private async Task AcknowledgeAsync()
    {
        if (_acknowledging || SelectedDevice is not { } device) return;
        _acknowledging = true;
        RefreshCommands();
        try
        {
            bool changed = await _store.AcknowledgeLatestAsync(device.Id);
            Log(changed
                ? $"[{device.Name}] 已确认最近一条未确认报警（确认并不表示恢复）"
                : $"[{device.Name}] 没有待确认报警");
            await RefreshAuditCoreAsync(device);
        }
        catch (Exception ex) { Log("报警确认失败：" + ex.Message); }
        finally { _acknowledging = false; RefreshCommands(); }
    }

    private void ResetHistory()
    {
        _pageCursors.Clear();
        _pageCursors.Add(null);
        _pageIndex = 0;
        _hasNext = false;
        History.Clear();
    }

    private async Task LoadHistoryAsync()
    {
        if (_querying || SelectedDevice is not { } device) return;
        _querying = true;
        RefreshCommands();
        int requestedPage = _pageIndex;
        try
        {
            var (rows, more) = await _store.QueryAsync(device.Id, _pageCursors[requestedPage]);
            if (!ReferenceEquals(device, SelectedDevice) || requestedPage != _pageIndex)
                return;
            History.Clear();
            foreach (var row in rows) History.Add(row);
            _hasNext = more;
            PageStatus = $"[{device.Name}] 第 {requestedPage + 1} 页，共展示 {rows.Count} 条（按 ID 游标）";
        }
        catch (Exception ex) { PageStatus = "历史查询失败：" + ex.Message; }
        finally { _querying = false; RefreshCommands(); }
    }

    private async Task ChangePageAsync(int delta)
    {
        if (_querying || History.Count == 0) return;
        if (delta > 0 && !_hasNext) return;
        if (delta < 0 && _pageIndex == 0) return;
        var oldIndex = _pageIndex;
        if (delta > 0)
        {
            long cursor = History[^1].Id;
            if (_pageCursors.Count == _pageIndex + 1) _pageCursors.Add(cursor);
            else _pageCursors[_pageIndex + 1] = cursor;
            _pageIndex++;
        }
        else _pageIndex--;
        await LoadHistoryAsync();
        if (PageStatus.StartsWith("历史查询失败", StringComparison.Ordinal))
            _pageIndex = oldIndex;
        RefreshCommands();
    }

    private async Task RefreshAuditAsync()
    {
        if (_auditLoading || SelectedDevice is not { } device) return;
        _auditLoading = true;
        RefreshCommands();
        try { await RefreshAuditCoreAsync(device); }
        catch (Exception ex) { Log("审计查询失败：" + ex.Message); }
        finally { _auditLoading = false; RefreshCommands(); }
    }

    private async Task RefreshAuditCoreAsync(DevicePanel device)
    {
        var rows = await _store.QueryAuditAsync(device.Id);
        if (!ReferenceEquals(device, SelectedDevice)) return;
        Audit.Clear();
        foreach (var row in rows) Audit.Add(row);
    }

    private async Task CleanupAsync()
    {
        if (_cleaning) return;
        var answer = MessageBox.Show(
            "只清理 30 天前的采样数据，本次最多清理 10000 行。\n" +
            "报警事件和操作审计会保留；清理无法撤销，建议先导出 CSV。\n确定继续？",
            "历史数据清理（不可撤销）",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        _cleaning = true;
        RefreshCommands();
        try
        {
            int count = await _store.CleanupSamplesAsync(DateTimeOffset.UtcNow.AddDays(-30));
            Log($"历史清理完成：{count} 条旧采样已删除；审计和报警表保留。");
            ResetHistory();
            PageStatus = "历史已更新，点击“查询历史”重新加载";
        }
        catch (Exception ex) { Log("清理失败：" + ex.Message); }
        finally { _cleaning = false; RefreshCommands(); }
    }

    private async Task ExportAsync()
    {
        if (_exporting || SelectedDevice is not { } device) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出选中设备的历史采样",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{device.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;
        _exporting = true;
        RefreshCommands();
        try
        {
            await _store.ExportCsvAsync(device.Id, dialog.FileName);
            Log($"[{device.Name}] 已导出 CSV：" + dialog.FileName);
        }
        catch (Exception ex) { Log("CSV 导出失败：" + ex.Message); }
        finally { _exporting = false; RefreshCommands(); }
    }

    public async Task StopAndWaitAsync()
    {
        await Task.WhenAll(_sessions.Values.Select(s => s.StopAsync()));
        await _store.DisposeAsync();
    }

    private void RefreshCommands()
    {
        ConnectCommand?.NotifyChanged();
        DisconnectCommand?.NotifyChanged();
        SaveThresholdCommand?.NotifyChanged();
        QueryHistoryCommand?.NotifyChanged();
        PreviousPageCommand?.NotifyChanged();
        NextPageCommand?.NotifyChanged();
        ExportCommand?.NotifyChanged();
        AcknowledgeCommand?.NotifyChanged();
        RefreshAuditCommand?.NotifyChanged();
        CleanupCommand?.NotifyChanged();
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand(Action run, Func<bool> canRun) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canRun();
    public void Execute(object? parameter) => run();
    public void NotifyChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
