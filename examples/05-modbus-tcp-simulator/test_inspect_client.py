"""End-to-end smoke tests for the companion Python Modbus TCP inspector."""
import asyncio
import contextlib
import io
import unittest

from inspect_client import main
from simulator import RegisterBank, handle_client


class InspectorIntegrationTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        bank = RegisterBank()
        self.server = await asyncio.start_server(
            lambda r, w: handle_client(r, w, bank), "127.0.0.1", 0
        )
        self.port = self.server.sockets[0].getsockname()[1]

    async def asyncTearDown(self):
        self.server.close()
        await self.server.wait_closed()

    async def invoke(self, args):
        captured = io.StringIO()

        def run():
            with contextlib.redirect_stdout(captured):
                return main(args, port=self.port)

        code = await asyncio.wait_for(asyncio.to_thread(run), 5)
        return code, captured.getvalue()

    async def test_read_and_decode(self):
        code, output = await self.invoke([])
        self.assertEqual(code, 0)
        self.assertIn("Temperature 25.3", output)
        self.assertIn("pressure 101.2", output)
        self.assertIn("RPM 1200", output)
        self.assertIn("00 01 00 00 00 06 01 03", output)

    async def test_write_and_readback(self):
        code, output = await self.invoke(["write-rpm", "1500"])
        self.assertEqual(code, 0)
        self.assertIn("Mock RPM confirmed: 1500", output)

    async def test_invalid_cli(self):
        code, output = await self.invoke(["wrong"])
        self.assertEqual(code, 2)
        self.assertIn("Usage:", output)


if __name__ == "__main__":
    unittest.main()
