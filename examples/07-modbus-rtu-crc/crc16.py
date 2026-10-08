"""Modbus RTU CRC16, pure Python. CRC bytes appear LOW first on the wire."""


def crc16_modbus(data: bytes) -> int:
    crc = 0xFFFF
    for byte in data:
        crc ^= byte
        for _ in range(8):
            crc = (crc >> 1) ^ (0xA001 if crc & 1 else 0)
    return crc


def make_rtu_frame(pdu_with_address: bytes) -> bytes:
    value = crc16_modbus(pdu_with_address)
    return pdu_with_address + value.to_bytes(2, "little")


def valid_rtu_frame(frame: bytes) -> bool:
    return len(frame) >= 4 and crc16_modbus(frame[:-2]) == int.from_bytes(frame[-2:], "little")


if __name__ == "__main__":
    sample = bytes.fromhex("01 03 00 00 00 0A")
    full = make_rtu_frame(sample)
    print(full.hex(" ").upper())
    print("CRC valid:", valid_rtu_frame(full))
