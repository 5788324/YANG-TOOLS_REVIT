#!/usr/bin/env python3
"""Source-only guard: reject line breaks in ordinary C# quoted strings.

This is a narrow lexical safety check, NOT a C# compiler or a proof of build.
Designed for the source-level error exposed by the RC3C Windows build evidence.
"""
from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[1]


def find_unterminated_regular_strings(source):
    """Report physical newlines inside regular (non-verbatim, non-raw) strings."""
    issues = []
    n = len(source)
    i = 0
    line = 1

    while i < n:
        ch = source[i]
        if ch == '\n':
            line += 1
            i += 1
            continue
        if ch == '/' and i + 1 < n and source[i + 1] == '/':
            end = source.find('\n', i + 2)
            i = n if end == -1 else end
            continue
        if ch == '/' and i + 1 < n and source[i + 1] == '*':
            end = source.find('*/', i + 2)
            if end == -1:
                issues.append((line, 'unterminated block comment'))
                break
            line += source.count('\n', i, end + 2)
            i = end + 2
            continue
        if ch == "'":
            start_line = line
            i += 1
            while i < n:
                if source[i] == '\\':
                    i += 2
                elif source[i] == "'":
                    i += 1
                    break
                elif source[i] in '\r\n':
                    issues.append((start_line, 'newline inside character literal'))
                    break
                else:
                    i += 1
            continue

        # A C# string may start with @, $, $@, @$, or multiple $ for raw strings.
        if ch not in ('@', '$', '"'):
            i += 1
            continue
        start = i
        prefix = ''
        while i < n and source[i] in '@$':
            prefix += source[i]
            i += 1
        if i >= n or source[i] != '"':
            i = start + 1
            continue
        # Avoid identifying suffixes of identifiers as independent prefixes.
        if start > 0 and (source[start - 1].isalnum() or source[start - 1] == '_'):
            if prefix:
                i = start + 1
                continue
        start_line = line
        delimiter_len = 0
        while i + delimiter_len < n and source[i + delimiter_len] == '"':
            delimiter_len += 1
        if delimiter_len >= 3:
            # Raw literal: physical newlines are legal. Any number of leading $ is allowed.
            end_marker = '"' * delimiter_len
            end = source.find(end_marker, i + delimiter_len)
            if end == -1:
                issues.append((start_line, 'unterminated raw string'))
                break
            line += source.count('\n', start, end + delimiter_len)
            i = end + delimiter_len
            continue

        i += 1
        verbatim = '@' in prefix
        closed = False
        while i < n:
            if source[i] == '"':
                if verbatim and i + 1 < n and source[i + 1] == '"':
                    i += 2
                    continue
                i += 1
                closed = True
                break
            if not verbatim and source[i] == '\\':
                i += 2
                continue
            if source[i] == '\n':
                if not verbatim:
                    issues.append((start_line, 'newline inside ordinary string literal'))
                    break
                line += 1
            elif source[i] == '\r' and not verbatim:
                issues.append((start_line, 'newline inside ordinary string literal'))
                break
            i += 1
        if not closed and i >= n:
            issues.append((start_line, 'unterminated string literal'))
    return issues


class LexicalRegression(unittest.TestCase):
    def test_detects_rc3c_regression(self):
        broken = 'TaskDialog.Show("提示", "信息中断。\n" + name);'
        self.assertTrue(find_unterminated_regular_strings(broken))

    def test_safe_escaped_newline(self):
        self.assertEqual([], find_unterminated_regular_strings('var s = "line 1\\nline 2";'))

    def test_allows_verbatim_multiline(self):
        self.assertEqual([], find_unterminated_regular_strings('var s = @"first\nsecond ""quote""";'))

    def test_allows_raw_multiline(self):
        self.assertEqual([], find_unterminated_regular_strings('var s = """\nhello\nworld\n""";'))

    def test_ignores_commented_invalid_string(self):
        self.assertEqual([], find_unterminated_regular_strings('// "broken\n/* "broken\ninside block" */\nvar ok = "yes";'))

    def test_handles_escaped_quotes(self):
        self.assertEqual([], find_unterminated_regular_strings('var s = "\\\"escaped\\\"";'))

    def test_compile_only_script_windows_powershell_5_1_encoding(self):
        # Windows PowerShell 5.1 reads UTF-8 without BOM using the legacy ANSI code page.
        path = ROOT / 'tools' / 'Invoke-RC3C-WindowsCompileOnly.ps1'
        data = path.read_bytes()
        self.assertTrue(data.startswith(b'\xef\xbb\xbf'), 'UTF-8 BOM required for Chinese PowerShell script on PS 5.1')
        self.assertIn('DeployAfterBuild=false', data.decode('utf-8-sig'))

    def test_all_project_csharp_sources(self):
        files = list((ROOT / 'src').rglob('*.cs'))
        self.assertGreater(len(files), 200)
        failures = []
        for file in files:
            try:
                text = file.read_text(encoding='utf-8-sig')
            except UnicodeDecodeError as error:
                failures.append(f'{file.relative_to(ROOT)}: bad UTF-8: {error}')
                continue
            for line, error in find_unterminated_regular_strings(text):
                failures.append(f'{file.relative_to(ROOT)}:{line}: {error}')
        self.assertFalse(failures, '\n'.join(failures[:35]))


if __name__ == '__main__':
    unittest.main(verbosity=1)
