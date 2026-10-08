import unittest
from crc16 import crc16_modbus, make_rtu_frame, valid_rtu_frame


class TestCrc16(unittest.TestCase):
    def test_known_vector(self):
        original = bytes.fromhex("01 03 00 00 00 0A")
        self.assertEqual(crc16_modbus(original), 0xCDC5)
        self.assertEqual(make_rtu_frame(original).hex(" ").upper(),
                         "01 03 00 00 00 0A C5 CD")

    def test_corrupted_packet(self):
        frame = make_rtu_frame(bytes.fromhex("01 03 00 00 00 0A"))
        self.assertTrue(valid_rtu_frame(frame))
        corrupted = bytearray(frame)
        corrupted[3] ^= 0x01
        self.assertFalse(valid_rtu_frame(corrupted))

    def test_short_packet_is_invalid(self):
        self.assertFalse(valid_rtu_frame(b"\x01\x03\x00"))


if __name__ == "__main__":
    unittest.main()
