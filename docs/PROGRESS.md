# 学习手册开发进度

最后维护：2026-10-09。**此页反映教程编写与项目实现的状态，不代表学习者已学完。**

## v0.1 · 已编写教材

- [x] 课程定位、12 周路线、学习方法
- [x] Windows 开发环境及后端知识迁移
- [x] C# 基础与异步 Task 概念
- [x] WPF/XAML/Binding/MVVM 入门
- [x] 字节、协议帧、TCP 长连接原理与实操
- [x] 串口与 RS-485 基础
- [x] Modbus RTU/TCP 和寄存器基础
- [x] 综合项目蓝图与验收清单
- [x] 示例：C# 控制台数据解析
- [x] 示例：Python TCP 模拟设备
- [x] 示例：C# TCP 客户端
- [x] 示例：WPF 实时监控基础版

## 验证状态

| 验证内容 | 状态 | 说明 |
| --- | --- | --- |
| VitePress 文档站生产构建 | 已通过 | v0.5 新章节使用现有 VitePress 1.6.4，Node heap 256 MB 下构建成功（2026-10-09） |
| Python 设备模拟器联调 | 已通过 | Python unittest 3 项：初始遥测、合法指令 ACK、非法转速拒绝 |
| Cloudflare Docs 网页验收 | 已通过 | 2026-10-09：自定义域名、首页、05-7/05-8 新章节及进度页均 HTTPS 验证通过、HTTP 200 |
| Modbus TCP 模拟器 / 原始帧客户端 | 已通过 | 共 16 项：含两台独立模拟设备 TCP socket 和寄存器隔离、动态温度、FC03/FC06、半包/粘包、异常码 |
| RTU CRC16 校验 | 已通过 | 已知 CRC 向量、损坏报文、短帧 3 项 |
| 6 个 C# 项目 Windows 编译 | 已通过 | GitHub Windows .NET 10 构建完成，v0.4 提交 be5a71c：[CI 记录](https://github.com/yvanu/host_computer_learn_book/actions/runs/37869098891) |
| WPF GUI 桌面操作与 SQLite 实际落盘 | 未验证 | Windows 云端已编译通过，但自动化构建不会打开 GUI 或测试实际交互，需要 Windows 图形机器验收 |
| 真实 PLC/Modbus RTU 联调 | 未开始 | 后续章节与设备条件就绪时再做 |
| v0.5 SQLite 端到端验证 | 已通过 | [Windows CI 37870194653](https://github.com/yvanu/host_computer_learn_book/actions/runs/37870194653)：实际运行 .NET 10 集成测试，覆盖配置、报警、审计、游标分页、清理、CSV |
| v0.5 便携式 ZIP Artifact | 已通过 | [Windows CI 37870194653](https://github.com/yvanu/host_computer_learn_book/actions/runs/37870194653) 的 Artifacts 已生成 `operations-monitor-v05-win-x64`；非 MSI、未签名，需要 .NET 10 Desktop Runtime |

## v0.2 · 工业通信实战（本轮新增）

- [x] C# 属性、接口、事件、泛型入门练习与参考答案
- [x] Modbus TCP Python 模拟设备（FC03/FC06）、寄存器映射与只读/可控写入
- [x] Python 原始帧检查客户端：读取、写入模拟转速、回读确认
- [x] C# Modbus TCP 客户端源码：MBAP 精确分帧、事务号与错误检查（**待 .NET SDK 编译验收**）
- [x] Modbus RTU CRC16 练习：已知校验向量、故障报文拒绝
- [x] 基础联调排障教程与 Modbus 自测 12 题（含解析）
- [x] Python 自动化测试共 **20 项**：旧版文本设备 3 项、Modbus TCP 14 项、CRC 3 项
- [x] Windows .NET 10 / WPF / C# Modbus 项目 CI 编译已通过；真实 GUI 交互验收仍待 Windows 图形机操作
- [ ] 真实串口虚拟端口收发练习与串口驱动配置
- [ ] C# 受控断线重连与超时专项单元测试
- [ ] 更多章节的 10～15 题专项题库

## v0.3 · WPF 工业设备监控台（本轮新增）

- [x] 独立的 `examples/08-wpf-modbus-monitor` 教学项目（不修改早期 WPF 入门代码）
- [x] C# Modbus TCP FC03 连续轮询，固定仅连接本机模拟器，异常和超时状态可观察
- [x] 实时温度/压力/转速指标卡 + 最近 60 次温度折线（无额外图表库）
- [x] 32 ℃ 高温进入/恢复状态切换、最近 30 条事件
- [x] `Channel` 有界采集队列 + `Microsoft.Data.Sqlite` 10.0.0 后台持久化 + CSV 导出
- [x] 模拟器增加 `--dynamic` 温度曲线模式；旧版无参数模式和测试保持兼容
- [x] [05-3 实战](/05-project/wpf-lab) / [05-4 故障和验收](/05-project/monitor-exercises) 两章配套中文教学
- [x] Python 20 项测试通过，新工程两个 XAML 文件通过 XML 结构检查
- [x] Windows GitHub Actions 编译通过（5 个 C# 项目，运行记录 https://github.com/yvanu/host_computer_learn_book/actions/runs/37806813899）；**不等同于 Windows GUI 运行测试**
- [ ] Windows 本机运行：确认连接、温度变化、报警切换、SQLite 写入和 CSV 导出

## v0.4 · 多设备连接与历史分页（2026-10-09）

- [x] 新建独立的 `examples/09-multi-device-monitor/` 进阶工程；保留 v0.3 课程和数据库
- [x] 本地模拟 PLC 支持 `--devices 2 --dynamic`，分别绑定 127.0.0.1:1502 和 :1503
- [x] 两台设备独立 `DevicePanel` 状态、独立 `DeviceSession` 和只读 FC03 采集
- [x] 受控自动重连：连续失败至多重试 3 次，间隔 1/2/4 秒；协议异常不盲目重连；允许取消
- [x] 设备独立阈值（20.0～80.0 ℃）与高温进入/恢复事件
- [x] SQLite `monitor_v04.sqlite` 按 `device_id` 隔离，后台有界队列（240 条）
- [x] 选中设备历史分页查询（20 条/页）、参数化 SQL 和 CSV 单设备导出
- [x] 新增 [05-5 两台设备采集](/05-project/multi-device-lab)、[05-6 SQLite 与阈值](/05-project/history-alerts) 课程
- [x] Python **22 项**测试通过，其中 Modbus TCP 16 项包含双 socket、寄存器隔离和模拟温度
- [x] WPF XAML 两文件 XML 结构检查通过；文档站待同步部署
- [x] Windows .NET 10 CI **6 个 C# 工程编译通过**：[be5a71c 的运行记录](https://github.com/yvanu/host_computer_learn_book/actions/runs/37869098891)
- [ ] Windows WPF 实际 GUI、重连与 SQLite 分页交互验收

## v0.5 · 配置持久化、报警审计与 Windows 交付（2026-10-09）

- [x] 独立新增 `examples/10-operations-monitor/`，保留前面 v0.1～v0.4 的项目与数据库
- [x] 新 SQLite `monitor_v05.sqlite`：设备阈值独立持久化，程序重开自动恢复
- [x] 报警事件：触发、人工确认、恢复/离线；确认**不等于**恢复
- [x] 持久审计：连接断开、阈值修改、报警、操作员确认、手动清理
- [x] 历史列表升级为按 ID 的 keyset 游标分页，避免采集时 OFFSET 翻页漂移
- [x] 旧采样清理手动确认：30 天前，一次最多 10000 行；不删除报警和审计
- [x] 独立 SQLite 无 GUI 集成测试项目，包含重启持久化、双设备隔离、报警确认、游标分页、清理和 CSV
- [x] 配置 GitHub Windows CI 编译 + SQLite 集成验证 + `win-x64` 框架依赖的 ZIP Artifacts 打包
- [x] 新增 [05-7 配置与报警审计](/05-project/settings-audit)、[05-8 数据保留与打包](/05-project/retention-release) 两篇教学课程
- [x] [Windows CI 37870194653](https://github.com/yvanu/host_computer_learn_book/actions/runs/37870194653) **8 个 C# 项目编译、SQLite 集成测试、Windows 便携包发布与上传全部通过**
- [ ] Windows 图形界面手动实测与长时间运行测试（CI 不模拟鼠标交互或真实 PLC）

## 后续 · v0.6+

- [ ] 长期稳定性与内存曲线、多设备采集压力测试
- [ ] 真实用户权限、账号审计、数据备份与完整性防篡改
- [ ] 基于设备配置表的动态设备增删、采集周期配置
- [ ] 安装包和数字签名（当前先交付 Windows ZIP）
- [ ] 进阶工业协议：OPC UA、CAN、PLC 厂商差异
- [ ] 求职面试题与项目复盘

## 内容质量要求

每次新增章节保持“目标 → 已知概念类比 → 原理 → 代码 → 练习 → 验收 → 常见坑”结构；示例优先可离线执行；不能在服务器上验证的内容明确标注，不虚报测试通过。提交时同步更新本页。
