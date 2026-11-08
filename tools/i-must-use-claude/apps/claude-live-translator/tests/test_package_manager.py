import io
import json
import os
import pathlib
import stat
import sys
import tempfile
import threading
import unittest
import warnings
import zipfile
from unittest import mock

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'backend'))
import package_manager as p
import package_store as s


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='whale-packs-test-')
        self.addCleanup(self.temporary.cleanup)
        self.root = pathlib.Path(self.temporary.name)
        override = mock.patch.dict(os.environ, {'DAFEIYU_DATA_DIR': str(self.root / 'data')})
        override.start()
        self.addCleanup(override.stop)
        self.metadata = {'from_code': 'en', 'from_name': 'English', 'to_code': 'fr',
                         'to_name': 'French', 'package_version': '1.9'}

    def archive(self, extras=None, metadata=None, omit=()):
        path = self.root / ('fixture-' + str(len(list(self.root.glob('*.argosmodel')))) + '.argosmodel')
        files = {'pack/metadata.json': json.dumps(self.metadata if metadata is None else metadata),
                 'pack/sentencepiece.model': b'fixture-tokenizer', 'pack/model/model.bin': b'fixture-model',
                 'pack/model/config.json': '{}', 'pack/model/shared_vocabulary.json': '[]',
                 'pack/README.md': 'Fixture attribution and license', 'pack/LICENSE.txt': 'Fixture license',
                 'pack/stanza/unneeded.bin': b'not-used'}
        files.update(extras or {})
        with zipfile.ZipFile(path, 'w') as archive:
            for name, value in files.items():
                if name not in omit:
                    if '\\' in name:
                        entry = zipfile.ZipInfo(name.replace('\\', '/'))
                        entry.filename = name
                        archive.writestr(entry, value)
                    else:
                        archive.writestr(name, value)
        return path

    def install_fixture(self, path=None, **kwargs):
        return p.import_package(path or self.archive(), validator=lambda _: None, **kwargs)

    def test_list_is_local_without_creating_data_or_importing_model(self):
        with mock.patch.object(p, 'open_official', side_effect=AssertionError('network forbidden')):
            result = p.list_packages()
        self.assertEqual(result['installed'], [])
        self.assertEqual(result['catalog_source'], 'bundled')
        self.assertFalse(s.data_directory().exists())
        ids = {item['id'] for item in result['catalog']}
        self.assertIn('translate-en_zh-1_9', ids)
        self.assertIn('translate-en_fr-1_9', ids)
        self.assertIn('translate-de_en-1_3', ids)
        self.assertTrue(all(item['compatibility'] == 'checked_on_install' for item in result['catalog']))

    def test_exact_https_source_allowlist(self):
        self.assertTrue(p.allowed_url('https://argos-net.com/v1/translate-en_fr-1_9.argosmodel'))
        self.assertTrue(p.allowed_url(p.CATALOG_URL, True))
        bad = ('http://argos-net.com/v1/translate-en_fr-1_9.argosmodel',
               'https://argos-net.com.evil/v1/translate-en_fr-1_9.argosmodel',
               'https://secret@argos-net.com/v1/translate-en_fr-1_9.argosmodel',
               'https://argos-net.com:444/v1/translate-en_fr-1_9.argosmodel',
               'https://argos-net.com/v1/../translate-en_fr-1_9.argosmodel',
               'https://argos-net.com/v1/translate-en_fr-1_9.argosmodel?secret=1',
               'https://example.com/translate-en_fr-1_9.argosmodel')
        for url in bad:
            self.assertFalse(p.allowed_url(url), url)

    def test_atomic_install_preserves_notices_and_ignores_unused_tokenizers(self):
        stages = []
        def progress(phase, count, total):
            stages.append(phase)
            self.assertEqual(s.installed_packages(), [])
        result = self.install_fixture(progress=progress)
        self.assertEqual(result['id'], 'translate-en_fr-1_9')
        installed = s.installed_packages()
        self.assertEqual([item['pair'] for item in installed], ['pair:en:fr'])
        folder = s.packages_directory() / result['id']
        for name in ('README.md', 'LICENSE.txt', 'metadata.json', '.install.json'):
            self.assertTrue((folder / name).is_file())
        self.assertFalse((folder / 'stanza').exists())
        self.assertEqual(list((s.packages_directory() / '.staging').iterdir()), [])
        self.assertEqual(stages, ['validate', 'install'])
        self.assertEqual(s.read_json(folder / '.install.json')['origin'], 'local_import')

    def test_existing_version_is_not_overwritten(self):
        first = self.install_fixture()
        folder = s.packages_directory() / first['id']
        original = (folder / '.install.json').read_bytes()
        second = self.install_fixture()
        self.assertTrue(second['already_installed'])
        self.assertEqual((folder / '.install.json').read_bytes(), original)

    def test_explicit_reinstall_preserves_old_package_backup(self):
        first = self.install_fixture()
        old = s.packages_directory() / first['id']
        original = (old / 'README.md').read_text()
        newer = self.archive({'pack/README.md': 'Updated fixture attribution'})
        with p.staging() as stage:
            result = p.install_archive(newer, stage, reinstall=True, validator=lambda _: None)
        self.assertTrue(result['reinstalled'])
        self.assertEqual((old / 'README.md').read_text(), 'Updated fixture attribution')
        backups = list((s.packages_directory() / '.backups').iterdir())
        self.assertEqual(len(backups), 1)
        self.assertEqual((backups[0] / 'README.md').read_text(), original)
        self.assertEqual(len(s.installed_packages()), 1)

    def test_failed_reinstall_restores_previous_package(self):
        first = self.install_fixture()
        old = s.packages_directory() / first['id']
        original = (old / '.install.json').read_bytes()
        real_rename = pathlib.Path.rename
        def fail_promotion(path, destination):
            if path.name == 'package':
                raise PermissionError('fixture sharing violation')
            return real_rename(path, destination)
        with p.staging() as stage, mock.patch.object(pathlib.Path, 'rename', fail_promotion):
            with self.assertRaisesRegex(p.PackageError, '^package_busy$'):
                p.install_archive(self.archive(), stage, reinstall=True, validator=lambda _: None)
        self.assertEqual((old / '.install.json').read_bytes(), original)
        self.assertEqual(len(s.installed_packages()), 1)

    def test_interrupted_commit_recovery_restores_backup_before_cleanup(self):
        first = self.install_fixture()
        root = s.packages_directory()
        stage = root / '.staging' / ('123-' + 'd' * 32)
        stage.mkdir()
        (stage / 'owner.json').write_text(json.dumps({'pid': 123, 'identity': 'dead'}))
        (root / '.backups').mkdir()
        backup = root / '.backups' / (first['id'] + '-' + 'e' * 32)
        (root / first['id']).rename(backup)
        (stage / 'commit.json').write_text(json.dumps({'id': first['id'], 'backup': backup.name}))
        self.assertEqual(s.installed_packages(), [])
        with mock.patch.object(p, '_process_identity', return_value=None):
            self.assertEqual(len(p.list_packages()['installed']), 1)
        self.assertEqual(len(s.installed_packages()), 1)
        self.assertFalse(stage.exists())

    def test_invalid_metadata_does_not_install(self):
        for key, value in (('from_code', '../en'), ('to_code', 'en'), ('package_version', '../../1'),
                           ('from_name', 'English\nsecret')):
            metadata = dict(self.metadata, **{key: value})
            with self.assertRaisesRegex(p.PackageError, '^invalid_metadata$'):
                self.install_fixture(self.archive(metadata=metadata))
        self.assertEqual(s.installed_packages(), [])

    def test_download_metadata_must_match_selected_pair(self):
        with p.staging() as stage:
            with self.assertRaisesRegex(p.PackageError, '^package_mismatch$'):
                p.install_archive(self.archive(), stage, expected={'id': 'translate-en_zh-1_9'}, validator=lambda _: None)
        self.assertEqual(s.installed_packages(), [])

    def test_bpe_and_incomplete_model_rejected_without_half_install(self):
        path = self.archive({'pack/bpe.model': 'not-supported'}, omit=('pack/sentencepiece.model',))
        with self.assertRaisesRegex(p.PackageError, '^unsupported_package$'):
            self.install_fixture(path)
        path = self.archive(omit=('pack/model/shared_vocabulary.json',))
        with self.assertRaisesRegex(p.PackageError, '^unsupported_package$'):
            self.install_fixture(path)
        self.assertEqual(s.installed_packages(), [])

    def test_archive_traversal_and_windows_devices_rejected(self):
        for name in ('pack/../escape.txt', '/absolute.txt', 'C:/escape.txt', 'pack\\escape.txt',
                     'pack/model/NUL.txt', 'pack/model/foo.txt:stream', 'other-root/file'):
            with self.subTest(name=name):
                with self.assertRaisesRegex(p.PackageError, '^unsafe_archive$'):
                    self.install_fixture(self.archive({name: b'x'}))
        self.assertFalse((self.root / 'escape.txt').exists())

    def test_symlink_archive_rejected(self):
        path = self.archive()
        with zipfile.ZipFile(path, 'a') as archive:
            entry = zipfile.ZipInfo('pack/model/link')
            entry.create_system = 3
            entry.external_attr = (stat.S_IFLNK | 0o777) << 16
            archive.writestr(entry, '../outside')
        with self.assertRaisesRegex(p.PackageError, '^unsafe_archive$'):
            self.install_fixture(path)

    def test_case_alias_and_duplicate_paths_rejected(self):
        path = self.archive({'pack/model/CONFIG.JSON': '{}'})
        with self.assertRaisesRegex(p.PackageError, '^unsafe_archive$'):
            self.install_fixture(path)

    def test_archive_size_limit_checked_before_extraction(self):
        path = self.archive()
        with mock.patch.object(p, 'MAX_UNPACKED', 8):
            with self.assertRaisesRegex(p.PackageError, '^archive_limit$'):
                self.install_fixture(path)

    def test_cancellation_cleans_stage_and_keeps_installed_list_empty(self):
        event = threading.Event()
        def progress(*_):
            event.set()
        with self.assertRaisesRegex(p.PackageError, '^cancelled$'):
            self.install_fixture(cancel=event, progress=progress)
        self.assertEqual(s.installed_packages(), [])
        self.assertEqual(list((s.packages_directory() / '.staging').iterdir()), [])

    def test_dead_process_stage_cleanup_never_touches_live_or_installed(self):
        self.install_fixture()
        stage_root = s.packages_directory() / '.staging'
        dead = stage_root / ('123-' + 'a' * 32)
        live = stage_root / ('456-' + 'b' * 32)
        for path, pid in ((dead, 123), (live, 456)):
            path.mkdir()
            (path / 'owner.json').write_text(json.dumps({'pid': pid, 'identity': '1234'}))
            (path / 'payload').write_text('temporary')
        with mock.patch.object(p, '_process_identity', side_effect=lambda pid: None if pid == 123 else '1234'):
            p.cleanup_stale_stages(stage_root)
        self.assertFalse(dead.exists())
        self.assertTrue(live.exists())
        self.assertEqual(len(s.installed_packages()), 1)

    def test_download_limit_and_cancel_remove_partial_transaction(self):
        response = io.BytesIO(b'fake-archive-bytes')
        response.headers = {'Content-Length': '99999999999'}
        with p.staging() as stage:
            with mock.patch.object(p, 'open_official', return_value=response):
                with self.assertRaisesRegex(p.PackageError, '^download_too_large$'):
                    p.download('unused', stage / 'download.argosmodel')
        self.assertEqual(list((s.packages_directory() / '.staging').iterdir()), [])

    def pinned_catalog(self, sha256=None):
        row = dict(self.metadata, links=['https://argos-net.com/v1/translate-en_fr-1_9.argosmodel'])
        if sha256 is not None:
            row['sha256'] = sha256
        path = self.root / 'bundled-catalog.json'
        path.write_text(json.dumps({'schema_version': 1, 'packages': [row]}), encoding='utf-8')
        return mock.patch.object(p, 'BUNDLED_CATALOG', path)

    def served(self, data):
        response = io.BytesIO(data)
        response.headers = {'Content-Length': str(len(data))}
        return mock.patch.object(p, 'open_official', return_value=response)

    def test_pinned_checksum_mismatch_rejects_download_without_install(self):
        data = self.archive().read_bytes()
        with self.pinned_catalog('0' * 64), self.served(data), mock.patch.object(p, 'install_archive') as install:
            with self.assertRaisesRegex(p.PackageError, '^checksum_mismatch$'):
                p.install('translate-en_fr-1_9')
        install.assert_not_called()
        self.assertEqual(s.installed_packages(), [])
        self.assertEqual(list((s.packages_directory() / '.staging').iterdir()), [])

    def test_pinned_checksum_match_continues_to_install(self):
        data = self.archive().read_bytes()
        digest = __import__('hashlib').sha256(data).hexdigest()
        with self.pinned_catalog(digest), self.served(data), mock.patch.object(p, 'install_archive', return_value={'ok': True}) as install:
            self.assertEqual(p.install('translate-en_fr-1_9'), {'ok': True})
        self.assertEqual(install.call_args[0][3], digest)

    def test_unpinned_package_installs_as_before(self):
        data = self.archive().read_bytes()
        with self.pinned_catalog(None), self.served(data), mock.patch.object(p, 'install_archive', return_value={'ok': True}) as install:
            self.assertEqual(p.install('translate-en_fr-1_9'), {'ok': True})
        install.assert_called_once()

    def test_malformed_pin_fails_before_any_download(self):
        with self.pinned_catalog('not-a-sha256'), mock.patch.object(p, 'open_official') as network:
            with self.assertRaisesRegex(p.PackageError, '^invalid_catalog$'):
                p.install('translate-en_fr-1_9')
        network.assert_not_called()

    def test_refreshed_catalog_cannot_remove_a_bundled_pin(self):
        refreshed = s.data_directory() / 'argos_catalog.json'
        refreshed.parent.mkdir(parents=True, exist_ok=True)
        row = dict(self.metadata, links=['https://argos-net.com/v1/translate-en_fr-1_9.argosmodel'])
        refreshed.write_text(json.dumps({'packages': [row]}), encoding='utf-8')
        data = self.archive().read_bytes()
        with self.pinned_catalog('f' * 64), self.served(data), mock.patch.object(p, 'install_archive') as install:
            with self.assertRaisesRegex(p.PackageError, '^checksum_mismatch$'):
                p.install('translate-en_fr-1_9')
        install.assert_not_called()

    def test_refresh_is_explicit_and_list_uses_validated_snapshot(self):
        metadata = dict(self.metadata, links=['https://argos-net.com/v1/translate-en_fr-1_9.argosmodel'])
        response = io.BytesIO(json.dumps([metadata]).encode())
        with mock.patch.object(p, 'open_official', return_value=response) as opened:
            result = p.refresh_catalog()
        opened.assert_called_once_with(p.CATALOG_URL, True)
        self.assertEqual(result['catalog_source'], 'refreshed')
        with mock.patch.object(p, 'open_official', side_effect=AssertionError('no list networking')):
            self.assertEqual(p.list_packages()['catalog'][0]['id'], 'translate-en_fr-1_9')

    def test_list_cli_emits_one_structured_result(self):
        output = io.StringIO()
        with mock.patch.object(p.sys, 'stdout', output), mock.patch.object(p, 'open_official', side_effect=AssertionError()):
            self.assertEqual(p.main(['--list']), 0)
        lines = output.getvalue().splitlines()
        self.assertEqual(len(lines), 1)
        self.assertEqual(json.loads(lines[0])['event'], 'result')


if __name__ == '__main__':
    unittest.main()
