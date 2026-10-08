# 实验 05：Python Modbus TCP 模拟设备

纯标准库实现，只监听 `127.0.0.1:1502`，无需购买 PLC、管理员权限或安装 pip 包。

## 启动

```powershell
python examples/05-modbus-tcp-simulator/simulator.py
```

要给 [WPF 工业监控台](../08-wpf-modbus-monitor/) 演示实时温度折线和报警，使用动态模式：

```powershell
python examples/05-modbus-tcp-simulator/simulator.py --dynamic
```

动态模式每秒产生一笔温度变化，从 25.3 ℃ 上升到约 37.3 ℃ 再回落；超过 32.0 ℃ 时可触发 UI 报警。默认无参数模式仍保持固定寄存器值，方便运行早期单元测试。

寄存器与模拟数据：

| Holding Register 报文偏移 | 显示编号（常见约定） | 解释 | 初始原始值 |
| --- | --- | --- | --- |
| 0 | 40001 | 温度，0.1 ℃/单位 | 253 → 25.3 ℃ |
| 1 | 40002 | 压力，0.1 kPa/单位 | 1012 → 101.2 kPa |
| 2 | 40003 | 转速，1 rpm/单位 | 1200 |

支持功能码 `03`（读保持寄存器）和 `06`（写单个保持寄存器，**仅偏移 2，允许模拟 RPM 0～3000**），Unit ID 为 1。其他功能码返回异常 01；非法地址返回异常 02；非法数量或 RPM 越界返回异常 03。

## 可以先只用 Python 验证整个 TCP 链路

模拟器正在运行时，第二个终端执行：

```powershell
python examples/05-modbus-tcp-simulator/inspect_client.py
python examples/05-modbus-tcp-simulator/inspect_client.py write-rpm 1500
```

会打印完整请求/响应十六进制以及寄存器换算结果。这样即使还没有安装 .NET SDK，也能完成协议层的验证。

## 测试

```powershell
python -m unittest discover -s examples/05-modbus-tcp-simulator -p "test_*.py" -v
```

包括粘包、半包、非法协议号、截断帧、非法寄存器、事务号回传以及 FC06 写入回读。

本模拟器不模拟现场安全联锁、认证或生产通信稳定性；**严禁作为真实工业设备服务端使用**。
