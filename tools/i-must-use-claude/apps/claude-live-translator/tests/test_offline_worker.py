import ast
import importlib.util
import io
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'backend'))
PATH = ROOT / "backend" / "offline_worker.py"
spec = importlib.util.spec_from_file_location("offline_worker", PATH)
o = importlib.util.module_from_spec(spec)
spec.loader.exec_module(o)
REAL = "--real" in sys.argv
if REAL:
    sys.argv.remove("--real")


def block_network(event, args):
    if event in ("socket.connect", "socket.connect_ex", "socket.getaddrinfo", "socket.sendto"):
        raise AssertionError("Network activity is forbidden in offline tests")


sys.addaudithook(block_network)


class OfflineTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='whale-offline-test-')
        self.addCleanup(temporary.cleanup)
        override = mock.patch.dict(os.environ, {'DAFEIYU_DATA_DIR': temporary.name})
        override.start()
        self.addCleanup(override.stop)

    def test_worker_does_not_import_network_or_google_backend(self):
        imports = []
        for node in ast.walk(ast.parse(PATH.read_text(encoding="utf-8-sig"))):
            if isinstance(node, ast.Import):
                imports.extend(alias.name.split(".")[0] for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imports.append(node.module.split(".")[0])
        self.assertFalse(set(imports) & {"urllib", "requests", "http", "socket", "google_worker"})

    def test_direction_aliases_and_unsupported_targets(self):
        for direction in ("en_zh", "auto:zh-CN", "pair:en:zh"):
            self.assertEqual(o.normalize_direction(direction), "en_zh")
        for direction in ("zh_en", "auto:en", "pair:zh:en"):
            self.assertEqual(o.normalize_direction(direction), "zh_en")
        for direction in ("auto:ja", "auto:zh-TW", "auto:fr", None, [], {}, 1, ""):
            with self.subTest(direction=direction):
                transport = mock.Mock()
                with self.assertRaisesRegex(o.TranslationError, "^offline_unsupported_target$"):
                    o.OfflineTranslator(transport).translate("private text", direction)
                transport.assert_not_called()
        self.assertEqual(o.normalize_direction('pair:en:fr'), 'pair:en:fr')
        self.assertEqual(o.normalize_direction('pair:de:en'), 'pair:de:en')

    def test_lazy_ready_and_offline_protocol(self):
        output = io.StringIO()
        with mock.patch.object(o.sys, "stdin", io.StringIO('')):
            with mock.patch.object(o.sys, "stdout", output):
                with mock.patch.object(o.ModelTransport, "_load", side_effect=AssertionError("eager load")):
                    self.assertEqual(o.main(), 0)
        ready = json.loads(output.getvalue())
        self.assertTrue(ready["ready"] and ready["offline"])
        self.assertFalse(ready["modelsLoaded"])
        self.assertEqual(ready["installed_pairs"], [])

    def test_mixed_language_is_translated_in_both_directions(self):
        for source, direction, expected, supplied in (("好的，I will check the file.", "auto:zh-CN", "好的，translation", "I will check the file."),
                                  ("Hello，请保留原始文件。", "auto:en", "translation", "Hello，请保留原始文件。")):
            calls = []
            engine = o.OfflineTranslator(lambda text, pair: calls.append((text, pair)) or "translation")
            self.assertEqual(engine.translate(source, direction), expected)
            self.assertEqual(calls, [(supplied, o.normalize_direction(direction))])

    def test_already_target_and_protected_text_need_no_model(self):
        transport = mock.Mock(side_effect=AssertionError("model not needed"))
        engine = o.OfflineTranslator(transport)
        for text, direction in (("已经是中文。", "auto:zh-CN"), ("Already English.", "auto:en"),
                                ("`literal_code()` https://example.com/路径\n123 🙂", "en_zh")):
            self.assertEqual(engine.translate(text, direction), text)

    def test_lines_code_and_urls_are_preserved(self):
        source = ('Hello title\r\n\r\n- Hello `literal()`\r\n'
                  'Hello https://example.com/路径?q=1.\r\n'
                  '```python\r\nHello_secret = 1\r\n```not_a_close\r\n'
                  'private_code = 2\r\n```\r\n'
                  '    Hello_indented = 3\r\nHello end\n')
        calls = []
        def transport(text, direction):
            calls.append(text)
            return text.replace("Hello", "你好")
        result = o.OfflineTranslator(transport).translate(source, "auto:zh-CN")
        expected = source.replace("Hello title", "你好 title").replace("- Hello `", "- 你好 `")
        expected = expected.replace("Hello https", "你好 https").replace("Hello end", "你好 end")
        self.assertEqual(result, expected)
        for text in calls:
            self.assertFalse(any(private in text for private in ("literal", "example.com", "secret", "private_code", "indented")))

    def test_tilde_and_long_fences_do_not_close_early(self):
        calls = []
        source = '~~~~text\n~~~\nprivate_code\n~~~~\nHello\n'
        result = o.OfflineTranslator(lambda text, _: calls.append(text) or text).translate(source, "en_zh")
        self.assertEqual(result, source)
        self.assertEqual(calls, ["Hello"])

    def test_url_does_not_swallow_following_chinese_and_english(self):
        calls = []
        source = 'https://example.com请保留原始文件 and check the result.'
        def transport(text, direction):
            calls.append((text, direction))
            return 'Please keep the original file and check the result.'
        result = o.OfflineTranslator(transport).translate(source, 'auto:en')
        self.assertEqual(result, 'https://example.comPlease keep the original file and check the result.')
        self.assertEqual(calls, [('请保留原始文件 and check the result.', 'zh_en')])
        self.assertEqual([source[a:b] for a, b in o.protected_spans(source)], ['https://example.com'])

    def test_model_line_breaks_cannot_replace_source_line_structure(self):
        engine = o.OfflineTranslator(lambda text, _: "one\ntwo")
        self.assertEqual(engine.translate("Hello\r\nWorld\r\n", "en_zh"), "one two\r\none two\r\n")

    def test_long_input_has_no_dropped_tail(self):
        source = ("Long source sentence. " * 500) + "END_SENTINEL\n"
        calls = []
        result = o.OfflineTranslator(lambda text, _: calls.append(text) or text).translate(source, "en_zh")
        self.assertEqual(result, source)
        self.assertTrue(all(len(text) <= o.MAX_CHUNK for text in calls))

    def test_cache_is_direction_specific_and_bounded(self):
        calls = []
        engine = o.OfflineTranslator(lambda text, direction: calls.append(direction) or text)
        for direction in ("en_zh", "auto:zh-CN", "zh_en", "auto:en"):
            self.assertEqual(engine.translate("Hello 中文", direction), "Hello 中文")
        self.assertEqual(calls, ["en_zh", "zh_en"])
        for number in range(205):
            engine.translate("Hello " + str(number), "en_zh")
        self.assertLessEqual(len(engine.cache), 200)
        self.assertLessEqual(engine.cache_chars, o.MAX_CACHE_CHARS)

    def test_missing_models_fail_explicitly_without_download(self):
        transport = o.ModelTransport()
        with self.assertRaisesRegex(o.TranslationError, "^offline_model_missing$"):
            transport("Hello", "en_zh")
        self.assertEqual(transport.engines, {})

    def test_explicit_other_language_pair_uses_that_model_without_guessing(self):
        calls = []
        engine = o.OfflineTranslator(lambda text, direction: calls.append((text, direction)) or 'Bonjour')
        self.assertEqual(engine.translate('Hello', 'pair:en:fr'), 'Bonjour')
        self.assertEqual(calls, [('Hello', 'pair:en:fr')])
        self.assertEqual(engine.translate('Привет', 'pair:ru:en'), 'Bonjour')
        self.assertEqual(calls[-1], ('Привет', 'pair:ru:en'))

    def test_token_chunks_pass_every_source_token(self):
        tokenizer = mock.Mock()
        tokenizer.encode.side_effect = lambda text, out_type: list(text)
        tokenizer.decode.side_effect = lambda tokens: "".join(tokens)
        translator = mock.Mock()
        translator.translate_batch.side_effect = lambda batches, **kw: [mock.Mock(hypotheses=[batches[0]])]
        transport = o.ModelTransport()
        transport.engines["en_zh"] = translator, tokenizer
        source = "a" * 250 + "FINAL"
        self.assertEqual(transport(source, "en_zh"), source)
        supplied = [call.args[0][0] for call in translator.translate_batch.call_args_list]
        self.assertEqual("".join(token for chunk in supplied for token in chunk), source)
        self.assertTrue(all(len(chunk) <= o.MAX_INPUT_TOKENS for chunk in supplied))
        self.assertTrue(all(call.kwargs["max_input_length"] == 0 for call in translator.translate_batch.call_args_list))

    def test_invalid_request_and_errors_never_log_content(self):
        requests = '[]\n' + json.dumps({"id": 1, "text": "private secret", "direction": "auto:ja"}) + '\n'
        requests += json.dumps({"id": 2, "text": "private secret", "direction": "en_zh"}) + '\n'
        output, errors = io.StringIO(), io.StringIO()
        with mock.patch.object(o.sys, "stdin", io.StringIO(requests)):
            with mock.patch.object(o.sys, "stdout", output), mock.patch.object(o.sys, "stderr", errors):
                with mock.patch.object(o.ModelTransport, "__call__", side_effect=RuntimeError("private secret")):
                    o.main()
        responses = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual([row.get("error") for row in responses[1:]],
                         ["invalid_request", "offline_unsupported_target", "translation_failed"])
        self.assertTrue(all(row["offline"] for row in responses))
        self.assertNotIn("private secret", output.getvalue() + errors.getvalue())
        self.assertEqual(errors.getvalue(), "")

    def test_text_limit_precedes_model_load(self):
        transport = mock.Mock()
        with self.assertRaisesRegex(o.TranslationError, "^text_too_long$"):
            o.OfflineTranslator(transport).translate("a" * 32001, "en_zh")
        transport.assert_not_called()


@unittest.skipUnless(REAL, "Use --real for bundled CPU model checks; never contacts a service")
class RealOfflineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import package_manager
        cls.temporary = tempfile.TemporaryDirectory(prefix='whale-installed-real-test-')
        cls.override = mock.patch.dict(os.environ, {'DAFEIYU_DATA_DIR': cls.temporary.name})
        cls.override.start()
        try:
            for name in ('translate-en_zh-1_9.argosmodel', 'translate-zh_en-1_9.argosmodel'):
                package_manager.import_package(ROOT / 'models' / name)
        except Exception:
            cls.override.stop()
            cls.temporary.cleanup()
            raise

    @classmethod
    def tearDownClass(cls):
        cls.override.stop()
        cls.temporary.cleanup()

    def test_actual_utf8_stdio_protocol_from_unrelated_directory(self):
        wrapper = ('import sys,runpy,pathlib\n'
                   'sys.path.insert(0,str(pathlib.Path(sys.argv[1]).parent))\n'
                   'def guard(event,args):\n'
                   '    if event in ("socket.connect","socket.connect_ex","socket.getaddrinfo","socket.sendto"): raise RuntimeError("network forbidden")\n'
                   'sys.addaudithook(guard)\n'
                   'runpy.run_path(sys.argv[1],run_name="__main__")\n')
        requests = [{'id': 1, 'text': 'Please keep the original file.', 'direction': 'auto:zh-CN'},
                    {'id': 2, 'text': '请保留原始文件。', 'direction': 'auto:en'},
                    {'id': 3, 'text': 'Private fixed sample', 'direction': 'auto:ja'}]
        result = subprocess.run([sys.executable, '-B', '-c', wrapper, str(PATH)],
            input='\n'.join(json.dumps(row, ensure_ascii=False) for row in requests) + '\n',
            capture_output=True, encoding='utf-8', timeout=45, cwd=tempfile.gettempdir())
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stderr, '')
        replies = [json.loads(row) for row in result.stdout.splitlines()]
        self.assertTrue(replies[0]['ready'] and replies[0]['offline'])
        self.assertEqual([row['id'] for row in replies[1:]], [1, 2, 3])
        self.assertTrue(all(row['offline'] for row in replies))
        self.assertRegex(replies[1]['translation'], r'[\u3400-\u9fff]')
        self.assertIn('original', replies[2]['translation'].lower())
        self.assertEqual(replies[3]['error'], 'offline_unsupported_target')

    def test_fixed_bilingual_and_mixed_samples(self):
        engine = o.OfflineTranslator()
        samples = (("Please keep the original file.", "auto:zh-CN"),
                   ("请保留原始文件。", "auto:en"),
                   ("好的，I will check the file.", "auto:zh-CN"),
                   ("Hello，请保留原始文件。", "auto:en"))
        for source, direction in samples:
            result = engine.translate(source, direction)
            self.assertNotEqual(result, source)
            self.assertTrue(result.strip())
            self.assertRegex(result, r"[\u3400-\u9fff]" if direction == "auto:zh-CN" else r"[A-Za-z]")
            self.assertNotRegex(result, r"[\ue000-\uf8ff\ufffd▁]")
            if source.startswith("好的"):
                self.assertTrue(result.startswith("好的，"))
                self.assertNotIn("I will check", result)
            print(json.dumps({"fixed_sample": source, "direction": direction, "result": result}, ensure_ascii=False))
        source = 'Please keep `original.txt`.\r\nVisit https://example.com/a?q=1.\r\n```\r\nsecret_code()\r\n```\r\n'
        result = engine.translate(source, "auto:zh-CN")
        self.assertEqual(result.count("\r\n"), source.count("\r\n"))
        for protected in ("`original.txt`", "https://example.com/a?q=1", "```\r\nsecret_code()\r\n```\r\n"):
            self.assertIn(protected, result)
        result = engine.translate('https://example.com请保留原始文件 and check the result.', 'auto:en')
        self.assertTrue(result.startswith('https://example.com'))
        self.assertNotRegex(result, r'[\u3400-\u9fff]')
        self.assertIn('original', result.lower())
        self.assertEqual(set(engine.transport.engines), {"en_zh", "zh_en"})


if __name__ == "__main__":
    unittest.main()
