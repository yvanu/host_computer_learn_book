using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// One SQLite writer handles two producers; device_id keeps their histories isolated.
/// v0.4 uses a separate database so the beginner v0.3 database remains unchanged.
/// </summary>
public sealed class SqliteHistoryStore : IAsyncDisposable
{
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HostComputerLearn", "monitor_v04.sqlite");

    private readonly Channel<DeviceReading> _queue =
        Channel.CreateBounded<DeviceReading>(new BoundedChannelOptions(240)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _worker;

    public SqliteHistoryStore() => _worker = Task.Run(WriteLoopAsync);

    private static SqliteConnection OpenConnection(bool readOnly = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        };
        var db = new SqliteConnection(builder.ConnectionString);
        db.Open();
        return db;
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            using var db = OpenConnection();
            using (var schema = db.CreateCommand())
            {
                schema.CommandText = """
                    CREATE TABLE IF NOT EXISTS readings (
                      id INTEGER PRIMARY KEY,
                      device_id TEXT NOT NULL,
                      captured_at_utc TEXT NOT NULL,
                      temperature_c REAL NOT NULL,
                      pressure_kpa REAL NOT NULL,
                      rpm INTEGER NOT NULL,
                      alarm INTEGER NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_readings_device_id
                      ON readings(device_id, id DESC);
                    """;
                schema.ExecuteNonQuery();
            }
            _ready.TrySetResult();

            await foreach (var reading in _queue.Reader.ReadAllAsync())
            {
                using var command = db.CreateCommand();
                command.CommandText = """
                    INSERT INTO readings
                      (device_id, captured_at_utc, temperature_c, pressure_kpa, rpm, alarm)
                    VALUES ($device, $time, $temp, $pressure, $rpm, $alarm);
                    """;
                command.Parameters.AddWithValue("$device", reading.DeviceId);
                command.Parameters.AddWithValue("$time", reading.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$temp", reading.TemperatureC);
                command.Parameters.AddWithValue("$pressure", reading.PressureKpa);
                command.Parameters.AddWithValue("$rpm", reading.Rpm);
                command.Parameters.AddWithValue("$alarm", reading.IsHighTemperature ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            _queue.Writer.TryComplete(ex);
            throw;
        }
    }

    public async Task AddAsync(DeviceReading reading, CancellationToken token)
    {
        await _ready.Task.WaitAsync(token);
        if (_worker.IsFaulted) await _worker;
        await _queue.Writer.WriteAsync(reading, token);
    }

    public async Task<(List<HistoryRow> Rows, bool More)> QueryAsync(
        string deviceId, int offset, int pageSize = 20)
    {
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = OpenConnection(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT captured_at_utc, temperature_c, pressure_kpa, rpm, alarm
                FROM readings WHERE device_id = $device
                ORDER BY id DESC LIMIT $count OFFSET $offset;
                """;
            cmd.Parameters.AddWithValue("$device", deviceId);
            cmd.Parameters.AddWithValue("$count", pageSize + 1);
            cmd.Parameters.AddWithValue("$offset", offset);
            using var results = cmd.ExecuteReader();
            var records = new List<HistoryRow>();
            while (results.Read())
            {
                records.Add(new HistoryRow(
                    DateTimeOffset.Parse(results.GetString(0), CultureInfo.InvariantCulture)
                        .ToLocalTime().ToString("MM-dd HH:mm:ss"),
                    results.GetDouble(1).ToString("F1", CultureInfo.InvariantCulture),
                    results.GetDouble(2).ToString("F1", CultureInfo.InvariantCulture),
                    results.GetInt32(3).ToString(CultureInfo.InvariantCulture),
                    results.GetInt32(4) == 1 ? "报警" : "正常"));
            }
            return (records.Take(pageSize).ToList(), records.Count > pageSize);
        });
    }

    public async Task ExportCsvAsync(string deviceId, string destination)
    {
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = OpenConnection(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT captured_at_utc, temperature_c, pressure_kpa, rpm, alarm
                FROM readings WHERE device_id = $device ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$device", deviceId);
            using var rows = cmd.ExecuteReader();
            using var output = new StreamWriter(destination, false, new UTF8Encoding(true));
            output.WriteLine("time_utc,temperature_c,pressure_kpa,rpm,is_alarm");
            while (rows.Read())
            {
                output.WriteLine(string.Join(",",
                    rows.GetString(0),
                    rows.GetDouble(1).ToString(CultureInfo.InvariantCulture),
                    rows.GetDouble(2).ToString(CultureInfo.InvariantCulture),
                    rows.GetInt32(3).ToString(CultureInfo.InvariantCulture),
                    rows.GetInt32(4).ToString(CultureInfo.InvariantCulture)));
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _worker;
    }
}
