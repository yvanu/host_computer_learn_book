using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace HostComputer.IndustrialMonitor;

/// <summary>
/// Single SQLite writer for samples; infrequent settings/alarm/audit operations use
/// parameterized, short-lived connections. The UI never performs synchronous SQL.
/// </summary>
public sealed class SqliteHistoryStore : IAsyncDisposable
{
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HostComputerLearn", "monitor_v05.sqlite");

    private readonly string _path;
    private readonly Channel<DeviceReading> _queue =
        Channel.CreateBounded<DeviceReading>(new BoundedChannelOptions(240)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _writer;

    public SqliteHistoryStore(string? databasePath = null)
    {
        _path = databasePath ?? DatabasePath;
        _writer = Task.Run(WriteLoopAsync);
    }

    private SqliteConnection Open(bool readOnly = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5,
            Pooling = false
        };
        var db = new SqliteConnection(builder.ToString());
        db.Open();
        return db;
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var db = Open();
            using (var schema = db.CreateCommand())
            {
                schema.CommandText = """
                    PRAGMA journal_mode=WAL;
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
                    CREATE TABLE IF NOT EXISTS device_settings (
                      device_id TEXT PRIMARY KEY,
                      threshold_c REAL NOT NULL,
                      changed_at_utc TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS alarm_events (
                      id INTEGER PRIMARY KEY,
                      device_id TEXT NOT NULL,
                      raised_at_utc TEXT NOT NULL,
                      temperature_c REAL NOT NULL,
                      threshold_c REAL NOT NULL,
                      acknowledged_at_utc TEXT,
                      ended_at_utc TEXT,
                      end_reason TEXT
                    );
                    CREATE INDEX IF NOT EXISTS ix_alarm_device
                      ON alarm_events(device_id, id DESC);
                    CREATE TABLE IF NOT EXISTS audit_events (
                      id INTEGER PRIMARY KEY,
                      device_id TEXT NOT NULL,
                      recorded_at_utc TEXT NOT NULL,
                      event_kind TEXT NOT NULL,
                      detail TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_audit_device
                      ON audit_events(device_id, id DESC);
                    """;
                schema.ExecuteNonQuery();
            }
            _ready.TrySetResult();

            await foreach (var reading in _queue.Reader.ReadAllAsync())
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO readings
                      (device_id, captured_at_utc, temperature_c, pressure_kpa, rpm, alarm)
                    VALUES ($device, $time, $temp, $pressure, $rpm, $alarm);
                    """;
                cmd.Parameters.AddWithValue("$device", reading.DeviceId);
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
            _ready.TrySetException(ex);
            _queue.Writer.TryComplete(ex);
            throw;
        }
    }

    public async Task AddAsync(DeviceReading reading, CancellationToken token)
    {
        await _ready.Task.WaitAsync(token);
        if (_writer.IsFaulted) await _writer;
        await _queue.Writer.WriteAsync(reading, token);
    }

    public async Task<Dictionary<string, double>> LoadThresholdsAsync()
    {
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = Open(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT device_id, threshold_c FROM device_settings";
            using var rows = cmd.ExecuteReader();
            var settings = new Dictionary<string, double>();
            while (rows.Read()) settings[rows.GetString(0)] = rows.GetDouble(1);
            return settings;
        });
    }

    public async Task SaveThresholdAsync(string deviceId, double threshold)
    {
        if (!double.IsFinite(threshold) || threshold < 20 || threshold > 80)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO device_settings(device_id, threshold_c, changed_at_utc)
                    VALUES($d,$t,$now)
                    ON CONFLICT(device_id) DO UPDATE SET
                    threshold_c=excluded.threshold_c, changed_at_utc=excluded.changed_at_utc;
                    """;
                cmd.Parameters.AddWithValue("$d", deviceId);
                cmd.Parameters.AddWithValue("$t", threshold);
                cmd.Parameters.AddWithValue("$now", Now());
                cmd.ExecuteNonQuery();
            }
            InsertAudit(db, tx, deviceId, "threshold_changed",
                $"Temperature threshold changed to {threshold.ToString("F1", CultureInfo.InvariantCulture)} C");
            tx.Commit();
        });
    }

    public async Task RecordTransitionAsync(DeviceReading reading, double threshold, bool isAlarm)
    {
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            if (isAlarm)
            {
                // A prior crash/disconnect may have left an open event.
                CloseOpen(db, tx, reading.DeviceId, "interrupted");
                using var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO alarm_events
                    (device_id,raised_at_utc,temperature_c,threshold_c)
                    VALUES($d,$at,$t,$limit);
                    """;
                cmd.Parameters.AddWithValue("$d", reading.DeviceId);
                cmd.Parameters.AddWithValue("$at", reading.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$t", reading.TemperatureC);
                cmd.Parameters.AddWithValue("$limit", threshold);
                cmd.ExecuteNonQuery();
            }
            else
            {
                CloseOpen(db, tx, reading.DeviceId, "recovered");
            }
            InsertAudit(db, tx, reading.DeviceId, isAlarm ? "alarm_raised" : "alarm_recovered",
                $"temp={reading.TemperatureC.ToString("F1", CultureInfo.InvariantCulture)} C, threshold={threshold.ToString("F1", CultureInfo.InvariantCulture)} C");
            tx.Commit();
        });
    }

    private static void CloseOpen(SqliteConnection db, SqliteTransaction tx,
        string deviceId, string reason)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE alarm_events SET ended_at_utc=$now, end_reason=$reason
            WHERE device_id=$d AND ended_at_utc IS NULL;
            """;
        cmd.Parameters.AddWithValue("$now", Now());
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$d", deviceId);
        cmd.ExecuteNonQuery();
    }

    public async Task CloseOnDisconnectAsync(string deviceId)
    {
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            CloseOpen(db, tx, deviceId, "offline"); // never falsely label as recovered
            InsertAudit(db, tx, deviceId, "disconnected", "Polling session ended; status unknown");
            tx.Commit();
        });
    }

    public async Task<bool> AcknowledgeLatestAsync(string deviceId)
    {
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE alarm_events SET acknowledged_at_utc=$now
                WHERE id = (
                  SELECT id FROM alarm_events WHERE device_id=$d
                  AND acknowledged_at_utc IS NULL ORDER BY id DESC LIMIT 1
                );
                """;
            cmd.Parameters.AddWithValue("$now", Now());
            cmd.Parameters.AddWithValue("$d", deviceId);
            bool changed = cmd.ExecuteNonQuery() == 1;
            if (changed) InsertAudit(db, tx, deviceId, "alarm_acknowledged",
                "Operator acknowledged latest unacknowledged alarm (demo user)");
            tx.Commit();
            return changed;
        });
    }

    public async Task WriteAuditAsync(string deviceId, string kind, string detail)
    {
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = Open();
            InsertAudit(db, null, deviceId, kind, detail);
        });
    }

    private static void InsertAudit(SqliteConnection db, SqliteTransaction? tx,
        string deviceId, string kind, string detail)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO audit_events(device_id,recorded_at_utc,event_kind,detail)
            VALUES($d,$now,$kind,$detail);
            """;
        cmd.Parameters.AddWithValue("$d", deviceId);
        cmd.Parameters.AddWithValue("$now", Now());
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$detail", detail);
        cmd.ExecuteNonQuery();
    }

    public async Task<List<string>> QueryAuditAsync(string deviceId, int count = 20)
    {
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = Open(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT recorded_at_utc,event_kind,detail FROM audit_events
                WHERE device_id=$device ORDER BY id DESC LIMIT $count;
                """;
            cmd.Parameters.AddWithValue("$device", deviceId);
            cmd.Parameters.AddWithValue("$count", count);
            using var rows = cmd.ExecuteReader();
            var items = new List<string>();
            while (rows.Read()) items.Add(
                $"{rows.GetString(0)} | {rows.GetString(1)} | {rows.GetString(2)}");
            return items;
        });
    }

    // Keyset pagination: stable under new INSERTs, unlike OFFSET pages.
    public async Task<(List<HistoryRow> Rows, bool More)> QueryAsync(
        string deviceId, long? beforeId, int pageSize = 20)
    {
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = Open(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id,captured_at_utc,temperature_c,pressure_kpa,rpm,alarm
                FROM readings WHERE device_id=$device
                AND ($cursor IS NULL OR id < $cursor)
                ORDER BY id DESC LIMIT $count;
                """;
            cmd.Parameters.AddWithValue("$device", deviceId);
            cmd.Parameters.AddWithValue("$cursor", (object?)beforeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$count", pageSize + 1);
            using var rows = cmd.ExecuteReader();
            var result = new List<HistoryRow>();
            while (rows.Read())
                result.Add(new HistoryRow(
                    rows.GetInt64(0),
                    DateTimeOffset.Parse(rows.GetString(1), CultureInfo.InvariantCulture)
                        .ToLocalTime().ToString("MM-dd HH:mm:ss"),
                    rows.GetDouble(2).ToString("F1", CultureInfo.InvariantCulture),
                    rows.GetDouble(3).ToString("F1", CultureInfo.InvariantCulture),
                    rows.GetInt32(4).ToString(CultureInfo.InvariantCulture),
                    rows.GetInt32(5) == 1 ? "报警" : "正常"));
            return (result.Take(pageSize).ToList(), result.Count > pageSize);
        });
    }

    public async Task<int> CleanupSamplesAsync(DateTimeOffset before, int maxRows = 10_000)
    {
        if (maxRows is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(maxRows));
        await _ready.Task;
        return await Task.Run(() =>
        {
            using var db = Open();
            int removed = 0;
            while (removed < maxRows)
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    DELETE FROM readings WHERE id IN (
                      SELECT id FROM readings
                      WHERE captured_at_utc < $cutoff ORDER BY id LIMIT $limit
                    );
                    """;
                cmd.Parameters.AddWithValue("$cutoff", before.ToString("O", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$limit", Math.Min(500, maxRows - removed));
                int count = cmd.ExecuteNonQuery();
                removed += count;
                if (count == 0) break;
            }
            InsertAudit(db, null, "_system", "retention_cleanup",
                $"Removed {removed} samples older than {before:O}; alarm/audit tables kept");
            return removed;
        });
    }

    public async Task ExportCsvAsync(string deviceId, string destination)
    {
        await _ready.Task;
        await Task.Run(() =>
        {
            using var db = Open(readOnly: true);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT captured_at_utc,temperature_c,pressure_kpa,rpm,alarm
                FROM readings WHERE device_id=$d ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$d", deviceId);
            using var rows = cmd.ExecuteReader();
            using var output = new StreamWriter(destination, false, new UTF8Encoding(true));
            output.WriteLine("time_utc,temperature_c,pressure_kpa,rpm,is_alarm");
            while (rows.Read())
                output.WriteLine(string.Join(",",
                    rows.GetString(0),
                    rows.GetDouble(1).ToString(CultureInfo.InvariantCulture),
                    rows.GetDouble(2).ToString(CultureInfo.InvariantCulture),
                    rows.GetInt32(3).ToString(CultureInfo.InvariantCulture),
                    rows.GetInt32(4).ToString(CultureInfo.InvariantCulture)));
        });
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _writer; // drain pending telemetry before exit
    }
}
