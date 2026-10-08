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
| VitePress 文档站生产构建 | 已通过 | v0.2 新章节使用现有 VitePress 1.6.4，限制 Node heap 256 MB 后构建通过（2026-10-08） |
| Python 设备模拟器联调 | 已通过 | Python unittest 3 项：初始遥测、合法指令 ACK、非法转速拒绝 |
| Cloudflare Docs 网页验收 | 已通过 | 自定义域名 https://docs.host.majhoon.site 曾在 2026-10-08 验证 HTTPS 及 HTTP 200；新章节上线后另行复测 |
| Modbus TCP 模拟器 / 原始帧客户端 | 已通过 | 实际 TCP socket 联调覆盖 FC03、FC06、半包、粘包、异常码、截断帧、写入回读等 14 项（增加动态温度测试） |
| RTU CRC16 校验 | 已通过 | 已知 CRC 向量、损坏报文、短帧 3 项 |
| 5 个 C# 项目 Windows 编译 | 已通过 | GitHub 托管 Windows 运行器 .NET 10 构建全部通过，提交 a3f4e4c；包含 WPF + SQLite 和各控制台项目 |
| WPF GUI 桌面操作与 SQLite 实际落盘 | 未验证 | Windows 云端已编译通过，但自动化构建不会打开 GUI 或测试实际交互，需要 Windows 图形机器验收 |
| 真实 PLC/Modbus RTU 联调 | 未开始 | 后续章节与设备条件就绪时再做 |

## v0.2 · 工业通信实战（本轮新增）

- [x] C# 属性、接口、事件、泛型入门练习与参考答案
- [x] Modbus TCP Python 模拟设备（FC03/FC06）、寄存器映射与只读/可控写入
- [x] Python 原始帧检查客户端：读取、写入模拟转速、回读确认
- [x] C# Modbus TCP 客户端源码：MBAP 精确分帧、事务号与错误检查（**待 .NET SDK 编译验收**）
- [x] Modbus RTU CRC16 练习：已知校验向量、故障报文拒绝
- [x] 基础联调排障教程与 Modbus 自测 12 题（含解析）
- [x] Python 自动化测试共 **20 项**：旧版文本设备 3 项、Modbus TCP 14 项、CRC 3 项
- [ ] Windows 上 .NET 10 / WPF / C# Modbus 客户端编译和界面验收（当前 Linux 服务器无 SDK）
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

## 后续 · v0.4+

- [ ] 多设备列表、独立连接会话和采集调度
- [ ] 历史查询与 SQLite 分页、可配置报警阈值与事件确认
- [ ] 受控重连与异常、负载和长期运行测试
- [ ] 工程化测试、发布安装包和签名
- [ ] 进阶工业协议：OPC UA、CAN、PLC 厂商差异
- [ ] 求职面试题与项目复盘

## 内容质量要求

每次新增章节保持“目标 → 已知概念类比 → 原理 → 代码 → 练习 → 验收 → 常见坑”结构；示例优先可离线执行；不能在服务器上验证的内容明确标注，不虚报测试通过。提交时同步更新本页。
