using System.Globalization;

// 第一课：把设备文本协议转换成强类型对象（没有网络依赖）。
string[] frames =
[
    "TEMP=23.5;PRESSURE=101.2;RPM=1200",
    "TEMP=24.1;PRESSURE=101.4;RPM=1500",
    "TEMP=not-a-number;PRESSURE=101.3;RPM=1200"
];

foreach (var frame in frames)
{
    if (TryParse(frame, out var reading))
        Console.WriteLine($"温度 {reading.Temperature:F1} ℃，压力 {reading.Pressure:F1} kPa，转速 {reading.Rpm} rpm");
    else
        Console.WriteLine($"[WARN] 数据不合法，已丢弃：{frame}");
}

static bool TryParse(string frame, out Reading reading)
{
    reading = default!;
    var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in frame.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var pair = item.Split('=', 2);
        if (pair.Length != 2 || !fields.TryAdd(pair[0].Trim(), pair[1].Trim()))
            return false;
    }

    if (!fields.TryGetValue("TEMP", out var tempText) ||
        !fields.TryGetValue("PRESSURE", out var pressureText) ||
        !fields.TryGetValue("RPM", out var rpmText) ||
        !double.TryParse(tempText, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp) ||
        !double.TryParse(pressureText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pressure) ||
        !int.TryParse(rpmText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rpm) ||
        !double.IsFinite(temp) || !double.IsFinite(pressure) || rpm < 0)
        return false;

    reading = new Reading(temp, pressure, rpm);
    return true;
}

public record Reading(double Temperature, double Pressure, int Rpm);
