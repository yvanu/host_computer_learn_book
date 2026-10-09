using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace HostComputer.IndustrialMonitor;

/// <summary>A single device's observable UI state, not the network connection itself.</summary>
public sealed class DevicePanel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly Queue<double> _trend = new();
    private bool _alarmActive;
    private string _status = "未连接";
    private string _temperature = "—";
    private string _pressure = "—";
    private string _rpm = "—";
    private string _lastUpdated = "尚无采集";
    private string _alarmStatus = "当前无高温报警";
    private PointCollection _points = new();
    private int _samples;

    public string Id { get; }
    public string Name { get; }
    public int Port { get; }
    public string Endpoint => $"127.0.0.1:{Port}";
    public double Threshold { get; private set; } = 32.0;
    public bool? LastAlarmTransition { get; private set; }
    public string ThresholdLabel => $"报警阈值：{Threshold:F1} ℃";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Temperature { get => _temperature; private set => Set(ref _temperature, value); }
    public string Pressure { get => _pressure; private set => Set(ref _pressure, value); }
    public string Rpm { get => _rpm; private set => Set(ref _rpm, value); }
    public string LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }
    public string AlarmStatus { get => _alarmStatus; private set => Set(ref _alarmStatus, value); }
    public string SampleCount => _samples.ToString();
    public PointCollection TemperaturePoints { get => _points; private set => Set(ref _points, value); }
    public string Summary => $"{Name} · {Status}";

    public DevicePanel(string id, string name, int port) => (Id, Name, Port) = (id, name, port);

    public void SetStatus(string status)
    {
        Status = status;
        Notify(nameof(Summary));
    }

    public void ChangeThreshold(double threshold)
    {
        Threshold = threshold;
        Notify(nameof(ThresholdLabel));
        // Preserve _alarmActive: next actual sample produces correct transition event.
    }

    public DeviceReading Receive(DeviceReading reading, Action<string> log)
    {
        var alarm = reading.TemperatureC >= Threshold;
        reading = reading with { DeviceId = Id, IsHighTemperature = alarm };

        Temperature = reading.TemperatureC.ToString("F1");
        Pressure = reading.PressureKpa.ToString("F1");
        Rpm = reading.Rpm.ToString();
        LastUpdated = "最近采集：" + reading.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        _samples++;
        Notify(nameof(SampleCount));

        _trend.Enqueue(reading.TemperatureC);
        if (_trend.Count > 60) _trend.Dequeue();
        var points = new PointCollection();
        int i = 0;
        foreach (double temp in _trend)
            points.Add(new Point(i++ * 900.0 / 59.0,
                152 - Math.Clamp((temp - 20.0) / 20.0, 0, 1) * 140.0));
        TemperaturePoints = points;

        LastAlarmTransition = alarm != _alarmActive ? alarm : null;
        if (alarm != _alarmActive)
        {
            _alarmActive = alarm;
            log(alarm
                ? $"高温触发：{reading.TemperatureC:F1} ℃ ≥ {Threshold:F1} ℃"
                : $"高温恢复：{reading.TemperatureC:F1} ℃ < {Threshold:F1} ℃");
        }
        AlarmStatus = alarm
            ? $"高温报警：{reading.TemperatureC:F1} ℃（阈值 {Threshold:F1} ℃）"
            : $"当前正常（阈值 {Threshold:F1} ℃）";
        return reading;
    }

    public void MarkOffline()
    {
        _alarmActive = false;
        LastAlarmTransition = null;
        AlarmStatus = "设备离线，报警状态未知";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name!);
    }

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
