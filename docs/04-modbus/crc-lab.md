# 04-4 · Modbus RTU CRC16：为什么校验字节顺序反过来？

> **学习目标：** 自己计算完整 RTU 报文 CRC；分清“算法计算的数值”和“在线路上发送的两个字节”；会用测试发现单字节错误。

上一课的 Modbus TCP 用 MBAP 帧头与 TCP 的传输机制，不添加 RTU CRC。**这节课单独学习串口 RTU 帧的 CRC16**。

## 1. 一条完整报文

读取从地址 0 开始的 10 个保持寄存器的 RTU 请求：

```text
01 03 00 00 00 0A C5 CD
│  │  │地址│ │数量│ │ CRC │
│  └ 功能码 03
└ 从站地址 01
```

前 6 字节用于计算 CRC：

```text
01 03 00 00 00 0A
```

Modbus RTU CRC16 的初值是 `0xFFFF`，逐位运算采用反射多项式 `0xA001`。这 6 字节的结果为 `0xCDC5`。

**容易弄错：** `0xCDC5` 是 CRC 的 16 位数值，但 RTU 报文要求 **CRC 低字节在前**，所以末尾发送的是 `C5 CD`。

## 2. 一段可运行的 Python 实验

已经写在 `examples/07-modbus-rtu-crc/crc16.py`：

```python
def crc16_modbus(data: bytes) -> int:
    crc = 0xFFFF
    for byte in data:
        crc ^= byte
        for _ in range(8):
            crc = (crc >> 1) ^ (0xA001 if crc & 1 else 0)
    return crc
```

运行：

```powershell
python examples/07-modbus-rtu-crc/crc16.py
```

预期输出：

```text
01 03 00 00 00 0A C5 CD
CRC valid: True
```

## 3. “只改一个字节”的实验

将地址 `00 00` 改为 `00 01`，但最后两个 CRC 字节仍旧保持 `C5 CD`，校验应该失败。

这是典型的**传输差错检查**，不是设备身份认证。攻击者如果能修改报文也可以重新计算 CRC，因此 CRC 不能用作防伪造的安全措施。

## 4. 自动测试

```powershell
python -m unittest discover -s examples/07-modbus-rtu-crc -p "test_*.py" -v
```

覆盖：已知向量、内容损坏、短帧。

## 5. 与 Modbus TCP 的直观对比

| 维度 | RTU | TCP |
| --- | --- | --- |
| 常见媒介 | RS-485 / 串口 | TCP/IP |
| 帧头 | 从站地址 + PDU | MBAP + PDU |
| 帧边界 | 串行时序间隔 + 协议长度 | MBAP 长度 |
| CRC16 | 需要 | 标准 Modbus TCP 不添加 |
| 是否具备身份认证 | 通常不具备 | 通常不具备 |

## 本课练习

1. CRC 计算得到 `0x1234`，若仅讨论发送顺序，RTU 应先发哪个字节？**答案：34，再发 12。**
2. CRC 正确是否代表接收的是可信指令？**不代表，只说明数据符合该算法校验。**
3. 同一组 RTU 业务请求换成 TCP 封装，为什么不能把尾部两个 CRC 原样追加？**两种帧封装不同，TCP 使用 MBAP。**

**验收：** 不看代码能说出 CRC 初值、计算方法、输出字节顺序，并运行至少一个“错误数据拒绝”的测试。

规范来源：[Modbus Organization](https://www.modbus.org/modbus-specifications)。
