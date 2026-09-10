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

    def test_invalid_and_out_of_range_columns_fail_closed(self):
        for column in (-1, 1.0, True):
            with self.subTest(column=column), self.assertRaisesRegex(ValueError, 'Invalid source column'):
                utf16_column('ab', column, 'utf8')
        for encoding, column in (('unicode', 3), ('utf8', 3), ('utf16', 3)):
            with self.subTest(encoding=encoding), self.assertRaisesRegex(ValueError, 'outside line'):
                utf16_column('ab', column, encoding)
        with self.assertRaisesRegex(ValueError, 'Unknown column encoding'):
            utf16_column('ab', 1, 'latin1')

    def test_malformed_empty_and_reversed_spans_fail_closed(self):
        for span in ((1, 0, 1), (1, 0, 1, '5')):
            with self.subTest(span=span), self.assertRaisesRegex(ValueError, 'Invalid source span'):
                contains(span, 1, 0)
        for span in ((1, 5, 1, 5), (2, 0, 1, 9)):
            with self.subTest(span=span), self.assertRaisesRegex(ValueError, 'Empty or reversed'):
                contains(span, 1, 0)

    def test_half_open_adjacent_callables_do_not_share_boundary(self):
        left = (1, 0, 1, 5)
        right = (1, 5, 1, 10)
        self.assertTrue(contains(left, 1, 4))
        self.assertFalse(contains(left, 1, 5))
        self.assertTrue(contains(right, 1, 5))
        with self.assertRaises(ValueError):
            contains((0, 0, 1, 5), 1, 2)

