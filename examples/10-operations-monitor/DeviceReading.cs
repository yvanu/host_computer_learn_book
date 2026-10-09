namespace HostComputer.IndustrialMonitor;

// Immutable snapshot. Alarm is computed from each device's configured threshold.
public sealed record DeviceReading(
    string DeviceId, DateTimeOffset CapturedAt, double TemperatureC,
    double PressureKpa, int Rpm, bool IsHighTemperature);

public sealed record HistoryRow(long Id, string CapturedAt, string Temperature, string Pressure,
    string Rpm, string Alarm)
{
    public string Display => $"{CapturedAt}    {Temperature} ℃ / {Pressure} kPa / {Rpm} rpm    {Alarm}";
}
