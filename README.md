# 上位机开发学习手册

一份面向**有后端经验、想转上位机开发**的中文实战教程。使用 C# / WPF / TCP / 串口 / Modbus，从第一个控制台程序走到工业监控上位机。

- [📖 在线学习手册](https://host-computer-learn-docs.1124645485.workers.dev/)
- [🗺️ 完整学习路线](docs/roadmap.md)
- [🚀 第一课：怎么开始](docs/00-start/index.md)
- [💻 可运行配套示例](examples/)
- [📋 内容完成度与下一步](docs/PROGRESS.md)
- [🧪 Modbus TCP 实操与 12 道自测题](docs/04-modbus/tcp-lab.md)

## 教材目标

你已经具备 Python、Flask、数据库、TCP、异步任务和系统部署经验。本手册不重复计算机基础，而是帮助你补齐 **C# 语言 → WPF MVVM → 字节协议 → 设备通信 → 工业现场可靠性**。

每日 1～2 小时，约 10～12 周可完成基础路线。**这是学习规划，不是学习效果保证。**

## 当前可动手的实验

| 示例 | 学什么 | 环境 |
| --- | --- | --- |
| [01 C# 基础](examples/01-csharp-basics/) | 类型、解析、强类型模型 | .NET 10，跨平台 |
| [02 Python 模拟设备](examples/02-device-simulator/) | 模拟传感器 TCP 长连接 | Python 3.10+，跨平台 |
| [03 C# TCP 客户端](examples/03-tcp-client/) | 行协议、异步、收发数据 | .NET 10，跨平台 |
| [04 WPF 监控面板](examples/04-wpf-monitor/) | 数据绑定、MVVM、实时状态 | Windows，.NET 10 |
| [05 Modbus TCP 模拟器](examples/05-modbus-tcp-simulator/) | MBAP、FC03、FC06、异常响应 | Python 3.10+，跨平台 |
| [06 C# Modbus 客户端](examples/06-modbus-tcp-client/) | 精确分帧、读写寄存器、响应匹配 | .NET 10，跨平台 |
| [07 RTU CRC16 校验](examples/07-modbus-rtu-crc/) | 反射多项式、低字节先发 | Python 3.10+，跨平台 |

> 即使没有 PLC、串口转换器或实际传感器，也能先用模拟设备练习。上位机控制真实工业设备时，必须遵守设备厂商说明和现场安全规程。

## 在线文档本地开发

```bash
npm install
npm run docs:dev
npm run docs:build
```

## Cloudflare 发布

```bash
npm run docs:build
npx wrangler deploy -c wrangler.jsonc
```

文档站使用 VitePress 和 Cloudflare Workers 静态资源托管；凭证仅通过环境变量使用，**不要提交 API Token**。

持续增补内容请优先修改 `docs/` 和 `docs/PROGRESS.md`，新实验请增加 `examples/`。项目遵循 MIT 许可证。
