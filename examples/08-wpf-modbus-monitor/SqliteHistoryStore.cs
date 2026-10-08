using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// A bounded writer queue keeps SQLite disk I/O off the WPF UI thread.
/// One background writer, no ORM, no unbounded memory growth.
/// </summary>
public sealed class SqliteHistoryStore : IAsyncDisposable
{
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HostComputerLearn", "monitor.sqlite");

    private readonly Channel<DeviceReading> _queue =
        Channel.CreateBounded<DeviceReading>(new BoundedChannelOptions(120)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

    private readonly Task _worker;

    public SqliteHistoryStore()
    {
        _worker = Task.Run(WriteLoopAsync);
    }

    public async Task AddAsync(DeviceReading reading, CancellationToken token)
    {
        if (_worker.IsFaulted) await _worker; // propagate a failed SQLite writer
        await _queue.Writer.WriteAsync(reading, token);
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            using var db = new SqliteConnection($"Data Source={DatabasePath}");
            db.Open();
            using (var init = db.CreateCommand())
            {
                init.CommandText = """
                    CREATE TABLE IF NOT EXISTS readings (
                      id INTEGER PRIMARY KEY AUTOINCREMENT,
                      captured_at_utc TEXT NOT NULL,
                      temperature_c REAL NOT NULL,
                      pressure_kpa REAL NOT NULL,
                      rpm INTEGER NOT NULL,
                      alarm INTEGER NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_readings_time
                      ON readings(captured_at_utc);
                    """;
                init.ExecuteNonQuery();
            }

            await foreach (var reading in _queue.Reader.ReadAllAsync())
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO readings
                      (captured_at_utc, temperature_c, pressure_kpa, rpm, alarm)
                    VALUES ($time, $temp, $pressure, $rpm, $alarm);
                    """;
                cmd.Parameters.AddWithValue("$time", reading.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$temp", reading.TemperatureC);
                cmd.Parameters.AddWithValue("$pressure", reading.PressureKpa);
                cmd.Parameters.AddWithValue("$rpm", reading.Rpm);
                cmd.Parameters.AddWithValue("$alarm", reading.IsHighTemperature ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _queue.Writer.TryComplete(ex); // unblock writers on disk failure
            throw;
        }
    }

    public static Task ExportCsvAsync(string destination) => Task.Run(() =>
    {
        using var db = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadOnly");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT captured_at_utc, temperature_c, pressure_kpa, rpm, alarm
            FROM readings ORDER BY id;
            """;
        using var rows = cmd.ExecuteReader();
        using var output = new StreamWriter(destination, false, new UTF8Encoding(true));
        output.WriteLine("time_utc,temperature_c,pressure_kpa,rpm,is_alarm");
        while (rows.Read())
        {
            // All columns are ISO timestamp / numeric data, no arbitrary text.
            output.WriteLine(string.Join(",",
                rows.GetString(0),
                rows.GetDouble(1).ToString(CultureInfo.InvariantCulture),
                rows.GetDouble(2).ToString(CultureInfo.InvariantCulture),
                rows.GetInt32(3).ToString(CultureInfo.InvariantCulture),
                rows.GetInt32(4).ToString(CultureInfo.InvariantCulture)));
        }
    });

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _worker; // drain pending samples so closing the window doesn't lose last values
    }
}
