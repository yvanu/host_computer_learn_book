namespace HostComputer.IndustrialMonitor;

// One complete device snapshot: three holding registers read in a single FC03 request.
public sealed record DeviceReading(DateTimeOffset CapturedAt, double TemperatureC,
    double PressureKpa, int Rpm)
{
    public bool IsHighTemperature => TemperatureC >= 32.0;
}
