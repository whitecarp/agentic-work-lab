from copy import deepcopy
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import MagicMock, AsyncMock, patch

from video_publisher import adapters, edits, browser_edits, browser_backends
from video_publisher.core import VpubError, atomic_json


class YouTubeEditTests(unittest.TestCase):
    def setUp(self):
        self.before={'id':'video123','snippet':{'channelId':'channel123','title':'Old','description':'Old copy',
                     'categoryId':'1','tags':['animation'],'defaultLanguage':'zh-Hans','defaultAudioLanguage':'zh-Hans'},
                     'status':{'privacyStatus':'private'}}
        self.patch={'expected_identity':'channel123','title':'New','description':'原文\n\nTranslation'}
        self.service=MagicMock()
        self.service.channels.return_value.list.return_value.execute.return_value={'items':[{'id':'channel123'}]}
        self.service.videos.return_value.list.return_value.execute.return_value={'items':[self.before]}

    def test_update_preserves_category_tags_languages_and_privacy(self):
        after=deepcopy(self.before);after['snippet'].update(title='New',description=self.patch['description'])
        self.service.videos.return_value.list.return_value.execute.side_effect=[{'items':[self.before]},{'items':[after]}]
        checkpoint=MagicMock()
        with patch.object(adapters,'yt_service',return_value=self.service), patch.object(adapters,'yt_credentials') as credentials:
            credentials.return_value.has_scopes.return_value=True
            result=edits.youtube_update('default','video123',self.patch,checkpoint)
        self.assertEqual('EDIT_VERIFIED',result['state'])
        body=self.service.videos.return_value.update.call_args.kwargs
        self.assertEqual('snippet',body['part'])
        self.assertNotIn('status',body['body'])
        self.assertEqual('1',body['body']['snippet']['categoryId'])
        self.assertEqual(['animation'],body['body']['snippet']['tags'])
        self.assertEqual('zh-Hans',body['body']['snippet']['defaultAudioLanguage'])
        self.assertEqual('EDIT_SUBMITTING',checkpoint.call_args.args[0]['state'])

    def test_verify_only_never_writes_a_mismatching_caption(self):
        with patch.object(adapters,'yt_service',return_value=self.service):
            result=edits.youtube_update('default','video123',self.patch,MagicMock(),verify_only=True)
        self.assertEqual('EDIT_SUBMITTED_UNVERIFIED',result['state'])
        self.service.videos.return_value.update.assert_not_called()

    def test_wrong_owner_or_privacy_readback_is_rejected(self):
        self.before['snippet']['channelId']='other'
        with patch.object(adapters,'yt_service',return_value=self.service), self.assertRaises(VpubError):
            edits.youtube_update('default','video123',self.patch,MagicMock())
        self.service.videos.return_value.update.assert_not_called()
        self.before['snippet']['channelId']='channel123'
        after=deepcopy(self.before);after['snippet'].update(title='New',description=self.patch['description'])
        after['status']['privacyStatus']='public'
        self.service.videos.return_value.list.return_value.execute.side_effect=[{'items':[self.before]},{'items':[after]}]
        with patch.object(adapters,'yt_service',return_value=self.service), patch.object(adapters,'yt_credentials') as credentials:
            credentials.return_value.has_scopes.return_value=True
            with self.assertRaises(VpubError) as error:
                edits.youtube_update('default','video123',self.patch,MagicMock())
        self.assertEqual('edit_unverified',error.exception.code)


class ChapterRecoveryTests(unittest.IsolatedAsyncioTestCase):
    async def test_pending_work_id_does_not_open_editor(self):
        receipt={'state':'SUBMITTED_UNVERIFIED','pending_chapters':{'chapters':[]}}
        with patch.object(browser_edits,'execute',new_callable=AsyncMock) as execute:
            result=await browser_backends.complete_pending_chapters(None,'douyin','creator',receipt)
        execute.assert_not_called()
        self.assertEqual('PENDING_WORK_ID',result['chapters_state'])

    async def test_saved_or_uncertain_chapters_never_get_second_save(self):
        with tempfile.TemporaryDirectory() as tmp, patch.dict(os.environ,VPUB_HOME=tmp):
            receipt={'id':'123','state':'SUBMISSION_VERIFIED','pending_chapters':{'chapters':[]}}
            for state,expected in [('EDIT_SUBMITTING','SAVE_UNVERIFIED'),('EDIT_SUBMITTED_UNVERIFIED','SAVE_UNVERIFIED'),('EDIT_VERIFIED','VERIFIED')]:
                atomic_json(Path(tmp)/'edits/douyin-chapters-123.json',{'state':state})
                with patch.object(browser_edits,'execute',new_callable=AsyncMock) as execute:
                    result=await browser_backends.complete_pending_chapters(None,'douyin','creator',receipt)
                execute.assert_not_called()
                self.assertEqual(expected,result['chapters_state'])

    def test_times_must_belong_to_matching_titles(self):
        chapters=[{'start_seconds':0,'title':'Opening'},{'start_seconds':14,'title':'Question'}]
        self.assertTrue(browser_edits.chapter_text_matches('章节数：2\n00:00\nOpening\n00:14\nQuestion',chapters))
        self.assertFalse(browser_edits.chapter_text_matches('章节数：2\n00:14\nOpening\n00:00\nQuestion',chapters))
        self.assertFalse(browser_edits.chapter_text_matches('00:00 Opening 00:14 Question',chapters))

    def test_edit_does_not_silently_ignore_a_second_operation(self):
        with self.assertRaises(VpubError):
            edits.validate_patch('douyin',{'expected_identity':'creator','portrait':'p.png','chapters':[]})
