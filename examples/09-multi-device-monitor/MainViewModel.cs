using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;

namespace HostComputer.IndustrialMonitor;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly SqliteHistoryStore _history = new();
    private readonly Dictionary<string, DeviceSession> _sessions;
    private DevicePanel? _selectedDevice;
    private string _thresholdText = "32.0";
    private string _pageStatus = "选择设备后点击“查询历史”";
    private int _offset;
    private bool _more;
    private bool _querying;
    private bool _exporting;

    public ObservableCollection<DevicePanel> Devices { get; } =
    [
        new DevicePanel("mock-1", "模拟设备 01", 1502),
        new DevicePanel("mock-2", "模拟设备 02", 1503)
    ];
    public ObservableCollection<string> Events { get; } = new();
    public ObservableCollection<HistoryRow> History { get; } = new();

    public DevicePanel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(_selectedDevice, value)) return;
            _selectedDevice = value;
            Notify();
            _thresholdText = value?.Threshold.ToString("F1", CultureInfo.InvariantCulture) ?? "32.0";
            Notify(nameof(ThresholdText));
            _offset = 0;
            _more = false;
            History.Clear();
            PageStatus = "已切换设备。点击“查询历史”查看该设备数据。";
            RefreshCommands();
        }
    }

    public string ThresholdText
    {
        get => _thresholdText;
        set { _thresholdText = value; Notify(); }
    }
    public string PageStatus { get => _pageStatus; private set { _pageStatus = value; Notify(); } }
    public string StorageLocation => SqliteHistoryStore.DatabasePath;

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand SaveThresholdCommand { get; }
    public RelayCommand QueryHistoryCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand ExportCommand { get; }

    public MainViewModel()
    {
        _sessions = Devices.ToDictionary(d => d.Id,
            d => new DeviceSession(d, _history, OnReading, OnLog, RefreshCommands));
        ConnectCommand = new RelayCommand(
            () => { if (SelectedDevice is { } d) _sessions[d.Id].Start(); },
            () => SelectedDevice is { } d && !_sessions[d.Id].IsRunning);
        DisconnectCommand = new RelayCommand(
            () => { if (SelectedDevice is { } d) _sessions[d.Id].Stop(); },
            () => SelectedDevice is { } d && _sessions[d.Id].IsRunning);
        SaveThresholdCommand = new RelayCommand(SaveThreshold, () => SelectedDevice is not null);
        QueryHistoryCommand = new RelayCommand(() => _ = LoadHistoryAsync(), () => !_querying);
        PreviousPageCommand = new RelayCommand(() => _ = ChangePageAsync(-1), () => !_querying && _offset > 0);
        NextPageCommand = new RelayCommand(() => _ = ChangePageAsync(1), () => !_querying && _more);
        ExportCommand = new RelayCommand(() => _ = ExportAsync(), () => !_exporting && SelectedDevice is not null);
        foreach (var device in Devices)
            device.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DevicePanel.Status)) RefreshCommands();
            };
        SelectedDevice = Devices[0];
        Log("本地演示项目已启动。启动模拟器：python simulator.py --devices 2 --dynamic");
    }

    private void OnReading(DevicePanel panel, DeviceReading reading)
    {
        // Separate device snapshots are already bound; one shared UI event log.
        RefreshCommands();
    }

    private void OnLog(DevicePanel panel, string message) => Log($"[{panel.Name}] {message}");

    private void Log(string message)
    {
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (Events.Count > 40) Events.RemoveAt(Events.Count - 1);
    }

    private void SaveThreshold()
    {
        if (SelectedDevice is not { } d) return;
        if (!double.TryParse(ThresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value) || value < 20 || value > 80)
        {
            Log("报警阈值无效：请输入 20.0～80.0 之间的数字（示例：32.0）");
            return;
        }
        d.ChangeThreshold(value);
        Log($"[{d.Name}] 高温报警阈值已修改为 {value:F1} ℃，下次采集生效");
    }

    private async Task LoadHistoryAsync()
    {
        if (_querying || SelectedDevice is not { } d) return;
        _querying = true;
        RefreshCommands();
        try
        {
            var (rows, more) = await _history.QueryAsync(d.Id, _offset);
            if (!ReferenceEquals(d, SelectedDevice)) return;
            History.Clear();
            foreach (var row in rows) History.Add(row);
            _more = more;
            PageStatus = $"[{d.Name}] 第 {_offset / 20 + 1} 页，显示 {rows.Count} 条记录";
        }
        catch (Exception ex)
        {
            _more = false;
            PageStatus = "查询失败：" + ex.Message;
        }
        finally
        {
            _querying = false;
            RefreshCommands();
        }
    }

    private async Task ChangePageAsync(int delta)
    {
        if (_querying) return;
        int old = _offset;
        _offset = Math.Max(0, _offset + delta * 20);
        await LoadHistoryAsync();
        if (PageStatus.StartsWith("查询失败", StringComparison.Ordinal)) _offset = old;
        RefreshCommands();
    }

    private async Task ExportAsync()
    {
        if (_exporting || SelectedDevice is not { } d) return;
        var dialog = new SaveFileDialog
        {
            Title = "仅导出当前设备的历史采样",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{d.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;
        _exporting = true;
        RefreshCommands();
        try
        {
            await _history.ExportCsvAsync(d.Id, dialog.FileName);
            Log($"[{d.Name}] 历史 CSV 导出成功：" + dialog.FileName);
        }
        catch (Exception ex) { Log("CSV 导出失败：" + ex.Message); }
        finally { _exporting = false; RefreshCommands(); }
    }

    public async Task StopAndWaitAsync()
    {
        await Task.WhenAll(_sessions.Values.Select(s => s.StopAsync()));
        await _history.DisposeAsync();
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
