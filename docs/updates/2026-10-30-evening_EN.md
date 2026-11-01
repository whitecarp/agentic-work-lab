# 2026-10-30: Transformation-study repairs and publication state

[繁體中文](2026-10-30-evening.md) · **English**

Two complete experimental deliveries are documented: v3 (67 seconds) and v4 (75.23 seconds). Their pending Release uploads were completed on 1 November 2026, with sizes and SHA-256 digests read back and matched. Production and Bilibili observations retain their original sampling dates. [Media catalogue](../../media/README_EN.md) · [Structured record](../../data/production-update-2026-10-30-evening.json)

## Turning specific defects into verifiable repairs

Version 4 preserves original assets and the earlier v3 while addressing reported timestamps. It uses an earlier face push to avoid problematic tail/body motion, matches brightness across a cut, interpolates only the eye regions between real source endpoints to repair abrupt gaze jumps, and replaces a returning pose with a tail close-up.

Audio and picture are checked separately: music continues during the smile hold; the drag-away scene, fade and black interval are silent. Music returns over the group portrait and ducks by 12 dB under the final shout. The v4 protection region also corrects the v3 case where a moving tail fin entered the hair mask; a fixed mask was not treated as full object recognition.

These are reusable production and validation practices. No time-saving percentage was measured, and residual style/motion differences are not claimed to be eliminated.

## Technical evidence and acceptance limits

| Check | Recorded v4 result |
|---|---|
| Encoding | 1080×1920, 30 fps, 2,257 frames, H.264/AAC |
| Full decode | Passed |
| Audio | Zero clipped samples; sample peak approximately −1.636 dBFS |
| Silence window | Decoded samples all zero from 50.763 to 62.503 seconds |
| Full black | First full-black frame: 1,845, at 61.500 seconds |
| Preservation | v3 hash unchanged; v4 picture stream was copied during audio mixing |

Delivery includes frame and signal checks. Inspection of local comparison images does not establish continuous full-film viewing or subjective listening. Both versions are experimental deliveries, not labelled as full user acceptance. Raw recordings, standalone third-party reference clips, private conversations and unfinished intermediate files are excluded.

## GPT episode four is public

The operations receipt records [GPT Duty Log 04](https://www.bilibili.com/video/BV1xFx764EjN) becoming public at 12:00 on 30 October 2026 (UTC+8), with public access checked at 13:00. Eight chapters were saved and read back, but chapter review remains pending in that receipt; collection listing was also delayed. Neither is presented as fully complete. The video hash is unchanged; no duplicate upload or schedule edit was performed.

No newer full-channel or day-one dataset was collected in this run. The [data comparison](../data-comparison_EN.md) retains its original sampling dates and populations; schedules and pending samples are not converted into view estimates.

## Tool version

The published CrossCast **0.15.1** and its 417-test record are preserved. This update changes research notes, the media catalogue and state only; it adds no CLI features and does not rerun that version's tests. [Validation record](../../tools/publishops-crosscast/docs/VALIDATION_EN.md)

[Back to the lab](../../README_EN.md)
