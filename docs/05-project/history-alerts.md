# 05-6 · 每台设备的历史记录、分页查询和独立报警

> **目标：** 用熟悉的 SQL、队列与数据库设计经验，把真实采集变成可追溯的数据。重点是 **device_id 隔离、有界采集队列、分页查询、每设备独立阈值**。

## 一、不要让两台设备写进一份没有设备 ID 的表

v0.3 教学示例只有一个设备，`readings` 直接写时间、温度、压力、转速和 alarm。多设备后这样会出现“查询最新 20 条记录，却不知道是哪个设备”。

v0.4 的新数据库模式：

```sql
CREATE TABLE readings (
  id INTEGER PRIMARY KEY,
  device_id TEXT NOT NULL,
  captured_at_utc TEXT NOT NULL,
  temperature_c REAL NOT NULL,
  pressure_kpa REAL NOT NULL,
  rpm INTEGER NOT NULL,
  alarm INTEGER NOT NULL
);
CREATE INDEX ix_readings_device_id ON readings(device_id, id DESC);
```

`device_id` 由**当前设备会话**指定，而不是由用户随意编辑一条历史记录。

由于是课程里的阶段性项目，本例使用新文件 `monitor_v04.sqlite`，不修改和迁移上一版的 `monitor.sqlite`。这样你能分别复现实验，不会覆盖原有数据。

## 二、两条采集流，共享一个写库任务

```text
PLC-01 ──每秒 DeviceReading──┐
                            ├→ Channel 容量 240 → 1 个后台 SQLite 写入者
PLC-02 ──每秒 DeviceReading──┘
```

`Channel` 设置 `SingleReader=true`、`SingleWriter=false`；超过容量时生产者等待写入，避免无上限积压内存。

**为什么这样写？** 因为在 WPF 的 UI Dispatcher 上同步执行磁盘 SQL，可能出现窗口无响应。后台写库可以隔离单次 I/O 延迟。仍然要认识到：达到队列上限以后 `AddAsync` 会等待，这称为**背压**，而不是“无论如何每秒绝不延迟”。

## 三、分页查询必须带 device_id

点击“查询历史”后，按选中设备单独查询：

```sql
SELECT captured_at_utc, temperature_c, pressure_kpa, rpm, alarm
FROM readings
WHERE device_id = 'mock-1'
ORDER BY id DESC
LIMIT 21 OFFSET 0;
```

每页只展示 20 条，第 21 条只是用来判断是否有“下一页”。在 C# 里要使用 **SQL 参数**（`$device`、`$count`、`$offset`），不要把用户输入直接拼进 SQL。

选择设备 02 后应清空旧历史列表，再点击“查询历史”读取设备 02 的内容。下一页使用偏移 20、40、60 等。

> [!NOTE] 分页教学边界
> OFFSET 分页最容易理解，但历史数据增长到百万级别后，深页 OFFSET 代价更高，且后台继续 INSERT 时分页可能发生记录位移。生产场景可考虑以 `id < last_seen_id` 形式做游标分页，并设置时间区间和数据保留策略。

## 四、报警阈值为什么需要每台设备单独配置？

如果设备 01 的高温定义是 32 ℃，设备 02 的高温定义是 29 ℃，你不能使用整个窗口唯一的全局变量 `threshold=32`。否则切换设备会修改另一台设备的判断规则。

项目将 `Threshold` 存在每台 `DevicePanel` 里，范围限定为 **20.0～80.0 ℃**，只有点击“应用”后才改变；下次有效测点进入状态转换判断。

```text
上次正常 → 本次超温 → 记录一次进入报警
持续超温 → 更新数值，但不重复制造“进入报警”事件
上次超温 → 本次恢复 → 记录一次恢复
```

SQLite 中的 `alarm=1` 表示**采样当时按照该设备生效阈值计算出的高温状态**。以后调高/调低阈值，不会追溯改写历史报警标记；这也是为什么大型工业系统通常需要额外保存“阈值修改事件”。

## 五、亲手验证历史数据

运行：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --devices 2 --dynamic
dotnet run --project examples/09-multi-device-monitor/MultiDeviceMonitor.csproj
```

两个命令需要分别在两个终端执行。

将设备 02 阈值改为 29.0 ℃，启动两台采集，持续约 30 秒，再选中设备 01 点击“查询历史”，之后切换设备 02 再查询。

用 Python 标准库检查 SQLite：

```powershell
python -c "import os,sqlite3; p=os.path.join(os.environ['LOCALAPPDATA'],'HostComputerLearn','monitor_v04.sqlite'); c=sqlite3.connect(p); print(c.execute('select device_id,count(*),sum(alarm) from readings group by device_id').fetchall())"
```

**期望**出现 `mock-1` 和 `mock-2` 两个分组，而不是所有采样混在一起。

## 六、异常场景练习

| 场景 | 预期表现 |
| --- | --- |
| 没启动模拟器就点击连接 | 最多三次退避重试，不无限刷屏 |
| 将阈值输入 abc | 拒绝修改并记录输入错误 |
| 将阈值输入 999 | 拒绝修改（超过 80 ℃ 上限） |
| 只选设备 02 查询 | 不显示设备 01 的历史采样 |
| 在设备 01 上导出 CSV | 文件中只包含设备 01 的采样 |
| 运行期间关闭窗口 | 停止两个会话、等待 SQLite 队列落盘 |

## 七、进阶思考

- 要让阈值在重启后保留，应该建哪个配置表？如何审计修改人和修改时间？
- 两个设备以不同采样周期写入数据库，如何避免 CPU、网络和 UI 更新频率互相影响？
- 为什么**设备控制的重试**和**数据读取的重试**不是同一个设计问题？
- 如果长期运行，如何按时间分区/归档/清理数据，而不是无限增长 SQLite 文件？

**验收：** 能独立解释设备 ID、SQL 参数化、OFFSET 与游标分页的区别、有界 Channel、报警状态转换，且能通过 SQLite 查询实际验证数据确实按设备隔离。

继续学习：[综合项目分阶段验收](/05-project/steps)。
