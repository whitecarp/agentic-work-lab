import importlib.util
import io
import pathlib
import unittest
from unittest import mock

path = pathlib.Path(__file__).resolve().parents[1] / 'backend' / 'google_worker.py'
spec = importlib.util.spec_from_file_location('google_worker', path)
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)

class GoogleTests(unittest.TestCase):
    def test_google_output_zero_width_artifacts_removed_without_editing_request(self):
        source = 'Original\u200b text\ufeff remains untouched.'
        response = mock.MagicMock()
        response.status = 200
        response.read.return_value = g.json.dumps(
            [[['中文\u200b', source], ['\ufeff译文\u200c\u200d', None]]]
        ).encode('utf-8')
        response.__enter__.return_value = response
        opener = mock.Mock()
        opener.open.return_value = response
        with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
            self.assertEqual(g.call_google(source, 'en_zh'), '中文译文\u200c\u200d')
        request = opener.open.call_args.args[0]
        self.assertEqual(g.urllib.parse.parse_qs(request.data.decode('utf-8'))['q'], [source])

    def test_source_zero_width_characters_survive_untranslated_and_protected_content(self):
        transport = mock.Mock(side_effect=lambda text, direction: text)
        translator = g.GoogleTranslator(transport, 0)
        untouched = '原文\u200b保留\ufeff。'
        self.assertEqual(translator.translate(untouched, 'en_zh'), untouched)
        transport.assert_not_called()
        protected = '`name\u200b\ufeff.txt`\n\n```\nkeep\u200b\ufeff()\n```\n'
        self.assertEqual(translator.translate(protected, 'en_zh'), protected)
        transport.assert_not_called()

    def test_worker_ready_handshake_does_not_claim_google_is_online(self):
        # The startup handshake confirms only the local worker process is ready.
        output = io.StringIO()
        with mock.patch.object(g.sys, 'stdin', io.StringIO('')):
            with mock.patch.object(g.sys, 'stdout', output):
                g.main()
        self.assertEqual(output.getvalue().splitlines()[0], '{"ready": true}')

    def test_http_407_maps_to_proxy_authentication_without_details(self):
        error = g.urllib.error.HTTPError(g.ENDPOINT, 407, 'Proxy Authentication Required', {}, None)
        opener = mock.Mock()
        opener.open.side_effect = error
        with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
            with self.assertRaises(g.TranslationError) as caught:
                g.call_google('private text', 'en_zh')
        self.assertEqual(str(caught.exception), 'proxy_authentication')
        self.assertNotIn('private text', str(caught.exception))
        self.assertNotIn(g.ENDPOINT, str(caught.exception))

    def test_explicit_407_tunnel_error_maps_to_proxy_authentication(self):
        opener = mock.Mock()
        opener.open.side_effect = g.urllib.error.URLError('Tunnel connection failed: 407 Proxy Authentication Required')
        with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
            with self.assertRaises(g.TranslationError) as caught:
                g.call_google('private text', 'en_zh')
        self.assertEqual(str(caught.exception), 'proxy_authentication')

    def test_non_407_network_error_stays_generic(self):
        opener = mock.Mock()
        opener.open.side_effect = g.urllib.error.URLError(ConnectionRefusedError('connection refused'))
        with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
            with self.assertRaises(g.TranslationError) as caught:
                g.call_google('private text', 'en_zh')
        self.assertEqual(str(caught.exception), 'network_error')

    def test_google_request_uses_explicit_source_language_for_each_direction(self):
        for direction, expected_source, expected_target in (
            ('en_zh', 'en', 'zh-CN'),
            ('zh_en', 'zh-CN', 'en'),
        ):
            response = mock.MagicMock()
            response.status = 200
            response.read.return_value = b'[[["translated","source",null,null,1]] ]'
            response.__enter__.return_value = response
            response.__exit__.return_value = False
            opener = mock.Mock()
            opener.open.return_value = response
            with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
                self.assertEqual(g.call_google('mixed language sample', direction), 'translated')
            request = opener.open.call_args.args[0]
            query = g.urllib.parse.parse_qs(g.urllib.parse.urlsplit(request.full_url).query)
            self.assertEqual(query['sl'], [expected_source])
            self.assertEqual(query['tl'], [expected_target])

    def test_automatic_source_and_each_supported_target_reach_google(self):
        expected = {'zh-CN', 'zh-TW', 'en', 'ja', 'ko', 'fr', 'de', 'es', 'it', 'pt',
                    'ru', 'ar', 'hi', 'th', 'vi', 'id', 'ms'}
        self.assertEqual(g.TARGET_LANGUAGES, expected)
        for target in sorted(expected):
            with self.subTest(target=target):
                response = mock.MagicMock()
                response.status = 200
                response.read.return_value = b'[[["translated","source",null,null,1]]]'
                response.__enter__.return_value = response
                response.__exit__.return_value = False
                opener = mock.Mock()
                opener.open.return_value = response
                with mock.patch.object(g.urllib.request, 'build_opener', return_value=opener):
                    self.assertEqual(g.call_google('Source text & symbols', 'auto:' + target), 'translated')
                request = opener.open.call_args.args[0]
                query = g.urllib.parse.parse_qs(g.urllib.parse.urlsplit(request.full_url).query)
                self.assertEqual(query['sl'], ['auto'])
                self.assertEqual(query['tl'], [target])
                self.assertNotIn('q', query)
                self.assertEqual(g.urllib.parse.parse_qs(request.data.decode('utf-8'))['q'], ['Source text & symbols'])

    def test_invalid_target_or_direction_is_rejected_before_transport(self):
        invalid = (None, [], {}, 123, '', 'auto:', 'auto:auto', 'auto:xx', 'auto:en&sl=ja',
                   'auto:zh_CN', 'auto:EN', ' auto:en', 'auto:en ', 'auto:en\n', 'en_fr')
        for direction in invalid:
            with self.subTest(direction=direction):
                transport = mock.Mock()
                translator = g.GoogleTranslator(transport, 0)
                with self.assertRaisesRegex(g.TranslationError, '^invalid_direction$'):
                    translator.translate('Text', direction)
                transport.assert_not_called()
                with mock.patch.object(g.urllib.request, 'build_opener') as opener:
                    with self.assertRaisesRegex(g.TranslationError, '^invalid_direction$'):
                        g.call_google('Text', direction)
                    opener.assert_not_called()

    def test_automatic_source_accepts_non_latin_and_non_chinese_scripts(self):
        samples = ('こんにちは', '안녕하세요', 'Привет', 'مرحبا', 'नमस्ते', 'สวัสดี',
                   '你好', 'Bonjour', 'Hola', '你好 — 안녕하세요 — Hello')
        for source in samples:
            with self.subTest(source=source):
                calls = []
                translator = g.GoogleTranslator(lambda text, direction: calls.append((text, direction)) or 'translated', 0)
                self.assertEqual(translator.translate(source, 'auto:en'), 'translated')
                self.assertEqual(calls, [(source, 'auto:en')])

    def test_automatic_source_does_not_guess_that_target_language_needs_no_request(self):
        calls = []
        translator = g.GoogleTranslator(lambda text, direction: calls.append((text, direction)) or text, 0)
        self.assertEqual(translator.translate('Hello', 'auto:en'), 'Hello')
        self.assertEqual(calls, [('Hello', 'auto:en')])

    def test_automatic_targets_and_legacy_directions_have_separate_caches(self):
        calls = []
        translator = g.GoogleTranslator(lambda text, direction: calls.append(direction) or direction, 0)
        for direction in ('auto:zh-CN', 'auto:zh-TW', 'auto:ja', 'auto:en', 'en_zh', 'zh_en'):
            self.assertEqual(translator.translate('Hello 中文', direction), direction)
            self.assertEqual(translator.translate('Hello 中文', direction), direction)
        self.assertEqual(calls, ['auto:zh-CN', 'auto:zh-TW', 'auto:ja', 'auto:en', 'en_zh', 'zh_en'])

    def test_automatic_mode_keeps_crlf_paragraphs_lists_code_and_links(self):
        calls = []
        source = ('Hello title\r\n\r\n'
                  '- Hello `literal_code()`\r\n'
                  '- Hello https://example.com/path?q=1\r\n\r\n'
                  '```python\r\nprivate_code = 1\r\n```\r\n'
                  '    private_indented = 2\r\n\r\nHello end\r\n')
        def translate(text, direction):
            calls.append((text, direction))
            return text.replace('Hello', 'Bonjour')
        translator = g.GoogleTranslator(translate, 0)
        expected = source.replace('Hello', 'Bonjour')
        self.assertEqual(translator.translate(source, 'auto:fr'), expected)
        for text, direction in calls:
            self.assertEqual(direction, 'auto:fr')
            for protected in ('literal_code', 'example.com', 'private_code', 'private_indented'):
                self.assertNotIn(protected, text)

    def test_automatic_mode_protected_only_and_punctuation_make_no_request(self):
        calls = []
        translator = g.GoogleTranslator(lambda text, direction: calls.append(text) or text, 0)
        source = '`literal`, https://example.com/test\n\n1234 — ! ? 🙂\n'
        self.assertEqual(translator.translate(source, 'auto:ja'), source)
        self.assertEqual(calls, [])

    def test_automatic_mode_token_fallback_keeps_non_latin_source_and_protected_text(self):
        calls = []
        def translate(text, direction):
            calls.append(text)
            return 'missing placeholder' if 'ZXQ' in text else '[' + text + ']'
        translator = g.GoogleTranslator(translate, 0)
        self.assertEqual(translator.translate('こんにちは `sample` مرحبا', 'auto:en'),
                         '[こんにちは] `sample` [مرحبا]')
        self.assertEqual(calls, ['こんにちは ZXQ0 مرحبا', 'こんにちは', 'مرحبا'])

    def test_whole_paragraph_and_cache(self):
        calls = []
        translator = g.GoogleTranslator(lambda text, direction: calls.append((text, direction)) or '完整段落译文', 0)
        source = 'Could you take a look? Please keep the original file.'
        self.assertEqual(translator.translate(source, 'en_zh'), '完整段落译文')
        translator.translate(source, 'en_zh')
        self.assertEqual(calls, [(source, 'en_zh')])

    def test_code_and_url_preserved(self):
        calls = []
        translator = g.GoogleTranslator(lambda t,d: calls.append(t) or t, 0)
        source = '```python\nprint("hi")\n```\n    private_code = 1\nVisit https://example.com/a.b?q=1 and use `some_code()`.\n'
        self.assertEqual(translator.translate(source, 'en_zh'), source)
        self.assertFalse(any('private_code' in x or 'example.com' in x or 'some_code()' in x for x in calls))

    def test_paragraph_and_list_newlines_survive_with_protected_code(self):
        calls = []
        translator = g.GoogleTranslator(lambda text, direction: calls.append(text) or text, 0)
        source = (
            'First paragraph.\n\n'
            'Second paragraph.\n\n'
            '- Keep `literal_code()` unchanged.\n'
            '- Keep `config.json` unchanged.\n'
        )
        self.assertEqual(translator.translate(source, 'en_zh'), source)
        self.assertIn('\n\n', source)
        self.assertTrue(any('ZXQ0' in text and '\n- Keep ZXQ1' in text for text in calls))

    def test_mixed_language_is_not_skipped(self):
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or '好的，我会检查文件。',0)
        translator.translate('好的，I will check the file.', 'en_zh')
        self.assertEqual(len(calls),1)

    def test_fence_looking_code_stays_private(self):
        for marker in ('```', '~~~'):
            calls=[]
            translator=g.GoogleTranslator(lambda t,d: calls.append(t) or t,0)
            source=marker + 'text\n' + marker + 'not_a_closing_fence\nprivate_code = 1\n' + marker + '\nTranslate this sentence.'
            self.assertEqual(translator.translate(source,'en_zh'),source)
            self.assertEqual(calls,['Translate this sentence.'])

    def test_long_source_has_no_loss(self):
        source=('A sentence that should remain intact.\n' * 200) + 'END_SENTINEL'
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or t,0)
        self.assertEqual(translator.translate(source,'en_zh'),source)
        self.assertTrue(all(len(x)<=g.MAX_CHUNK for x in calls))

    def test_rate_limit_no_fallback_or_retry(self):
        calls=[]
        def fail(t,d):
            calls.append(t)
            raise g.TranslationError('rate_limited')
        translator=g.GoogleTranslator(fail,0)
        with self.assertRaisesRegex(g.TranslationError,'rate_limited'):
            translator.translate('Hello there.','en_zh')
        self.assertEqual(len(calls),1)
        self.assertEqual(len(translator.cache),0)

    def test_directions_are_isolated(self):
        translator=g.GoogleTranslator(lambda t,d:d,0)
        self.assertEqual(translator.translate('English 中文','en_zh'),'en_zh')
        self.assertEqual(translator.translate('English 中文','zh_en'),'zh_en')

    # 0.3: a sentence with links or inline code is sent whole, with placeholders.
    def test_sentence_with_code_and_url_is_one_request(self):
        calls=[]
        def fake(t,d):
            calls.append(t)
            return '在空白窗口中打开 ZXQ0，然后查看 ZXQ1。'
        translator=g.GoogleTranslator(fake,0)
        source='Open `settings.json` in the empty window, then check https://example.com/x.'
        self.assertEqual(translator.translate(source,'en_zh'),'在空白窗口中打开 `settings.json`，然后查看 https://example.com/x。')
        self.assertEqual(calls,['Open ZXQ0 in the empty window, then check ZXQ1.'])
        self.assertEqual(translator.fallbacks,0)

    def test_token_order_case_and_spacing_tolerated(self):
        translator=g.GoogleTranslator(lambda t,d:'先 zxq 1，再 ZXQ0。',0)
        self.assertEqual(translator.translate('Use `a` after `b`.','en_zh'),'先 `b`，再 `a`。')

    def test_lost_placeholder_falls_back_without_losing_code(self):
        calls=[]
        def fake(t,d):
            calls.append(t)
            return '打开文件' if 'ZXQ' in t else '[' + t.strip() + ']'
        translator=g.GoogleTranslator(fake,0)
        result=translator.translate('Open `settings.json` now.','en_zh')
        self.assertEqual(result,'[Open] `settings.json` [now.]')
        self.assertEqual(calls,['Open ZXQ0 now.','Open','now.'])
        self.assertEqual(translator.fallbacks,1)

    def test_duplicated_or_invented_placeholder_falls_back(self):
        for reply in ('ZXQ0 和 ZXQ0','ZXQ0 和 ZXQ1'):
            translator=g.GoogleTranslator(lambda t,d,r=reply: r if 'ZXQ' in t else '译', 0)
            self.assertEqual(translator.translate('Keep `x` here.','en_zh'),'译 `x` 译')
            self.assertEqual(translator.fallbacks,1)

    def test_transport_error_is_not_retried_as_fragments(self):
        calls=[]
        def fail(t,d):
            calls.append(t)
            raise g.TranslationError('rate_limited')
        translator=g.GoogleTranslator(fail,0)
        with self.assertRaisesRegex(g.TranslationError,'rate_limited'):
            translator.translate('Open `a` and `b`.','en_zh')
        self.assertEqual(len(calls),1)

    def test_link_inside_chinese_does_not_swallow_sentence(self):
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or 'See ZXQ0, then tell me the result.',0)
        result=translator.translate('请看https://example.com/a，然后告诉我结果。','zh_en')
        self.assertEqual(calls,['请看ZXQ0，然后告诉我结果。'])
        self.assertEqual(result,'See https://example.com/a, then tell me the result.')

    def test_link_punctuation_and_parentheses(self):
        spans=lambda s:[s[a:b] for a,b in g.protected_spans(s)]
        self.assertEqual(spans('Read https://example.com/a.b?q=1.'),['https://example.com/a.b?q=1'])
        self.assertEqual(spans('(see www.example.com)'),['www.example.com'])
        self.assertEqual(spans('Wiki https://en.wikipedia.org/wiki/Whale_(disambiguation).'),['https://en.wikipedia.org/wiki/Whale_(disambiguation)'])
        self.assertEqual(spans('Run `npm install`, then `npm test`.'),['`npm install`','`npm test`'])

    def test_text_that_already_contains_token_uses_fragments(self):
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or t,0)
        source='Model ZXQ1 uses `cfg`.'
        self.assertEqual(translator.translate(source,'en_zh'),source)
        self.assertEqual(calls,['Model ZXQ1 uses'])

    def test_only_protected_content_makes_no_request(self):
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or t,0)
        self.assertEqual(translator.translate('`a`, `b`. https://example.com','en_zh'),'`a`, `b`. https://example.com')
        self.assertEqual(calls,[])

    def test_long_chinese_splits_at_sentence_end(self):
        calls=[]
        translator=g.GoogleTranslator(lambda t,d: calls.append(t) or t,0)
        source='这是一个没有空格的中文句子。' * 300
        self.assertEqual(translator.translate(source,'zh_en'),source)
        self.assertTrue(len(calls)>1 and all(len(x)<=g.MAX_CHUNK and x.endswith('。') for x in calls))

    def test_google_captcha_redirect_has_its_own_error(self):
        handler=g.GoogleOnlyRedirect()
        request=g.urllib.request.Request(g.ENDPOINT, data=b'q=x')
        with self.assertRaisesRegex(g.TranslationError,'google_blocked'):
            handler.redirect_request(request,None,302,'Found',{},'https://www.google.com/sorry/index?continue=x')
        with self.assertRaisesRegex(g.TranslationError,'unexpected_redirect'):
            handler.redirect_request(request,None,302,'Found',{},'https://example.com/sorry/index')
        self.assertIsNotNone(handler.redirect_request(request,None,302,'Found',{},'https://translate.googleapis.com/translate_a/single'))

if __name__ == '__main__':
    unittest.main()
