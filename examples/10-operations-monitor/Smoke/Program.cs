using HostComputer.IndustrialMonitor;

var tempDir = Path.Combine(Path.GetTempPath(), "host-computer-v05-" + Guid.NewGuid().ToString("N"));
var dbPath = Path.Combine(tempDir, "smoke.sqlite");
Directory.CreateDirectory(tempDir);

static void Expect(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("SQLite smoke failed: " + message);
}

try
{
    var today = DateTimeOffset.UtcNow;
    await using (var store = new SqliteHistoryStore(dbPath))
    {
        await store.SaveThresholdAsync("mock-1", 29.5);
        var thresholds = await store.LoadThresholdsAsync();
        Expect(thresholds.TryGetValue("mock-1", out var threshold) && threshold == 29.5,
            "threshold saved");
        await store.WriteAuditAsync("mock-1", "connected", "test");
        for (int i = 0; i < 24; i++)
        {
            await store.AddAsync(new DeviceReading("mock-1", today.AddMinutes(i),
                29.5 + i / 10.0, 101.2, 1200, i > 18), CancellationToken.None);
        }
        await store.AddAsync(new DeviceReading("mock-2", today, 26, 101, 1300, false),
            CancellationToken.None);
        await store.AddAsync(new DeviceReading("mock-1", today.AddDays(-45), 20, 100, 900, false),
            CancellationToken.None);
        var alarm = new DeviceReading("mock-1", today, 32.5, 101.2, 1200, true);
        await store.RecordTransitionAsync(alarm, 29.5, true);
        Expect(await store.AcknowledgeLatestAsync("mock-1"), "acknowledge alarm");
        Expect(!await store.AcknowledgeLatestAsync("mock-1"), "duplicate ack suppressed");
        await store.RecordTransitionAsync(alarm with { TemperatureC = 28.0, IsHighTemperature = false },
            29.5, false);
    }

    await using (var reopened = new SqliteHistoryStore(dbPath))
    {
        var persisted = await reopened.LoadThresholdsAsync();
        Expect(persisted["mock-1"] == 29.5, "settings survive restart");
        var (first, hasNext) = await reopened.QueryAsync("mock-1", null);
        Expect(first.Count == 20 && hasNext, "first keyset page");
        var (second, noMore) = await reopened.QueryAsync("mock-1", first[^1].Id);
        Expect(second.Count == 5 && !noMore, "second keyset page");
        var (other, _) = await reopened.QueryAsync("mock-2", null);
        Expect(other.Count == 1, "history isolation");
        Expect(first[^1].Id > second[0].Id, "no duplicate rows between pages");

        var audit = await reopened.QueryAuditAsync("mock-1");
        Expect(audit.Any(x => x.Contains("threshold_changed")) &&
               audit.Any(x => x.Contains("alarm_acknowledged")) &&
               audit.Any(x => x.Contains("alarm_recovered")), "audit events persisted");

        int deleted = await reopened.CleanupSamplesAsync(today.AddDays(-30));
        Expect(deleted == 1, "retention removes old samples only");
        var after = await reopened.QueryAsync("mock-1", null);
        Expect(after.Rows.Count == 20 && after.More, "newer rows retained");
        Expect((await reopened.QueryAuditAsync("mock-1")).Count >= audit.Count,
            "cleanup preserved audit");
        string csv = Path.Combine(tempDir, "history.csv");
        await reopened.ExportCsvAsync("mock-2", csv);
        Expect(File.ReadAllLines(csv).Length == 2, "CSV device isolation");
        Console.WriteLine("PASS: SQLite persistence, alarm acknowledgement, audit, keyset paging, retention, CSV");
    }
}
finally
{
    // Files are test-only and all connections use Pooling=false.
    Directory.Delete(tempDir, recursive: true);
}
