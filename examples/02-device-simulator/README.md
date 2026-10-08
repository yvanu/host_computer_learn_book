# 第 2 个实验：TCP 模拟设备（Python 标准库）

运行 `python simulator.py`，监听 `127.0.0.1:9000`。每隔约 1 秒向已连接客户端发送：

```text
TEMP=23.3;PRESSURE=101.2;RPM=1200\n
```

支持控制文本 `SET RPM=1500\n`（0～3000），返回 `ACK=RPM:1500\n`；非法请求返回 `ERR=INVALID_COMMAND` 或 `ERR=INVALID_RPM`。

本协议只用于演示 TCP 拆包/组帧与实时数值更新，不是 Modbus，不保证工业设备级可靠性，不可用于真实机器控制。

验证：`python -m unittest discover -s examples/02-device-simulator -p 'test_*.py'`。
