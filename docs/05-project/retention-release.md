# 05-8 · 数据保留、游标分页与 Windows 发布验收

> 本课目标：理解为什么上位机不能无限保存所有原始采样，如何避免错误删除，怎样处理持续 INSERT 时的翻页，以及如何交付可运行的 Windows 压缩包。

## 1. SQLite 为什么也需要保留策略？

两台设备每秒一条采样，连续一天就会新增约 `2 × 86,400 = 172,800` 行。真实设备可能还有几十种指标。即使现在只有几 MB，长期使用也必须明确**保存时长、容量阈值、清理和备份**。

本例的删除策略是刻意保守的：

- 清理**只针对采样表 `readings`**，不删除 `alarm_events` / `audit_events`。
- 必须在界面点击“清理 30 天前数据”并确认，程序不会在后台悄悄删除。
- 每次最多清理 10,000 条，每批至多 500 条；如果积累更多旧记录，需要再次明确执行。
- 删除不可撤销，**应先按设备导出 CSV 或备份数据库**。
- 清理操作本身会写一条 `retention_cleanup` 审计。

```sql
DELETE FROM readings WHERE id IN (
  SELECT id FROM readings
  WHERE captured_at_utc < $cutoff
  ORDER BY id LIMIT 500
);
```

实际 C# 代码采用 SQL 参数，`$cutoff` 为 UTC ISO8601 时间，严格限制删除行数，后台执行不堵塞 UI。**示例没有自动 VACUUM**，因为它可能锁住数据库并增加 I/O；工业环境需要在维护窗口内统一安排空间回收。

## 2. 为什么从 OFFSET 改为游标？

上一版历史列表：

```sql
SELECT * FROM readings
WHERE device_id = $device
ORDER BY id DESC LIMIT 20 OFFSET 20;
```

后台仍在采集，每秒新增 1 行。你在第一页停留时如果又新增 3 行，第二页的 OFFSET 边界可能移动，导致翻页重复或漏看数据。

新版本只记录当前页最后一条的 `id`，下一页查 **更小的 ID**：

```sql
SELECT id, captured_at_utc, temperature_c, pressure_kpa, rpm, alarm
FROM readings WHERE device_id = $device AND id < $cursor
ORDER BY id DESC LIMIT 21;
```

为什么 `LIMIT 21`？展示 20 条，多查 1 条用于判断是否还有下一页。上一页回退时使用前一页保存的游标。与 OFFSET 比，它更适合持续新增的时间序列。教学版没有实现跳转任意页数或跨条件搜索；等真实需求出现再扩展。

## 3. 直接拿到便携式 Windows 发布包

新增的 GitHub CI 在托管 Windows 运行器上执行：

1. `.NET 10` 编译原有 C# 示例以及 v0.5 WPF 工程。
2. 运行无界面 SQLite 测试，验证配置、报警确认、审计、游标分页、数据清理。
3. 发布 **framework-dependent win-x64** WPF 程序，压缩成 ZIP。
4. 上传为 GitHub Actions Artifact，名称为 `operations-monitor-v05-win-x64`。

打开 [GitHub Actions](https://github.com/yvanu/host_computer_learn_book/actions/workflows/desktop-build.yml)，点击最近一次成功的运行记录，在页面底部 Artifacts 下载 ZIP。解压后启动 `OperationsMonitor.exe`。

> [!IMPORTANT] 这不是 MSI 安装程序
> 压缩包需要 Windows x64，以及 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。它没有代码签名，不是生产交付包，也不会随附真实 PLC 或 Python 模拟器。运行前先在另一终端启动对应模拟设备。

## 4. SQLite 无界面测试为什么重要？

前几个版本只在 Windows CI **编译**通过，仍不知道 SQLite 语句是否能实际执行。因此这版新增：

```powershell
dotnet run --project examples/10-operations-monitor/Smoke/SqliteSmoke.csproj -c Release
```

测试会在 Windows 临时目录创建数据库，检查：

- 阈值保存后重开可读；
- 两台设备的数据和 CSV 不串；
- 高温报警触发、确认、防重复确认、恢复与持久审计；
- 大于 20 条的游标分页，避免数据重复；
- 只清理 30 天前的样本，**保留审计**。

测试成功会打印 PASS，失败抛出异常使 CI 红灯。这样你能从后端工程师熟悉的“编译 + 数据层集成测试”过渡到更接近工业应用的验收。

## 5. 需要你在 Windows 上亲手完成的最后验收

| 检查 | 预期 |
| --- | --- |
| 两台模拟设备同时在线 | 两个独立连接，温度互不相同 |
| 修改设备 02 阈值并重启 | 配置仍然保留，设备 01 不受影响 |
| 高温时点击确认 | 确认记录产生，不会修改当前温度 |
| 高温时关闭设备模拟器 | 标识离线，不把离线写成恢复 |
| 历史分页 + CSV | 仅查出和导出选中设备 |
| 清理 30 天前数据 | 必须确认，审计保留 |
| Windows 发布 ZIP | 解压可启动，运行环境说明准确 |

**验收标准：** GitHub CI 通过、你能在 Windows 实际复现并记录这七项，且能解释 Modbus 帧、WPF Binding、SQLite 队列、报警和审计各自职责。

后续要补的不是更多按钮，而是**真实 Windows GUI 自动化、长时运行、账号权限、备份恢复和签名安装程序**。
