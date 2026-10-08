using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace HostComputer.IndustrialMonitor;

/// <summary>UI state and workflow only. Network framing lives in ModbusDeviceClient.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _busy;
    private bool _exporting;
    private bool _alarmActive;
    private CancellationTokenSource? _stop;
    private ModbusDeviceClient? _device;
    private Task? _running;
    private readonly Queue<double> _temperatureHistory = new();

    private string _status = "未连接";
    private string _temperature = "—";
    private string _pressure = "—";
    private string _rpm = "—";
    private string _alarmStatus = "当前无高温报警";
    private string _lastUpdated = "尚未采集";
    private string _sampleCount = "0";
    private PointCollection _temperaturePoints = new();
    private int _received;

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string Pressure { get => _pressure; private set => Set(ref _pressure, value); }
    public string Rpm { get => _rpm; private set => Set(ref _rpm, value); }
    public string AlarmStatus { get => _alarmStatus; private set => Set(ref _alarmStatus, value); }
    public string LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }
    public string SampleCount { get => _sampleCount; private set => Set(ref _sampleCount, value); }
    public PointCollection TemperaturePoints
    {
        get => _temperaturePoints;
        private set => Set(ref _temperaturePoints, value);
    }

    public string StorageLocation => SqliteHistoryStore.DatabasePath;
    public ObservableCollection<string> Events { get; } = new();

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand ExportCommand { get; }

    public MainViewModel()
    {
        ConnectCommand = new RelayCommand(Start, () => !_busy);
        DisconnectCommand = new RelayCommand(Stop, () => _busy);
        ExportCommand = new RelayCommand(() => _ = ExportAsync(), () => !_exporting);
        Log("演示环境已就绪。请先启动本地 Modbus TCP 模拟器。");
    }

    private void Start()
    {
        if (_busy) return;
        _busy = true;
        _alarmActive = false; // a new connection should report a new alarm transition
        _stop = new CancellationTokenSource();
        Status = "正在连接 127.0.0.1:1502…";
        RefreshCommands();
        _running = PollAsync(_stop.Token);
    }

    private async Task PollAsync(CancellationToken token)
    {
        var device = new ModbusDeviceClient();
        _device = device;
        SqliteHistoryStore? store = null;
        try
        {
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(4));
            await device.ConnectAsync(connectTimeout.Token);
            token.ThrowIfCancellationRequested();
            store = new SqliteHistoryStore();
            Status = "已连接 · 正在采集";
            Log("设备连接成功。FC03 每秒读取 3 个保持寄存器。");

            while (true)
            {
                var reading = await device.ReadAsync(token);
                // SQLite insertions are consumed on a bounded background queue.
                await store.AddAsync(reading, token);
                ShowReading(reading);
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Status = "已断开";
            Log("采集已手动停止。");
        }
        catch (OperationCanceledException)
        {
            Status = "采集超时";
            Log("请求超时：检查模拟器是否在线或回复了完整 Modbus 帧。");
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
            {
                Status = "已断开";
                Log("采集已停止。");
            }
            else
            {
                Status = "连接或采集异常";
                Log("连接异常：" + ex.Message);
            }
        }
        finally
        {
            device.Dispose();
            if (store is not null)
            {
                try { await store.DisposeAsync(); }
                catch (Exception ex) { Log("数据存储失败：" + ex.Message); }
            }
            _device = null;
            _busy = false;
            _stop?.Dispose();
            _stop = null;
            RefreshCommands();
        }
    }

    private void ShowReading(DeviceReading reading)
    {
        Temperature = reading.TemperatureC.ToString("F1");
        Pressure = reading.PressureKpa.ToString("F1");
        Rpm = reading.Rpm.ToString();
        LastUpdated = "最后采集：" + reading.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        SampleCount = (++_received).ToString();

        _temperatureHistory.Enqueue(reading.TemperatureC);
        if (_temperatureHistory.Count > 60) _temperatureHistory.Dequeue();

        // WPF Polyline uses fixed logical canvas coordinates (900 x 160).
        var points = new PointCollection();
        int index = 0;
        foreach (double value in _temperatureHistory)
        {
            double x = index++ * 900.0 / 59.0;
            double y = 152.0 - Math.Clamp((value - 20.0) / 20.0, 0, 1) * 140.0;
            points.Add(new Point(x, y));
        }
        TemperaturePoints = points;

        if (reading.IsHighTemperature && !_alarmActive)
        {
            _alarmActive = true;
            Log($"⚠ 高温告警：温度 {reading.TemperatureC:F1} ℃ ≥ 32.0 ℃");
        }
        else if (!reading.IsHighTemperature && _alarmActive)
        {
            _alarmActive = false;
            Log($"✓ 高温已恢复：温度 {reading.TemperatureC:F1} ℃");
        }
        AlarmStatus = reading.IsHighTemperature
            ? $"高温告警 · {reading.TemperatureC:F1} ℃（阈值 32.0 ℃）"
            : "当前无高温报警（阈值 32.0 ℃）";
    }

    private void Log(string text)
    {
        Events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        while (Events.Count > 30) Events.RemoveAt(Events.Count - 1);
    }

    private void Stop()
    {
        _stop?.Cancel();
        _device?.Dispose(); // ensure an in-progress read exits quickly
    }

    public async Task StopAndWaitAsync()
    {
        Stop();
        var active = _running;
        if (active is not null)
            await active; // ensures background SQLite writer has drained
    }

    private async Task ExportAsync()
    {
        if (_exporting) return;
        if (!File.Exists(StorageLocation))
        {
            Log("没有历史数据库：先连接模拟设备采集几条数据。");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "导出上位机采集历史",
            Filter = "CSV 表格 (*.csv)|*.csv",
            FileName = $"industrial-readings-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;

        _exporting = true;
        RefreshCommands();
        try
        {
            await SqliteHistoryStore.ExportCsvAsync(dialog.FileName);
            Log("历史数据已导出：" + dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                 Microsoft.Data.Sqlite.SqliteException)
        {
            Log("导出失败：" + ex.Message);
        }
        finally
        {
            _exporting = false;
            RefreshCommands();
        }
    }

    private void RefreshCommands()
    {
        ConnectCommand.NotifyChanged();
        DisconnectCommand.NotifyChanged();
        ExportCommand.NotifyChanged();
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RelayCommand(Action action, Func<bool> allowed) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => allowed();
    public void Execute(object? parameter) => action();
    public void NotifyChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
