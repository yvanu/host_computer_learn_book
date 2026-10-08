"""Minimal, localhost-only Modbus TCP training device: FC03 and FC06.

Teaching scope: one Unit ID, three holding registers, no concurrency beyond the
single asyncio event loop. Never expose to a production/industrial network.
"""
from __future__ import annotations

import asyncio
import struct

HOST = "127.0.0.1"
PORT = 1502
UNIT_ID = 1
MAX_LENGTH = 254  # MBAP Length = Unit ID (1) + PDU (at most 253)


class RegisterBank:
    """Offsets: 0 temperature x10, 1 pressure x10, 2 motor RPM."""

    def __init__(self) -> None:
        self.values = [253, 1012, 1200]

    def handle_pdu(self, pdu: bytes) -> bytes:
        if not pdu:
            raise ValueError("Missing function code")
        function = pdu[0]
        if function not in (0x03, 0x06):
            return bytes([function | 0x80, 0x01])  # ILLEGAL FUNCTION
        if len(pdu) != 5:
            return bytes([function | 0x80, 0x03])  # ILLEGAL DATA VALUE

        address, arg = struct.unpack(">HH", pdu[1:])
        if function == 0x03:
            if not 1 <= arg <= 125:
                return b"\x83\x03"  # ILLEGAL DATA VALUE (register count)
            if address + arg > len(self.values):
                return b"\x83\x02"  # ILLEGAL DATA ADDRESS
            data = struct.pack(f">{arg}H", *self.values[address:address + arg])
            return bytes([function, len(data)]) + data

        # FC06: only mock RPM is writable. Validate its demonstration range.
        if address != 2:
            return b"\x86\x02"
        if arg > 3000:
            return b"\x86\x03"
        self.values[2] = arg
        return pdu  # FC06 normal response echoes request PDU


async def handle_client(
    reader: asyncio.StreamReader, writer: asyncio.StreamWriter, bank: RegisterBank
) -> None:
    try:
        while True:
            try:
                header = await reader.readexactly(7)
            except asyncio.IncompleteReadError:
                break  # orderly disconnect, or truncated MBAP
            transaction, protocol, length, unit = struct.unpack(">HHHB", header)
            if protocol != 0 or not 2 <= length <= MAX_LENGTH:
                break  # invalid framing; close instead of buffering huge payload
            try:
                pdu = await reader.readexactly(length - 1)
            except asyncio.IncompleteReadError:
                break
            if unit != UNIT_ID:
                continue  # only unit 1 exists in this training simulator
            reply_pdu = bank.handle_pdu(pdu)
            reply_header = struct.pack(">HHHB", transaction, 0, len(reply_pdu) + 1, unit)
            writer.write(reply_header + reply_pdu)
            await writer.drain()
    except (ConnectionError, OSError):
        pass
    finally:
        writer.close()
        try:
            await writer.wait_closed()
        except ConnectionError:
            pass


async def main() -> None:
    bank = RegisterBank()
    server = await asyncio.start_server(
        lambda reader, writer: handle_client(reader, writer, bank), HOST, PORT
    )
    print(f"Modbus TCP training simulator: {HOST}:{PORT}, unit={UNIT_ID}")
    print("Holding registers: offset 0=253 (25.3°C), 1=1012 (101.2kPa), 2=1200 rpm")
    print("Supports FC03 read and FC06 write offset 2 only; Ctrl+C to stop.")
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("Simulator stopped")
