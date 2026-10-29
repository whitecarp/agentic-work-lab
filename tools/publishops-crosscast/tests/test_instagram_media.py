from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from PIL import Image
from video_publisher import cli, instagram_media, cover_video
from video_publisher.core import atomic_json, file_sha, load_plan, read_json, VpubError


class InstagramPreparationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.video = self.root / 'source.mp4'
        self.video.write_bytes(b'offline synthetic video')
        self.cover = self.root / 'cover.jpg'
        Image.new('RGB', (320, 180), 'red').save(self.cover)
        self.value = {'schema_version':1,'video':str(self.video),'cover':str(self.cover),
                      'title':'Example','description':'','tags':[],
                      'platforms':{'instagram':{'method':'native','visibility':'account','ai_generated':True},
                                   'x':{'method':'native','visibility':'account','ai_generated':True}}}
        self.manifest = self.root / 'input.json'
        atomic_json(self.manifest, self.value)

    def tearDown(self):
        self.tmp.cleanup()

    def test_portrait_and_square_are_unchanged_and_plan_is_instagram_only(self):
        for width, height in [(180,320),(320,320)]:
            with patch.object(instagram_media,'_binary',return_value='probe'), patch.object(instagram_media,'_probe',return_value={'streams':[{'codec_type':'video','width':width,'height':height}]}), patch.object(instagram_media,'prepend') as render:
                result = instagram_media.prepare(self.manifest,self.root/f'out-{width}-{height}.json')
                self.assertEqual('ORIGINAL_PORTRAIT_OR_SQUARE',result['mode'])
                self.assertEqual(file_sha(self.video),result['video_sha256'])
                self.assertEqual(['instagram'],list(load_plan(result['manifest'])['platforms']))
                render.assert_not_called()

    def test_landscape_requires_cover_and_no_duplicate_lead_in(self):
        receipt = self.video.with_suffix('.mp4.receipt.json')
        atomic_json(receipt,{'output_sha256':file_sha(self.video),'recipe':{'mode':'prepend_contain_silent','cover_sha256':file_sha(self.cover)}})
        probe={'streams':[{'codec_type':'video','width':320,'height':180}]}
        with patch.object(instagram_media,'_binary',return_value='probe'), patch.object(instagram_media,'_probe',return_value=probe), patch.object(instagram_media,'prepend') as render:
            result=instagram_media.prepare(self.manifest,self.root/'ready.json')
            self.assertEqual('ALREADY_PREPARED',result['mode'])
            load_plan(result['manifest'])
            render.assert_not_called()
            self.cover.write_bytes(b'changed cover')
            with self.assertRaises(VpubError):
                instagram_media.prepare(self.manifest,self.root/'changed.json')
            receipt.unlink()
            self.value.pop('cover')
            atomic_json(self.manifest,self.value)
            with self.assertRaises(VpubError):
                instagram_media.prepare(self.manifest,self.root/'missing.json')

    def test_render_refuses_source_overwrite_and_invalid_duration(self):
        original=self.video.read_bytes()
        for seconds in [float('nan'),0,-1,10]:
            with self.assertRaises(VpubError):
                cover_video.prepend(self.video,self.cover,self.root/'bad.mp4',seconds)
        with self.assertRaises(VpubError):
            cover_video.prepend(self.video,self.cover,self.video)
        self.assertEqual(original,self.video.read_bytes())

    def test_real_ffmpeg_cover_audio_duration_reuse_and_valid_plan(self):
        encoder=os.environ.get('CROSSCAST_TEST_FFMPEG') or shutil.which('ffmpeg')
        probe=os.environ.get('CROSSCAST_TEST_FFPROBE') or shutil.which('ffprobe')
        if not encoder or not probe:
            self.skipTest('Set CROSSCAST_TEST_FFMPEG/FFPROBE for local encode integration test')
        source=self.root/'real.mp4'
        subprocess.run([encoder,'-hide_banner','-loglevel','error','-f','lavfi','-i','color=c=blue:s=320x180:r=30:d=2',
                        '-f','lavfi','-i','sine=frequency=440:sample_rate=48000:duration=2','-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac','-shortest',str(source)],check=True)
        self.value['video']=str(source)
        atomic_json(self.manifest,self.value)
        before=file_sha(source)
        output=self.root/'prepared.json'
        result=instagram_media.prepare(self.manifest,output,encoder,probe)
        self.assertEqual('LANDSCAPE_COVER_PREPENDED',result['mode'])
        self.assertEqual(['instagram'],list(load_plan(output)['platforms']))
        self.assertEqual(before,file_sha(source))
        media=Path(result['video'])
        receipt=read_json(media.with_suffix('.mp4.receipt.json'))
        self.assertAlmostEqual(1,receipt['cover_seconds'],places=2)
        self.assertAlmostEqual(3,receipt['output_duration'],delta=.15)
        self.assertTrue(receipt['audio_preserved'])
        def pixel(at):
            return subprocess.check_output([encoder,'-v','error','-ss',str(at),'-i',str(media),'-frames:v','1','-vf','scale=1:1','-f','rawvideo','-pix_fmt','rgb24','-'])
        self.assertGreater(pixel(.5)[0],200)
        self.assertGreater(pixel(1.5)[2],200)
        again=instagram_media.prepare(self.manifest,output,encoder,probe)
        self.assertEqual(result['video_sha256'],again['video_sha256'])
        prepared_again=instagram_media.prepare(output,self.root/'again.json',encoder,probe)
        self.assertEqual('ALREADY_PREPARED',prepared_again['mode'])
        media.write_bytes(b'changed output')
        with self.assertRaises(VpubError):
            instagram_media.prepare(self.manifest,output,encoder,probe)
