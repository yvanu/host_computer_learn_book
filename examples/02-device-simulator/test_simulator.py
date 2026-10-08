"""Integration tests for newline framing and command round trips."""
import asyncio
import unittest

from simulator import handle_client


class DeviceSimulatorTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.server = await asyncio.start_server(handle_client, "127.0.0.1", 0)
        self.port = self.server.sockets[0].getsockname()[1]
        self.reader, self.writer = await asyncio.open_connection("127.0.0.1", self.port)

    async def asyncTearDown(self):
        self.writer.close()
        await self.writer.wait_closed()
        self.server.close()
        await self.server.wait_closed()
        await asyncio.sleep(0)

    async def read_until_prefix(self, prefix):
        for _ in range(5):
            line = await asyncio.wait_for(self.reader.readline(), timeout=2)
            if line.startswith(prefix):
                return line.decode().strip()
        self.fail(f"missing line prefix {prefix!r}")

    async def test_receives_valid_telemetry(self):
        data = await self.read_until_prefix(b"TEMP=")
        self.assertIn("PRESSURE=", data)
        self.assertIn("RPM=1200", data)

    async def test_command_ack_and_new_rpm(self):
        await self.read_until_prefix(b"TEMP=")
        self.writer.write(b"SET RPM=1500\n")
        await self.writer.drain()
        self.assertEqual(await self.read_until_prefix(b"ACK="), "ACK=RPM:1500")
        self.assertIn("RPM=1500", await self.read_until_prefix(b"TEMP="))

    async def test_invalid_command_rejected(self):
        await self.read_until_prefix(b"TEMP=")
        self.writer.write(b"SET RPM=99999\n")
        await self.writer.drain()
        self.assertEqual(await self.read_until_prefix(b"ERR="), "ERR=INVALID_RPM")


if __name__ == "__main__":
    unittest.main()
