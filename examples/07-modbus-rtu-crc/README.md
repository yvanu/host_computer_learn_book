# 实验 07：Modbus RTU CRC16

```powershell
python examples/07-modbus-rtu-crc/crc16.py
python -m unittest discover -s examples/07-modbus-rtu-crc -p "test_*.py" -v
```

已知请求：`01 03 00 00 00 0A`

CRC 算法：初值 `FFFF`，反射多项式 `A001`。计算得到的 CRC 整数为 `CDC5`，**在 RTU 帧中低字节先发**，所以输出：

```text
01 03 00 00 00 0A C5 CD
```

只要内容中改动任意一位，原 CRC 就通常不匹配。CRC 能检查传输差错，但不能认证设备或防止恶意篡改。

**与 Modbus TCP 区分：** Modbus TCP 不在其标准帧末尾添加这一 RTU CRC。
