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

    def __init__(self, temperature_base: int = 253) -> None:
        self._temperature_base = temperature_base
        self.values = [temperature_base, 1012, 1200]

    def tick(self) -> None:
        """Optional triangle-wave temperature for the graphical monitoring lab."""
        phase = getattr(self, "_tick", 0) + 1
        self._tick = phase % 40
        self.values[0] = self._temperature_base + 6 * min(self._tick, 40 - self._tick)
        self.values[1] = 1012 + (self._tick % 5)

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


async def main(dynamic: bool = False, devices: int = 1) -> None:
    # The second independent mock PLC listens on 1503, with a different temperature baseline.
    banks = [RegisterBank(253 + i * 25) for i in range(devices)]
    servers = []
    for i, bank in enumerate(banks):
        server = await asyncio.start_server(
            lambda reader, writer, device=bank: handle_client(reader, writer, device),
            HOST, PORT + i
        )
        servers.append(server)
        print(f"Modbus TCP mock #{i + 1}: {HOST}:{PORT + i}, unit={UNIT_ID}")
    print("Registers 0/1/2 = temperature x10, pressure x10, RPM. FC03/FC06.")
    if dynamic:
        print("Dynamic temperature enabled (1-second interval).")

    async def vary_values() -> None:
        while True:
            await asyncio.sleep(1)
            for bank in banks:
                bank.tick()

    task = asyncio.create_task(vary_values()) if dynamic else None
    try:
        await asyncio.gather(*(s.serve_forever() for s in servers))
    finally:
        if task:
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass
        for server in servers:
            server.close()
        await asyncio.gather(*(s.wait_closed() for s in servers))


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Local-only Modbus TCP learning PLC")
    parser.add_argument("--dynamic", action="store_true", help="Change temperature each second")
    parser.add_argument("--devices", type=int, choices=[1, 2], default=1)
    args = parser.parse_args()
    try:
        asyncio.run(main(dynamic=args.dynamic, devices=args.devices))
    except KeyboardInterrupt:
        print("Simulator stopped")
