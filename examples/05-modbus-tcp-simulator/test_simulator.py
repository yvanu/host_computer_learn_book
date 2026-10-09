"""Protocol-level integration tests; no hardware or third-party dependencies."""
import asyncio
import struct
import unittest
from simulator import RegisterBank, handle_client


def request(tx: int, function: int, address: int, arg: int, *, unit: int = 1):
    pdu = bytes([function]) + struct.pack(">HH", address, arg)
    return struct.pack(">HHHB", tx, 0, len(pdu) + 1, unit) + pdu


async def receive(reader: asyncio.StreamReader):
    header = await asyncio.wait_for(reader.readexactly(7), 2)
    tx, proto, length, unit = struct.unpack(">HHHB", header)
    pdu = await asyncio.wait_for(reader.readexactly(length - 1), 2)
    return tx, proto, unit, pdu


class ModbusTcpTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        bank = RegisterBank()
        self.server = await asyncio.start_server(
            lambda r, w: handle_client(r, w, bank), "127.0.0.1", 0
        )
        port = self.server.sockets[0].getsockname()[1]
        self.reader, self.writer = await asyncio.open_connection("127.0.0.1", port)

    async def asyncTearDown(self):
        self.writer.close()
        await self.writer.wait_closed()
        self.server.close()
        await self.server.wait_closed()

    async def send(self, frame):
        self.writer.write(frame)
        await self.writer.drain()
        return await receive(self.reader)

    async def test_two_mock_plcs_have_independent_sockets(self):
        a, b = RegisterBank(253), RegisterBank(278)
        s1 = await asyncio.start_server(
            lambda r, w: handle_client(r, w, a), "127.0.0.1", 0)
        s2 = await asyncio.start_server(
            lambda r, w: handle_client(r, w, b), "127.0.0.1", 0)
        try:
            r1, w1 = await asyncio.open_connection(
                "127.0.0.1", s1.sockets[0].getsockname()[1])
            r2, w2 = await asyncio.open_connection(
                "127.0.0.1", s2.sockets[0].getsockname()[1])
            try:
                w1.write(request(10, 6, 2, 1550))
                await w1.drain()
                self.assertEqual((await receive(r1))[-1],
                                 bytes.fromhex("06 00 02 06 0E"))
                w2.write(request(11, 3, 2, 1))
                await w2.drain()
                self.assertEqual((await receive(r2))[-1],
                                 bytes.fromhex("03 02 04 B0"))
                w2.write(request(12, 3, 0, 1))
                await w2.drain()
                self.assertEqual((await receive(r2))[-1],
                                 bytes.fromhex("03 02 01 16"))
            finally:
                w1.close()
                w2.close()
                await w1.wait_closed()
                await w2.wait_closed()
        finally:
            s1.close()
            s2.close()
            await s1.wait_closed()
            await s2.wait_closed()

    async def test_independent_device_register_banks(self):
        device_a = RegisterBank(253)
        device_b = RegisterBank(278)
        device_a.handle_pdu(b"\x06\x00\x02\x05\xdc")
        self.assertEqual(device_a.values[2], 1500)
        self.assertEqual(device_b.values[2], 1200)
        device_b.tick()
        self.assertEqual(device_a.values[0], 253)
        self.assertEqual(device_b.values[0], 284)

    async def test_dynamic_triangle_wave_raises_and_clears_alarm(self):
        bank = RegisterBank()
        for _ in range(20):
            bank.tick()
        self.assertEqual(bank.values[0], 373)  # 37.3 C >= 32 C threshold
        self.assertEqual(bank.values[1] // 10, 101)
        for _ in range(20):
            bank.tick()
        self.assertEqual(bank.values[0], 253)  # returns to normal

    async def test_read_three_registers(self):
        self.assertEqual(await self.send(request(5, 3, 0, 3)),
                         (5, 0, 1, b"\x03\x06\x00\xfd\x03\xf4\x04\xb0"))

    async def test_fragmented_tcp_packet(self):
        frame = request(11, 3, 0, 1)
        self.writer.write(frame[:4])
        await self.writer.drain()
        await asyncio.sleep(0.01)
        self.writer.write(frame[4:8])
        await self.writer.drain()
        self.writer.write(frame[8:])
        await self.writer.drain()
        self.assertEqual(await receive(self.reader),
                         (11, 0, 1, b"\x03\x02\x00\xfd"))

    async def test_coalesced_packets(self):
        self.writer.write(request(1, 3, 0, 1) + request(2, 3, 1, 1))
        await self.writer.drain()
        self.assertEqual(await receive(self.reader), (1, 0, 1, b"\x03\x02\x00\xfd"))
        self.assertEqual(await receive(self.reader), (2, 0, 1, b"\x03\x02\x03\xf4"))

    async def test_write_and_read_back(self):
        self.assertEqual(await self.send(request(10, 6, 2, 1500)),
                         (10, 0, 1, b"\x06\x00\x02\x05\xdc"))
        self.assertEqual(await self.send(request(11, 3, 2, 1)),
                         (11, 0, 1, b"\x03\x02\x05\xdc"))

    async def test_invalid_address(self):
        self.assertEqual((await self.send(request(3, 3, 3, 1)))[-1], b"\x83\x02")
        self.assertEqual((await self.send(request(4, 6, 0, 5)))[-1], b"\x86\x02")

    async def test_write_value_out_of_range(self):
        self.assertEqual((await self.send(request(21, 6, 2, 3001)))[-1], b"\x86\x03")
        self.assertEqual((await self.send(request(22, 3, 2, 1)))[-1], b"\x03\x02\x04\xb0")

    async def test_invalid_register_count(self):
        self.assertEqual((await self.send(request(7, 3, 0, 0)))[-1], b"\x83\x03")
        self.assertEqual((await self.send(request(8, 3, 0, 126)))[-1], b"\x83\x03")

    async def test_unsupported_function(self):
        self.assertEqual((await self.send(request(9, 4, 0, 1)))[-1], b"\x84\x01")

    async def test_wrong_protocol_closes(self):
        frame = bytearray(request(1, 3, 0, 1))
        frame[3] = 1  # nonzero protocol id
        self.writer.write(frame)
        await self.writer.drain()
        self.assertEqual(await asyncio.wait_for(self.reader.read(), 2), b"")

    async def test_truncated_frame_closes(self):
        frame = request(1, 3, 0, 1)
        self.writer.write(frame[:-1])
        await self.writer.drain()
        self.writer.write_eof()
        self.assertEqual(await asyncio.wait_for(self.reader.read(), 2), b"")


if __name__ == "__main__":
    unittest.main()
