"""Opt-in official-download acceptance check; never downloads unless --live is given."""
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
LIVE = '--live' in sys.argv
if LIVE:
    sys.argv.remove('--live')


@unittest.skipUnless(LIVE, 'Official download requires explicit --live')
class OfficialPackageTests(unittest.TestCase):
    def test_official_french_install_then_socket_blocked_translation(self):
        with tempfile.TemporaryDirectory(prefix='whale-official-language-test-') as directory:
            environment = dict(os.environ, DAFEIYU_DATA_DIR=directory, PYTHONIOENCODING='utf-8')
            process = subprocess.Popen([sys.executable, '-B', str(ROOT / 'backend' / 'package_manager.py'),
                '--install', 'translate-en_fr-1_9'], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                stderr=subprocess.PIPE, text=True, encoding='utf-8', env=environment, cwd=tempfile.gettempdir())
            result, previous = None, -1
            try:
                for line in process.stdout:
                    row = json.loads(line)
                    if row['event'] == 'result':
                        result = row
                    elif row['phase'] != 'download':
                        print(json.dumps(row), flush=True)
                    elif row.get('total_bytes'):
                        percent = row['bytes'] * 100 // row['total_bytes']
                        if percent // 10 > previous:
                            previous = percent // 10
                            print(json.dumps({'download_percent': percent}), flush=True)
                process.wait(timeout=30)
                errors = process.stderr.read()
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait()
                process.stdin.close()
                process.stdout.close()
                process.stderr.close()
            self.assertEqual(process.returncode, 0, (result, errors))
            self.assertEqual(errors, '')
            self.assertTrue(result and result['ok'], result)
            folder = pathlib.Path(directory) / 'packages' / 'translate-en_fr-1_9'
            self.assertTrue((folder / '.install.json').is_file())
            receipt = json.loads((folder / '.install.json').read_text(encoding='utf-8'))
            self.assertEqual(receipt['origin'], 'official')
            wrapper = ('import sys,runpy,pathlib\n'
                       'sys.path.insert(0,str(pathlib.Path(sys.argv[1]).parent))\n'
                       'def guard(event,args):\n'
                       '    if event in ("socket.connect","socket.connect_ex","socket.getaddrinfo","socket.sendto"): raise RuntimeError("network forbidden")\n'
                       'sys.addaudithook(guard)\n'
                       'runpy.run_path(sys.argv[1],run_name="__main__")\n')
            request = {'id': 7, 'text': 'Please keep the original file.', 'direction': 'pair:en:fr'}
            run = subprocess.run([sys.executable, '-B', '-c', wrapper, str(ROOT / 'backend' / 'offline_worker.py')],
                input=json.dumps(request) + '\n', capture_output=True, encoding='utf-8', env=environment,
                cwd=tempfile.gettempdir(), timeout=45)
            self.assertEqual(run.returncode, 0, run.stderr)
            self.assertEqual(run.stderr, '')
            response = json.loads(run.stdout.splitlines()[-1])
            self.assertTrue(response['offline'])
            self.assertIn('fichier', response['translation'].lower())
            self.assertNotEqual(response['translation'], request['text'])
            self.assertEqual(list((pathlib.Path(directory) / 'packages' / '.staging').iterdir()), [])
            print(json.dumps({'official_install': result['package']['id'],
                              'archive_sha256': receipt['archive_sha256'],
                              'socket_blocked_translation': response['translation']}, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    unittest.main()
