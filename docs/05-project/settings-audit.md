# 05-7 · 配置持久化、报警确认与审计：让上位机留下证据

> 本课目标：理解什么数据要一直保存、什么叫“确认报警”、为什么不能把报警恢复和确认混为一谈，以及设备离线应该怎样记录。你已经会数据库与 Flask，这一课主要学习**长期运行桌面应用的状态生命周期**。

配套工程：[examples/10-operations-monitor](https://github.com/yvanu/host_computer_learn_book/tree/main/examples/10-operations-monitor)。

## 1. 先区分三类数据

一个长期运行的上位机至少有三类状态：

| 类别 | 示例 | 是否需要跨程序重启保留？ |
| --- | --- | --- |
| 实时状态 | 温度 35.6 ℃、设备在线 | 不一定；重启后需要重新确认在线 |
| 设备配置 | 设备 02 报警温度 29 ℃ | **需要**，否则每次重开都要重新设置 |
| 过程记录 | 报警出现、恢复、操作员点击确认 | **需要**，否则无法解释事故时间线 |

前一个版本将阈值放在 `DevicePanel.Threshold` 属性内，程序退出后丢失。这版新增 SQLite `device_settings`：

```sql
CREATE TABLE device_settings (
  device_id TEXT PRIMARY KEY,
  threshold_c REAL NOT NULL,
  changed_at_utc TEXT NOT NULL
);
```

每次点击“应用”，先验证是否为有限数字、范围 20.0～80.0 ℃，再通过 **参数化 UPSERT** 写入 SQLite。启动时查询配置恢复到对应设备，不与其他设备混淆。

## 2. 为什么“报警确认”不是“报警恢复”

设想：传感器从 28 ℃ 升到 35 ℃，上位机报警；操作员在 35 ℃ 时点击“已确认”。

- **触发（raised）**：读数达到阈值，风险出现。
- **确认（acknowledged）**：有人看到了这条报警；**温度可能仍为 35 ℃**。
- **恢复（recovered）**：后续读数低于阈值，数值回到正常范围。
- **离线（offline）**：通信中断，**不知道现场温度是否恢复**，绝不能显示成已恢复。

本书在 `alarm_events` 保留这三类时间：

```sql
CREATE TABLE alarm_events (
  id INTEGER PRIMARY KEY,
  device_id TEXT NOT NULL,
  raised_at_utc TEXT NOT NULL,
  temperature_c REAL NOT NULL,
  threshold_c REAL NOT NULL,
  acknowledged_at_utc TEXT,
  ended_at_utc TEXT,
  end_reason TEXT
);
```

示例时间线：

```text
10:00:00  temp = 35.0    raised_at = 10:00:00
10:00:10  操作员确认      acknowledged_at = 10:00:10
10:01:30  temp = 28.0    ended_at = 10:01:30, end_reason = recovered
```

如果 10:01:00 设备掉线，结束原因应当是 `offline` 而不是 `recovered`。程序退出时也会尽可能关闭未完成的在线会话并记录审计；突然断电不保证产生离线事件，所以重连时若发现遗留报警，会将旧事件标记为 `interrupted`，另起新事件。

> [!WARNING] 本书的“操作员确认”只是教学动作
> 没有用户登录或身份认证，记录中只能标识为演示操作员。**真实工厂的审计需要用户身份、权限、日志完整性保护与备份**，不能把这个示例视为合规生产系统。

## 3. 实际运行步骤

先启动两台本机设备：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --devices 2 --dynamic
```

再打开 Windows 桌面监控台：

```powershell
dotnet run --project examples/10-operations-monitor/OperationsMonitor.csproj
```

按顺序做：选择“模拟设备 02” → 把阈值从 32.0 改成 29.0 → “应用” → 连接设备 → 观察高温 → 点击“确认最近未确认报警” → 切换到“持久审计”并刷新 → 关闭并重新打开程序。你应该看到设备 02 的阈值继续是 29.0，设备 01 仍保持自己的值。

## 4. 用 SQL 验证，而不是只看 UI 提示

数据库使用独立的新路径：

```text
%LOCALAPPDATA%\HostComputerLearn\monitor_v05.sqlite
```

打开 SQLite 客户端或 Python 的 sqlite3：

```sql
SELECT device_id, threshold_c, changed_at_utc FROM device_settings;

SELECT device_id, raised_at_utc, acknowledged_at_utc,
       ended_at_utc, end_reason
FROM alarm_events ORDER BY id DESC LIMIT 10;

SELECT device_id, recorded_at_utc, event_kind, detail
FROM audit_events ORDER BY id DESC LIMIT 20;
```

你要能回答：某条报警何时触发、是否被确认、何时恢复、若设备离线应如何标识。

## 5. 设计选择与边界

本书没有引入账号中心、消息总线、云端审计平台，因为你现在的学习目标是掌握**设备侧流程 → SQLite 持久化**。真实生产项目再考虑本地权限、服务端审计、设备证书、备份和防篡改。

还有一个细节：SQLite 开启 WAL 后，后台采集写入、人工确认和只读历史查询可以拥有相对独立的连接，但仍然存在锁和 I/O 延迟；代码采用短事务与 5 秒超时，而不是“SQLite 永远不会锁表”。

## 6. 练习与验收

1. 设备 01 的阈值保持 32 ℃，设备 02 设置 29 ℃；重启后确认两个配置仍独立。
2. 在某次高温尚未恢复时点击确认；验证 `acknowledged_at_utc` 不为空，但 `ended_at_utc` 仍可能为空。
3. 中断模拟器，检查 `end_reason`：不应写成 `recovered`。
4. 在 WPF 窗口点击“刷新持久审计”，说明 UI 事件与数据库审计的区别。
5. 给报警管理设计一个真实用户 ID 字段，并解释为什么在工业现场还应记录操作者的权限。

**验收：** 能清楚区分触发、确认、恢复、离线，且能够用 SQL 恢复整个报警时间线。

下一课：[05-8 历史清理、Windows 发布包和无界面验收](/05-project/retention-release)。
