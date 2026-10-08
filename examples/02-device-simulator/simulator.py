"""Teaching-only TCP sensor simulator. localhost:9000, UTF-8 newline framing."""
from __future__ import annotations

import asyncio
from contextlib import suppress


async def handle_client(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    peer = writer.get_extra_info("peername")
    print(f"[connect] {peer}")
    rpm = 1200
    sequence = 0

    async def publish() -> None:
        nonlocal sequence
        while True:
            sequence += 1
            temperature = 23.0 + (sequence % 7) * 0.3
            pressure = 101.0 + (sequence % 5) * 0.1
            data = f"TEMP={temperature:.1f};PRESSURE={pressure:.1f};RPM={rpm}\n"
            writer.write(data.encode("utf-8"))
            await writer.drain()
            await asyncio.sleep(1)

    publisher = asyncio.create_task(publish())
    try:
        while raw := await reader.readline():
            if len(raw) > 256:
                writer.write(b"ERR=TOO_LONG\n")
                await writer.drain()
                continue
            command = raw.decode("utf-8", errors="replace").strip()
            if command.startswith("SET RPM="):
                try:
                    requested = int(command.split("=", 1)[1])
                    if not 0 <= requested <= 3000:
                        raise ValueError("out of range")
                except ValueError:
                    writer.write(b"ERR=INVALID_RPM\n")
                else:
                    rpm = requested
                    writer.write(f"ACK=RPM:{rpm}\n".encode("utf-8"))
            else:
                writer.write(b"ERR=INVALID_COMMAND\n")
            await writer.drain()
    except (ConnectionError, asyncio.IncompleteReadError):
        pass
    finally:
        publisher.cancel()
        with suppress(asyncio.CancelledError, ConnectionError):
            await publisher
        writer.close()
        with suppress(ConnectionError):
            await writer.wait_closed()
        print(f"[disconnect] {peer}")


async def main() -> None:
    server = await asyncio.start_server(handle_client, "127.0.0.1", 9000)
    print("Device simulator listening on 127.0.0.1:9000")
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("Simulator stopped")
