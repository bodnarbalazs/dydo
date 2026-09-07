"""Source spans have one encoding across collectors and mutation engines."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from positions import canonical_text, utf16_column, contains


class PositionTests(unittest.TestCase):
    def test_python_byte_offsets_normalize_bmp_and_astral_characters(self):
        line = 'é😀x = lambda: 1'
        self.assertEqual(3, utf16_column(line, 6, "utf8"))
        self.assertEqual(3, utf16_column(line, 2, "unicode"))
        self.assertEqual(3, utf16_column(line, 3, "utf16"))

    def test_offsets_inside_an_encoded_character_fail_closed(self):
        for encoding, column in (("utf8", 1), ("utf8", 3), ("utf16", 2)):
            with self.subTest(encoding=encoding, column=column):
                with self.assertRaises(ValueError):
                    utf16_column('é😀x', column, encoding)

    def test_bom_and_crlf_preserve_canonical_lines(self):
        self.assertEqual('é😀\r\nx\n', canonical_text(b'\xef\xbb\xbf' + 'é😀\r\nx\n'.encode()))
        with self.assertRaises(ValueError):
            canonical_text(b'\xff')

    def test_half_open_adjacent_callables_do_not_share_boundary(self):
        left = (1, 0, 1, 5)
        right = (1, 5, 1, 10)
        self.assertTrue(contains(left, 1, 4))
        self.assertFalse(contains(left, 1, 5))
        self.assertTrue(contains(right, 1, 5))
        with self.assertRaises(ValueError):
            contains((0, 0, 1, 5), 1, 2)

