"""Inspect raw Modbus TCP frames using only Python's standard library.

Training target is always localhost:1502; write is opt-in (only mock RPM).
"""
import socket
import struct
import sys


def receive_exact(conn: socket.socket, count: int) -> bytes:
    received = bytearray()
    while len(received) < count:
        part = conn.recv(count - len(received))
        if not part:
            raise ConnectionError(f"Connection closed after {len(received)}/{count} bytes")
        received.extend(part)
    return bytes(received)


def exchange(conn: socket.socket, tx: int, function: int, address: int, arg: int) -> bytes:
    pdu = bytes([function]) + struct.pack(">HH", address, arg)
    request = struct.pack(">HHHB", tx, 0, len(pdu) + 1, 1) + pdu
    print("> " + request.hex(" ").upper())
    conn.sendall(request)

    header = receive_exact(conn, 7)
    received_tx, protocol, length, unit = struct.unpack(">HHHB", header)
    if received_tx != tx or protocol != 0 or unit != 1 or not 2 <= length <= 254:
        raise ValueError("Invalid MBAP header")
    response = receive_exact(conn, length - 1)
    print("< " + (header + response).hex(" ").upper())
    if len(response) == 2 and response[0] == function | 0x80:
        raise ValueError(f"Modbus exception 0x{response[1]:02X}")
    if not response or response[0] != function:
        raise ValueError("Unexpected function code")
    return response


def main(args: list[str] | None = None, *, port: int = 1502) -> int:
    args = sys.argv[1:] if args is None else args
    if len(args) not in (0, 2) or (len(args) == 2 and args[0] != "write-rpm"):
        print("Usage: python inspect_client.py [write-rpm 1500]")
        return 2

    try:
        with socket.create_connection(("127.0.0.1", port), timeout=3) as conn:
            pdu = exchange(conn, 1, 3, 0, 3)
            if len(pdu) != 8 or pdu[1] != 6:
                raise ValueError("Invalid FC03 byte count")
            temp, pressure, rpm = struct.unpack(">HHH", pdu[2:])
            print(f"Temperature {temp/10:.1f} °C; pressure {pressure/10:.1f} kPa; RPM {rpm}")

            if len(args) == 2:
                speed = int(args[1])
                if not 0 <= speed <= 3000:
                    raise ValueError("Training RPM range is 0..3000")
                ack = exchange(conn, 2, 6, 2, speed)
                if ack != b"\x06" + struct.pack(">HH", 2, speed):
                    raise ValueError("FC06 echoed value differs")
                verified = exchange(conn, 3, 3, 2, 1)
                if verified != b"\x03\x02" + struct.pack(">H", speed):
                    raise ValueError("Read-back RPM differs")
                print(f"Mock RPM confirmed: {speed}")
    except (ConnectionError, OSError, ValueError) as exc:
        print(f"Communication failure: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
