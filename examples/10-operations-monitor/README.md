# 实验 10 · v0.5 工业监控交付版（Windows）

这是在 v0.4 双设备示例基础上的独立进阶项目，**不会覆盖** `examples/09-multi-device-monitor`，也不会修改之前的 `monitor_v04.sqlite`。

## 本版新增

- **配置持久化**：每台模拟设备的高温阈值存入 SQLite `device_settings`，重新启动 WPF 后自动恢复。阈值只允许 20～80 ℃。
- **报警与人工确认**：高温进入时写入 `alarm_events`，降温时写入恢复原因；点击“确认最近未确认报警”只设置确认时间，**不等于报警已经恢复**。
- **可追踪审计**：连接、断开、阈值修改、报警进入/恢复、确认、清理操作分别记录到 `audit_events`。可以在“持久审计”选项卡查看选中设备最新记录。
- **历史游标分页**：使用 `id < $cursor` 而不是 OFFSET，后台仍持续写入时更容易保持翻页边界稳定。
- **手动清理**：必须在弹窗中确认；一次只删除 30 天前最多 10,000 条采样，保留报警事件与审计记录。清理不可撤销，先导出 CSV。
- **便携式 Windows 发布包**：GitHub Actions 在 Windows 构建 `win-x64` 文件夹并压成 ZIP。此版本为 **framework-dependent**，用户电脑须安装 .NET 10 Desktop Runtime。

## 一步步启动

PowerShell 1：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --devices 2 --dynamic
```

PowerShell 2（Windows 10/11，.NET 10 SDK）：

```powershell
dotnet run --project examples/10-operations-monitor/OperationsMonitor.csproj
```

依次点击设备 01、设备 02 的“连接选中设备”，更改设备 02 的阈值为 29.0 ℃ 并“应用”。观察高温进入、恢复；点击“确认最近未确认报警”；进入“持久审计”点击“刷新持久审计”；关闭窗口并重开，设备 02 应继续显示 29.0 ℃。

## 数据位置及 SQL

```text
%LOCALAPPDATA%\HostComputerLearn\monitor_v05.sqlite
```

```sql
SELECT device_id, threshold_c FROM device_settings;
SELECT device_id, raised_at_utc, acknowledged_at_utc, ended_at_utc, end_reason
FROM alarm_events ORDER BY id DESC LIMIT 20;
SELECT device_id, event_kind, detail FROM audit_events ORDER BY id DESC LIMIT 20;
```

`end_reason='offline'` 只表示设备会话结束、**状态未知**，不是温度恢复。旧报警即使已经离线或者恢复，依然可以追溯人工确认记录。

## 只测试 SQLite，无需运行 WPF

在具备 .NET 10 SDK 的 Windows 开发机运行：

```powershell
dotnet run --project examples/10-operations-monitor/Smoke/SqliteSmoke.csproj -c Release
```

该测试在临时文件夹中建立独立数据库，检查配置重启保留、双设备隔离、报警确认、审计记录、游标分页、旧数据清理与 CSV。结束后删除临时文件夹。GitHub Actions 已运行该检查并通过。对应 Windows 编译、SQLite 集成和 ZIP 上传记录：[37870194653](https://github.com/yvanu/host_computer_learn_book/actions/runs/37870194653)。

## 关于发布包

从 [GitHub Windows CI 成功记录](https://github.com/yvanu/host_computer_learn_book/actions/runs/37870194653) 的 **Artifacts → operations-monitor-v05-win-x64** 下载 ZIP，**解压一次**，执行 `OperationsMonitor.exe`。它是便携压缩包而不是 MSI 安装程序；运行电脑必须先安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 。仍需另行运行 Python 模拟器。

## 已知边界

- 两台设备端口固定 `127.0.0.1:1502/1503`，默认只读 FC03；**不要直接拿它连接生产 PLC**。
- 本课程简化为本地“演示操作员”，确认报警没有真实用户身份验证；生产场景需要账号权限、时间同步、审计防篡改和数据恢复设计。
- 历史清理只删除样本，不删除审计/报警表，不保证长期存储空间无限可用。
- Windows 云端编译和 SQLite 的无界面测试**不等于**实际鼠标操作、WPF 数据绑定与图形桌面验收。
