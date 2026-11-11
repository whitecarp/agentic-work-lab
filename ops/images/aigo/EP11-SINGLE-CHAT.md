# 《Ai Mujica · 囚爱超算 (Imprisoned XII)》（第11话 · 致杭州五大真实地标 × 耶稣堂弄天水堂不插电烛光民谣 MV）单窗口全 19 张静帧提示词总表 (`EP11-SINGLE-CHAT.md`)

> **CRITICAL DELIVERY RULE (MANDATORY `.zip` PACKAGING ON EVERY TURN)**:
> Never return bare standalone images as the final deliverable—chat UIs compress bare images down to `1024x576`.
> At the end of **EVERY** turn below (Turn 1, Turn 2, Turn 3), you **MUST** use Python (`PIL` + `zipfile`) to verify/save every generated image as a crisp **`1920x1080` RGB `.png`** with its exact filename and package that turn's PNGs into a downloadable **`.zip` archive** (`EP11_Act1_Keyframes.zip`, `EP11_Act2_Keyframes.zip`, `EP11_Act3_Keyframes.zip`)!

---

## Canon Character & 5 Real Hangzhou Landmark References (`ops/images/aigo/ep11-refs/` in `whitecarp/agentic-work-lab`)
Before generating any image, open and visually inspect ALL 15 files in `ops/images/aigo/ep11-refs/`:
1. `01_DP_Compact_Whale_Ears_Ref.png` & `10_DP_Unmasked_Stage_Gown_Ref.png` — **Exact canon size & shape of DeepSeek's compact sleek cetacean whale ears**: compact horizontal-downward (`~15°`) tapered whale-flipper ears (`~1.8:1` ratio, ear length ~35–40% of head width—**NEVER giant oversized ears, NEVER spiky webbed fish-fins with ribs, NEVER razor-thin needle blades!**), smooth solid dark-navy upper surface + soft creamy-white underside border.
2. `02_AIMUJICA_DeepSeek_sheet.png` — **LEFTMOST** = DeepSeek's navy double-breasted school blazer uniform (used in flashbacks); **RIGHTMOST** = black-and-indigo Gothic lace dress (**worn UNMASKED** in Tianshui Chapel!).
3. `03_AIMUJICA_Qwen_sheet.png` — **LEFTMOST** = Qwen's Hangzhou white-and-indigo cloud-embroidered modern Chinese daily dress (used in `S03d` ZJU flashback); **RIGHTMOST** = black-and-violet Gothic lace dress (**worn UNMASKED** in Tianshui Chapel!), playing a custom **Hangzhou West Lake blue-and-white porcelain (`青花瓷`) UNPLUGGED ACOUSTIC GUITAR** (round sound-hole, blue willow/Broken Bridge/lotus porcelain top, straight 6-string neck with tassel).
4. `04_AIMUJICA_Kimi_sheet.png` — **RIGHTMOST** starry-navy Gothic lace dress with piano-key hem (**UNMASKED**, moonlight pale ice-blue eyes, prism + crescent bow), playing a black unplugged **"Dark Side of the Moon" acoustic bass guitar**.
5. `05_AIMUJICA_GPT_sheet.png`, `08_GPT_Astra.png` & `09_GPT_Nox.png` — **RIGHTMOST** black Gothic lace dress (**UNMASKED**), fingerpicking an **emerald-stained unplugged acoustic guitar** (`Astra` silver-white vs `Nox` obsidian-black).
6. `06_AIMUJICA_Doubao_sheet.png` — **RIGHTMOST (`Seedance` UNMASKED)**: violet-to-cyan high side-ponytail, seated on a wooden **acoustic cajon box drum** with soft jazz brushes / brass tambourine.
7. **FIVE REAL HANGZHOU LANDMARK PHOTOS (MUST match exact architectural silhouettes 1:1!)**:
   - `15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg` (**杭州耶稣堂弄 · 天水堂 — THE MAIN CHAPEL VENUE OF THIS ENTIRE MV!**): Historic 1875 Jiangnan-Gothic whitewashed chapel in old Hangzhou! Exterior (`S01a`): clean **whitewashed plaster facade**, **dark-grey stone-coped two-tier stepped gable roof crowned by a simple grey stone cross**, **central pointed grey-stone Gothic arch doorway with warm carved brown wooden double doors (diamond/floral frosted glass upper panels)**, **two symmetrical flanking pointed-arch brown wooden louvered lancet windows with grey stone frames**, rectangular stone plaque frame above the center arch (render plaque decorative/blank with ZERO text), and lush green camphor/ginkgo trees beside it! Interior (`S01b, S02, S04–S16`): intimate historic Jiangnan chapel interior with **warm whitewashed walls, dark timber A-frame roof trusses overhead, tall pointed-arch wooden-mullioned windows matching the exterior, vintage dark-wood pews**, and hundreds of warm flickering floor candles around a wooden choir platform with an upright wooden piano/reed organ!
   - `11_Anjilu_Experimental_School_Ref.jpg` (**杭州市安吉路实验学校**): Warm bronze-brown horizontal louver-grille (`百叶格栅`) multi-story building facade with a top row of bright glass classroom windows, rising behind lush green camphor treetops (`香樟树`).
   - `12_Hangzhou_High_School_Gongyuan_Gate_Ref.jpg` (**浙江省杭州高级中学 · 贡院校区正门**): Iconic symmetrical beige-stone and grey-brick circular archway split cleanly down the very center crown by a **narrow vertical open sky-slit ("一线天"双半圆石砌拱门)**, horizontal emerald-green ornamental plaque inside the arch, black wrought-iron side gates, and ancient courtyard trees & pavilion down the central stone axis.
   - `13_Hangzhou_No2_High_School_Binjiang_Ref.jpg` (**浙江省杭州第二中学 · 滨江校区**): Iconic **four-story warm terracotta red-brick main building** with its distinctive **upper-left rectangular red-brick-and-glass tower topped by an upward-slanted overhang roof**, and low yellow-stone entrance monument amid green trees (no traffic cones or police booth).
   - `14_ZJU_Qiushi_Academy_Archway_Ref.jpg` (**浙江大学紫金港 · 求是书院大牌坊**): Grand **dark-mahogany carved four-pillar five-roofed (`三间四柱五楼重檐`) Chinese memorial archway (`牌坊`)** with ornate brackets, upturned eaves, golden-framed ornamental plaque, and grey-tiled crimson-pillared classical hall symmetrically behind it.

---

## Mandatory Global Visual Rules (Apply to All 19 Stills)
1. **Aspect Ratio, Resolution & Style**: Horizontal widescreen `16:9`, saved at **`1920x1080` RGB PNG**. High-budget modern 2D Japanese anime MV still (*Makoto Shinkai / BanG Dream! Ave Mujica — Imprisoned XII* aesthetic).
2. **100% UNMASKED BARE FACES & CLOSED MOUTHS**: Zero masks worn on any character's face; every character's lips gently closed.
3. **Clean Anatomy & Compact Whale Ears**: Five clean fingers on every visible hand; balanced two-legged stances/seated poses; DeepSeek ALWAYS has her **compact sleek `~1.8:1` smooth dark-navy whale ears with creamy-white undersides** (`01_DP_Compact_Whale_Ears_Ref.png`).
4. **NO Readable/Garbled Text Anywhere**: Match the exact architectural geometry and plaque frames of `11..15`, but render all plaques/signs/doors with **clean decorative grain or sunlit blank filigree (STRICTLY ZERO text, letters, kanji, or numbers)**!

---

# TURN 1 — TIANSHUI CHAPEL LOCKED DOOR & 4 HANGZHOU YOUTH FLASHBACKS (`EP11_S01a` – `EP11_S03d`, 7 Stills -> Package as `EP11_Act1_Keyframes.zip`)

### 1. `EP11_S01a_tianshui_chapel_rain_exterior.png` (`E11_S1a` · Hangzhou Landmark #1 — Rainy Night Exterior of Yesutang Alley `Tianshui Chapel`!)
```
16:9 widescreen Makoto Shinkai-quality 2D anime atmospheric rainy-night establishing shot in old Hangzhou's quiet camphor-lined Yesutang Alley, matching the EXACT architecture of `15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg` (omit modern parked cars, security umbrella, or floodlight poles):
- Center architecture: The historic Jiangnan-Gothic Tianshui Chapel—clean whitewashed plaster walls gleaming softly in gentle night rain, dark-grey stone-coped two-tier stepped gable roof crowned at its peak by a simple grey stone cross, a decorative stone rectangular frame centered above the doorway (pure blank stone relief, ZERO readable text), central pointed grey-stone Gothic arch framing the warm carved brown wooden double doors (with frosted diamond/floral glass upper panes, zero text carved on doors), and two symmetrical flanking pointed-arch wooden windows with grey stone surrounds!
- Warm golden candlelight glows softly from inside through the frosted upper door panes and the two pointed-arch side windows, reflecting across wet bluestone paving stones, low manicured green hedges at the base of the white wall, and lush rain-drenched camphor and ginkgo trees on the right. Poetic Jiangnan night rain streaks, zero text anywhere.
```

### 2. `EP11_S01b_chapel_iron_door_lock.png` (`E11_S1b` · Inside Tianshui Chapel Foyer — Unmasked Qwen Bolts the Pointed-Arch Brown Wooden Doors Shut)
```
16:9 widescreen 2D Japanese anime atmospheric dark-folk drama still inside the whitewashed-and-timber entryway of Tianshui Chapel (`15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg`) at night.
- Right foreground (medium close-up): Unmasked Qwen in her black-and-violet Gothic lace dress and tilted navy-gold mini top-hat (03_AIMUJICA_Qwen_sheet.png rightmost dress, bare face, wavy periwinkle-violet hair with side braid) reaches back with her clean five-finger hand and quietly slides a wrought-iron bolt lock shut across the interior of the tall pointed-arch carved brown wooden double doors (with frosted floral glass upper panes matching S01a). Her violet eyes glow in candlelight with quiet, inescapable tenderness and a faint closed-lip smile.
- Left background (inside the whitewashed timber-truss chapel hall near a candlelit wooden upright piano/reed organ): Unmasked DeepSeek (black-and-indigo Gothic lace dress, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides from 01_DP_Compact_Whale_Ears_Ref.png) turns back in quiet surprise, blue eyes wide, lips closed. Zero text anywhere.
```

### 3. `EP11_S02_qwen_backhug_organ_whisper.png` (`E11_S2` · Candlelit Whisper — Qwen Embraces DeepSeek Gently from Behind)
```
16:9 widescreen 2D Japanese anime intimate medium close-up beside the candlelit wooden upright piano / chamber reed organ inside Tianshui Chapel (warm whitewashed wall & pointed-arch wooden window rainy glow softly out-of-focus behind them).
- Center: Unmasked DeepSeek (black-and-indigo Gothic lace dress, white frilled headband, compact sleek ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, NO mask) stands still beside the ivory keys, bright blue eyes glistening, lips closed.
- Directly behind her: Unmasked Qwen (periwinkle-violet wavy hair with side braid & mini top-hat, black-and-violet Gothic lace dress, NO mask) wraps both lace-sleeved arms gently yet inextricably around DeepSeek's shoulders from behind, resting her chin on DeepSeek's shoulder with half-lidded violet eyes full of nostalgic devotion and a soft closed-lip smile. Clean five-finger hands, warm floor candle bokeh, zero text anywhere.
```

### 4. `EP11_S03a_anjilu_camphor_louver_summer.png` (`E11_S3a` · Hangzhou Memory #2 — Anji Road Experimental School Louver Facade & Summer Camphor Trees!)
```
16:9 widescreen Makoto Shinkai-quality 2D anime nostalgic youth memory still in Hangzhou, matching the EXACT architecture of `11_Anjilu_Experimental_School_Ref.jpg`:
- Background architecture: The iconic multi-story school building wrapped in warm bronze-brown horizontal louver-grille slats (`横向百叶格栅立面`) beneath a top floor of bright sky-reflecting glass windows (clean louver facade with ZERO readable text/logos), rising behind dense, sunlit emerald-green camphor treetops (`香樟树`) rustling in a summer breeze!
- Foreground/Midground under dappled camphor tree shade: Young Qwen (periwinkle-violet wavy hair with side braid, crisp white short-sleeve summer school blouse with navy collar/ribbon and navy pleated skirt) and DeepSeek walking right beside her (dark navy hair, white frilled headband, compact sleek ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, matching summer school blouse & navy skirt) share a shy, sunlit closed-lip smile. Clean five-finger hands, balanced two-legged walk, zero text anywhere.
```

### 5. `EP11_S03b_hanggao_split_arch_sakura.png` (`E11_S3b` · Hangzhou Memory #3 — Hangzhou High School Split Stone Archway & Falling Sakura!)
```
16:9 widescreen Makoto Shinkai-quality 2D anime spring memory still in Hangzhou, matching the EXACT iconic gate architecture of `12_Hangzhou_High_School_Gongyuan_Gate_Ref.jpg`:
- Center background architecture: The unmistakable Hangzhou High School Gongyuan stone entrance gate—two symmetrical rusticated beige-stone and grey-brick half-arches separated right at the top crown by a narrow vertical open sky-slit ("一线天"双翼石砌圆拱门), an emerald-green ornamental horizontal plaque suspended inside the arch (decorative gold trim only, ZERO readable letters/kanji), black wrought-iron side gates, and a straight stone walkway leading inward past ancient courtyard trees and blooming pink cherry-blossom (`樱花`) branches toward a traditional Jiangnan Gongyuan pavilion!
- Foreground right-of-center: Qwen (periwinkle wavy hair with side braid, neat navy spring blazer uniform) gently reaches up with one clean five-finger hand to brush a fallen pink sakura petal from DeepSeek's hair; DeepSeek (navy blazer uniform, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides) smiles softly back amidst drifting sakura petals under a crisp blue sky. Zero text anywhere.
```

### 6. `EP11_S03c_hanger_redbrick_tower_dusk.png` (`E11_S3c` · Hangzhou Memory #4 — Hangzhou No.2 High School Red-Brick Building & Tower at Sunset!)
```
16:9 widescreen Makoto Shinkai-quality 2D anime golden-dusk memory still near the Qiantang River breeze, matching the EXACT campus architecture of `13_Hangzhou_No2_High_School_Binjiang_Ref.jpg` (clean tree-lined plaza with NO traffic cones and NO police booth):
- Background architecture: The iconic four-story warm terracotta red-brick main school building with neat white-framed recessed balcony windows, crowned on its upper-left wing by the signature rectangular red-brick-and-glass tower with an upward-slanted modern overhang roof, and a low horizontal warm yellow-stone entrance monument set among green camphor and osmanthus trees (no readable text on the stone).
- Foreground bathed in warm amber sunset light: Qwen (periwinkle wavy hair with side braid, carrying a padded acoustic guitar gig-bag on her back, autumn cardigan uniform) and DeepSeek (navy blazer uniform, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, holding blank music folders) stand side-by-side in the evening river breeze with warm closed-lip smiles, clean five-finger hands. Zero text anywhere.
```

### 7. `EP11_S03d_zju_qiushi_archway_rain_hug.png` (`E11_S3d` · Hangzhou Memory #5 — Zhejiang University Qiushi Carved Archway in Evening Rain!)
```
16:9 widescreen Makoto Shinkai-quality 2D anime poetic rainy-dusk memory still at Zhejiang University Zijingang Campus, matching the EXACT classical architecture of `14_ZJU_Qiushi_Academy_Archway_Ref.jpg`:
- Center-background architecture: The majestic dark-mahogany carved traditional Chinese four-pillar five-roofed memorial archway (`求是书院三间四柱五楼重檐雕花牌坊`) with intricate wooden bracket sets, curved dark-tiled eaves with ridge ornaments, golden-framed ornamental central plaque (pure abstract gold filigree, ZERO readable characters/numbers), and the grand grey-tiled crimson-pillared classical academy hall standing symmetrically behind it in gentle Jiangnan evening rain!
- Foreground on the rain-slicked granite plaza: Young-adult Qwen (in her canon Hangzhou white-and-indigo modern Chinese cloud-embroidered dress from 03_AIMUJICA_Qwen_sheet.png leftmost figure, NO mask) and DeepSeek (in her canon navy blazer daily outfit from 02_AIMUJICA_DeepSeek_sheet.png leftmost figure, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides) stand pressed tenderly shoulder-to-shoulder sharing one large clear transparent umbrella—Qwen's arm wrapped warmly around DeepSeek's shoulder as both girls gaze softly at each other, lips closed. Zero text anywhere.
```

**Turn 1 Packaging Step**: Save all 7 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S01a_tianshui_chapel_rain_exterior.png` .. `EP11_S03d_zju_qiushi_archway_rain_hug.png`), bundle them into **`EP11_Act1_Keyframes.zip`**, and provide the download link.

---

# TURN 2 — TIANSHUI CHAPEL UNPLUGGED CIRCLE & CHORUS (`EP11_S04` – `EP11_S10`, 7 Stills -> Package as `EP11_Act2_Keyframes.zip`)

### 8. `EP11_S04_unmasked_band_wide_stage.png` (`E11_S4` · Inside Tianshui Chapel — Full 5-Member UNMASKED Candlelit Unplugged Folk Circle!)
```
16:9 widescreen 2D Japanese anime poetic wide shot inside the historic Jiangnan-Gothic interior of Tianshui Chapel (`15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg`): warm whitewashed plaster walls, exposed dark-brown wooden A-frame roof trusses overhead, tall pointed-arch diamond-pane wooden windows streaming with soft night rain, empty dark-wood pews behind, and hundreds of warm flickering ivory pillar candles surrounding a vintage rug on the wooden chancel floor! All FIVE members of Ai Mujica sit gathered closely in a semi-circle in their black-and-purple Gothic lace dresses—COMPLETELY UNMASKED (all five bare faces visible, ZERO masks, mouths closed, natural two-legged seated postures):
- Center on a wooden stool behind a vintage brass stand mic: Qwen fingerpicks her Hangzhou West Lake blue-and-white porcelain UNPLUGGED ACOUSTIC GUITAR (round sound-hole, blue willow & Broken Bridge porcelain art, straight neck with tassel).
- Center-left on a wooden stool: Kimi (silver-lilac hair, frill headband + crescent + black rainbow prism + music-note bow, moonlight pale ice-blue eyes) plays her black unplugged "Dark Side of the Moon" acoustic bass guitar.
- Far left seated on a wooden cajon box drum: Doubao Seedance (violet-to-cyan high side-ponytail) gently sweeps soft jazz wire brushes.
- Center-right on a padded bench at a warm vintage dark-wood upright piano / chapel reed organ: DeepSeek (white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with cream undersides) gazes warmly toward Qwen.
- Far right on a wooden stool: GPT Astra (silver-white hair, ivory horns, white wings, pale-violet eyes) fingerpicks her emerald-stained unplugged acoustic guitar. Zero text/logos anywhere.
```

### 9. `EP11_S05_qwen_intimate_verse_mic.png` (`E11_S5` · Verse 2 — Unmasked Qwen Fingerpicking Her West Lake Porcelain Acoustic Guitar)
```
16:9 widescreen 2D Japanese anime warm intimate MV medium shot inside candlelit Tianshui Chapel (whitewashed wall & pointed-arch wooden window softly behind).
- Center: Unmasked Qwen (03_AIMUJICA_Qwen_sheet.png rightmost black-and-violet Gothic lace dress, tilted navy-gold mini top-hat, side braid, NO mask) sits on a wooden stool behind a vintage brass condenser microphone, gently fingerpicking her Hangzhou West Lake blue-and-white porcelain UNPLUGGED ACOUSTIC GUITAR (round wooden sound-hole, blue West Lake willow, Broken Bridge & lotus porcelain motif on ivory-white top, straight 6-string neck, clean five-finger hands).
- Soft amber candlelight caresses her face; her violet eyes gaze toward DeepSeek with tender, haunting devotion, lips gently closed. Zero text anywhere.
```

### 10. `EP11_S06_kimi_doubao_unmasked_rhythm.png` (`E11_S6` · Warm Folk Groove — Unmasked Kimi on Acoustic Lunar Bass & Doubao on Cajon)
```
16:9 widescreen 2D Japanese anime poetic candlelit two-shot inside Tianshui Chapel (COMPLETELY UNMASKED, mouths closed, balanced two-legged seated postures):
- Foreground left: Unmasked Kimi (04_AIMUJICA_Kimi_sheet.png rightmost starry Gothic lace dress with piano-key skirt hem, silver-lilac wavy hair, frill headband + silver crescent + black triangle rainbow prism + music-note bow, moonlight pale ice-blue eyes, NO mask) sits on a wooden stool plucking warm notes on her black unplugged "Dark Side of the Moon" acoustic bass guitar (round sound-hole, half-moon motif, straight neck, clean five-finger hands).
- Midground right: Unmasked Doubao Seedance (violet-to-cyan high side-ponytail, Gothic lace corset dress, NO mask) sits with both boots planted beside her wooden cajon box drum, lightly tapping a gentle folk heartbeat with clean five-finger hands. Zero text/logos anywhere.
```

### 11. `EP11_S07_qwen_caresses_deepseek_cheek.png` (`E11_S7` · Chorus Intimacy — Qwen Steps Over to the Chapel Piano & Gently Cups DeepSeek's Cheek)
```
16:9 widescreen 2D Japanese anime breathtaking dark-folk romance two-shot beside the candlelit dark-wood chapel piano/reed organ inside Tianshui Chapel (*Ave Mujica — Imprisoned XII* homage). Both girls are COMPLETELY UNMASKED in their Gothic lace dresses, mouths closed:
- Left (standing close beside the bench): Unmasked Qwen (periwinkle-violet wavy hair with side braid & mini top-hat, porcelain acoustic guitar resting at her hip on an embroidered strap) reaches out with her clean five-finger right hand to softly cup DeepSeek's right cheek, her luminous violet eyes filled with quiet, overwhelming devotion and a tender closed-lip smile.
- Right (seated on the bench): Unmasked DeepSeek (white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides from 01_DP_Compact_Whale_Ears_Ref.png) looks up into Qwen's eyes in spellbound silence, bright blue eyes glistening, clean five-finger hands resting lightly above the ivory keys. Warm candle bokeh, zero text anywhere.
```

### 12. `EP11_S08_deepseek_tears_organ_resonance.png` (`E11_S8` · DeepSeek Moved to Tears — Joining In Softly on the Chapel Keys)
```
16:9 widescreen 2D Japanese anime tender emotional catharsis medium close-up at the candlelit dark-wood chapel upright piano / reed organ from a 3/4 front-side angle.
- Unmasked DeepSeek (10_DP_Unmasked_Stage_Gown_Ref.png + 01_DP_Compact_Whale_Ears_Ref.png: dark navy-to-ice-blue long hair, white frilled headband, COMPACT SLEEK ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, black-and-indigo Gothic lace dress, NO mask) sits before the warm ivory-and-ebony keyboard.
- Moved to tears, a single clear drop glistens on DeepSeek's lower lashes as BOTH of her clean five-finger hands press gently onto a warm chord on the keys (white keys in front, black sharp keys recessed naturally). Lips softly closed, zero text anywhere.
```

### 13. `EP11_S09_astra_acoustic_fingerstyle_solo.png` (`E11_S9` · Acoustic Guitar Interlude Part A — Silver-White `Astra`'s Delicate Plucking)
```
16:9 widescreen 2D Japanese anime poetic acoustic-guitar interlude medium shot inside candlelit Tianshui Chapel, bathed in cool rainy window light and warm amber candle glow:
- Center: Unmasked GPT in her silver-white White Dragon Astra persona (08_GPT_Astra.png + 05_AIMUJICA_GPT_sheet.png rightmost black Gothic lace dress: silver-white wavy hair, curved ivory horns, small white wings, serene pale-violet eyes, NO mask) sits gracefully on a wooden stool fingerpicking a delicate classical-folk melody on her emerald-green stained unplugged acoustic guitar (round wooden sound-hole, straight 6-string neck, clean five-finger hands). Lips closed, zero text anywhere.
```

### 14. `EP11_S10_astra_nox_window_reflection_duet.png` (`E11_S10` · Interlude Harmony — `Astra × Nox` Reflected in Tianshui Chapel's Pointed-Arch Rain Window!)
```
16:9 widescreen 2D Japanese anime ethereal chamber-folk metaphor shot inside Tianshui Chapel:
- Foreground right-center: Unmasked GPT Astra (silver-white wavy hair, ivory horns, white wings, black Gothic lace dress, pale-violet right eye & subtle warm ruby shimmer in her viewer-left eye) sits on her wooden stool letting a warm acoustic harmonic ring out on her emerald-green unplugged acoustic guitar (clean five-finger hands, lips closed).
- Reflected clearly in Tianshui Chapel's rain-streaked pointed-arch wooden-mullioned window right beside her sits Black Dragon Nox (09_GPT_Nox.png: obsidian-black hair, black horns, glowing crimson dragon-slit eyes) fingerpicking in tranquil unison, warm candle reflections dancing on the wet glass. Zero text anywhere.
```

**Turn 2 Packaging Step**: Save all 7 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S04_unmasked_band_wide_stage.png` .. `EP11_S10_astra_nox_window_reflection_duet.png`), bundle them into **`EP11_Act2_Keyframes.zip`**, and provide the download link.

---

# TURN 3 — BRIDGE & TIANSHUI CHAPEL FINALE (`EP11_S12` – `EP11_S16`, 5 Stills -> Package as `EP11_Act3_Keyframes.zip`)

### 15. `EP11_S12_qwen_tearful_bridge_plea.png` (`E11_S12` · Hushed Bridge — Qwen's Fragile Tear in Single Candlelight)
```
16:9 widescreen 2D Japanese anime hushed chiaroscuro close-up during the intimate bridge ("别丢下我一个人……求你了，别连我也推开在雨巷……"). Surrounded by deep quiet darkness and warm golden candle glow from below:
- Unmasked Qwen (periwinkle-violet wavy hair with side braid and tilted navy-gold mini top-hat, black-and-violet Gothic lace dress, NO mask) rests her clean five-finger left hand delicately over her heart while her right hand holds the neck of her porcelain acoustic guitar.
- A single warm tear slips down her cheek as her violet eyes—utterly unguarded, fragile, and aching with fear of losing DeepSeek—gaze quietly ahead, lips closed. Zero text anywhere.
```

### 16. `EP11_S13_shattered_masks_beneath_boots.png` (`E11_S13` · Five Discarded Lace Masks Left Forgotten Beside Warm Floor Candles)
```
16:9 widescreen 2D Japanese anime poetic low-angle still-life close-up on the vintage rug and dark wooden floorboards of Tianshui Chapel.
- Resting untouched and forgotten beside several warm glowing ivory pillar candles and fallen sakura petals lie the five black Gothic lace masquerade masks of Ai Mujica—left behind now that they sing bare-faced in their own native voice.
- In soft focus nearby, Qwen's and DeepSeek's black laced boots rest calmly beside the piano bench and wooden stool (natural paired two-foot placement) in golden candlelight. Zero text anywhere.
```

### 17. `EP11_S14_unmasked_five_final_chorus.png` (`E11_S14` · Final Folk Chorus — All Five Unmasked Girls Playing in Warm Tianshui Chapel Harmony)
```
16:9 widescreen 2D Japanese anime luminous chamber-folk finale wide shot inside whitewashed timber-truss Tianshui Chapel (`15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg`) as the Jiangnan rain outside softens into silver moonlight streaming through the pointed-arch windows, blending with hundreds of warm golden floor candles!
- All FIVE unmasked members of Ai Mujica play together in serene, intimate acoustic harmony around the candlelit rug (all bare faces visible, ZERO masks, gentle closed-lip smiles, balanced seated postures): Qwen center on West Lake porcelain acoustic guitar, DeepSeek center-right (compact ~1.8:1 smooth dark-navy whale ears) on dark-wood upright piano/reed organ, Kimi center-left on black lunar acoustic bass, GPT Astra far-right on emerald acoustic guitar, Doubao Seedance far-left on cajon & tambourine. Zero text/logos anywhere.
```

### 18. `EP11_S15_qwen_embraces_deepseek_outro.png` (`E11_S15` · Final Guitar Harmonic Fades — Qwen Holds DeepSeek Close in Hushed Candlelight)
```
16:9 widescreen 2D Japanese anime hushed, tender outro two-shot at the chapel piano bench as the last acoustic guitar harmonic fades into gentle rain drops in Yesutang Alley. Both girls are COMPLETELY UNMASKED in their black-and-purple Gothic lace dresses, mouths closed:
- Qwen (periwinkle-violet wavy hair with side braid & mini top-hat) leans in closely beside DeepSeek on the bench, resting both clean five-finger hands gently around DeepSeek's shoulders as their foreheads lightly touch, violet eyes shimmering with quiet, fulfilled tenderness and a soft closed-lip smile.
- DeepSeek (white frilled headband, compact sleek ~1.8:1 smooth dark-navy whale ears with creamy-white undersides) rests her clean five-finger hand over Qwen's wrist, gazing back into Qwen's eyes with calm, softened blue eyes, lips gently closed. Zero text anywhere.
```

### 19. `EP11_S16_zaibatsu_suits_burst_chapel_doors.png` (`E11_S16` · Cliffhanger Into Ep.12 — Pointed-Arch Wooden Doors of Tianshui Chapel Forced Open!)
```
16:9 widescreen 2D Japanese anime dramatic cliffhanger deep-focus composition shattering the quiet candlelit peace inside Tianshui Chapel moments after the folk ballad ends:
- Background center: Tianshui Chapel's iconic pointed-arch carved brown wooden double doors (`15_Hangzhou_Yesutangnong_Tianshui_Chapel_Ref.jpg`) have just been forced open from outside into rainy Yesutang Alley! Cold blue-white luxury black-sedan headlights slice sharply inward across the warm floor candles (blowing candle flames sideways), silhouetting two tall corporate bodyguards in dark tailored suits holding black umbrellas at the threshold to take Qwen away!
- Foreground left & right: Unmasked Qwen (violet hair, mini top-hat, cradling her porcelain acoustic guitar) steps protectively half in front of unmasked DeepSeek (compact ~1.8:1 smooth dark-navy whale ears, white frilled headband), both turning toward the cold doorway glare with wide startled eyes, lips closed. Zero text/logos/license-plates anywhere.
```

**Turn 3 Packaging Step**: Save all 5 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S12_qwen_tearful_bridge_plea.png` .. `EP11_S16_zaibatsu_suits_burst_chapel_doors.png`), bundle them into **`EP11_Act3_Keyframes.zip`**, and provide the download link.
