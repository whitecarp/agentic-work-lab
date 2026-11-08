"""Offline regression checks for the fixed Google acceptance script."""
import importlib.util
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('semantic_check', ROOT / 'tests' / 'semantic_check.py')
semantic = importlib.util.module_from_spec(spec)
spec.loader.exec_module(semantic)


class SemanticCheckTests(unittest.TestCase):
    def test_default_dictionary_matches_csharp_defaults_exactly(self):
        source = (ROOT / 'src' / 'Glossary.cs').read_text(encoding='utf-8-sig')
        source = source.split('internal static List<GlossaryEntry> Defaults(){', 1)[1]
        source = source.split('return Validate(rows);', 1)[0]
        names = re.search(r'foreach\(string name in new\[\]\{(.*?)\}\)', source, re.S)
        self.assertIsNotNone(names, 'C# name-default syntax changed; review semantic defaults')
        self.assertEqual(tuple(re.findall(r'"([^"]+)"', names.group(1))), semantic.DEFAULT_NAMES)
        store = (ROOT / 'src' / 'Glossary.cs').read_text(encoding='utf-8-sig')
        exact = re.search(r'ExactCaseNames=\{(.*?)\};', store, re.S)
        self.assertIsNotNone(exact, 'C# exact-case name syntax changed; review semantic defaults')
        self.assertEqual(tuple(re.findall(r'"([^"]+)"', exact.group(1))), semantic.DEFAULT_EXACT_NAMES)
        for label, expected in (('phrases', semantic.DEFAULT_PHRASES), ('reverse', semantic.DEFAULT_REVERSE)):
            block = re.search(r'string\[,] ' + label + r'=\{(.*?)\};', source, re.S)
            self.assertIsNotNone(block, 'C# phrase-default syntax changed; review semantic defaults')
            self.assertEqual(tuple(re.findall(r'\{"([^"]+)","([^"]+)"\}', block.group(1))), expected)
        rows = semantic.default_glossary()
        self.assertEqual(len(rows), 135)
        self.assertEqual(len(semantic.g.validated_entries(rows)), len(rows))

    def test_old_way_accepts_and_ignores_dictionary_for_code_and_links(self):
        calls = []
        old = semantic.OldWay(lambda text, direction: calls.append(text) or '[' + text + ']', 0)
        source = 'Claude reads `settings.json` at https://example.com/setup now.'
        result = old.translate(source, 'en_zh', [dict(source='Claude', replacement='REPLACED', target='*')])
        self.assertEqual(result, '[Claude reads] `settings.json` [at] https://example.com/setup [now.]')
        self.assertEqual(calls, ['Claude reads', 'at', 'now.'])

    def test_current_way_preserves_default_names_and_complete_code_context(self):
        calls = []
        new = semantic.g.GoogleTranslator(lambda text, direction: calls.append(text) or text, 0)
        source = 'Claude and Codex read `settings.json`.'
        self.assertEqual(new.translate(source, 'en_zh', semantic.default_glossary()), source)
        self.assertEqual(calls, ['ZXQ0 and ZXQ1 read ZXQ2.'])

    def test_all_fixed_samples_can_be_compared_without_api_calls(self):
        old = semantic.OldWay(lambda text, direction: text, 0)
        new = semantic.g.GoogleTranslator(lambda text, direction: text, 0)
        glossary = semantic.default_glossary()
        self.assertEqual(len(semantic.CASES), 27)
        for case in semantic.CASES:
            with self.subTest(case=case[0]):
                new.translate(case[3], case[2], glossary)
                if semantic.OLD_PROTECTED.search(case[3]):
                    old.translate(case[3], case[2], glossary)

    def test_brand_names_are_checked_with_exact_spelling(self):
        case = next(case for case in semantic.CASES if case[0] == 'order_until')
        missing, _ = semantic.check(case, '等编解码器回复后再开始下一步。')
        self.assertIn('Codex', missing)
        missing, _ = semantic.check(case, '等 Codex 回复后再开始下一步。')
        self.assertNotIn('Codex', missing)


if __name__ == '__main__':
    unittest.main()
