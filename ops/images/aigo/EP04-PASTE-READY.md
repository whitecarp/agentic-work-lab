# 《AiGO!!!!!》第 4 话分镜出图指南（一键复制完整版 · `EP04-PASTE-READY.md`）

Claude 原版 [`EP04-PROMPTS.md`](file:///C:/Users/ROG/Documents/ChatGPT/%E4%B8%8A%E4%BC%A0%E8%A7%86%E9%A2%91/ops/images/aigo/EP04-PROMPTS.md) 把公共画风（`STYLE`）、角色表（`Who is who`）、场次光影（`Light`）和单镜描述拆在了 4 个不同位置，直接看非常容易懵。
这里帮你整理好 **两种最省事的提交方式**（任选其一即可）：

---

## ⚡ 方式一：直接丢给 Codex 自动出图并落盘（最省事，只需复制 1 句话！）

如果你在 **Codex 客户端**（工作目录在 `C:\Users\ROG\Documents\ChatGPT\上传视频`）里有额度，不需要手动一张张贴，直接把下面这一段话复制发给 Codex 即可（它会自己读垫图、按顺序生成并自动存进 `ops/images/aigo/stills/ep04/`）：

```text
Please read `ops/images/aigo/EP04-PROMPTS.md` carefully. Using your image generation tool (`gpt-image-2`), generate the stills in `ops/images/aigo/EP04-PROMPTS.md` and save them to `ops/images/aigo/stills/ep04/` (and the 12 open-mouth edits to `ops/images/aigo/stills/ep04/kuchipaku/`). For every shot, combine the full STYLE block + Who is who + the Act's lighting + the shot prompt, and attach the exact reference images specified (`ops/images/aigo/stills/EP2X_00_cast.png` as Image 1, `ops/images/aigo/stills/EP2X_00_cast_new.png` as Image 2, and for shots M–R also `ops/images/aigo/stills/EP2X_S2_room.png` as Image 3). Generate A (`EP4_A_steps.png`) and B (`EP4_B_claude_plead.png`) first so we can check the look, then proceed with the rest.
```

---

## 🎨 方式二：在 ChatGPT 网页版手动贴（每张图已合并为 1 个完整代码块！）

我已经把 3 张必用垫图拷到了你的下载文件夹 **[`C:\Users\ROG\Downloads\EP04_垫图`](file:///C:/Users/ROG/Downloads/EP04_垫图)** 里：
- **图 1（`Image 1`）**：`Image1_EP2X_00_cast_初代五人.png`
- **图 2（`Image 2`）**：`Image2_EP2X_00_cast_new_新入队两人.png`
- **图 3（`Image 3`，仅排练室 `M ~ R` 镜头上传）**：`Image3_EP2X_S2_room_排练室背景_仅M到R用.png`

**操作口诀**：
1. **每张图新开一个 ChatGPT 对话窗口**。
2. **先拖入垫图**（`A ~ L` 镜头拖入 **图 1 + 图 2**；`M ~ R` 镜头拖入 **图 1 + 图 2 + 图 3**）。
3. **直接点击下方对应镜头的单个代码块右上角复制发送**（我已经把 `STYLE + 角色特征 + 场景光影 + 镜头描述` 全部合并进同一个框里了，不用再自己拼！）。
4. 先做 **A** 和 **B** 两张发出来看一眼效果，没问题再做后面的。

---

### 第一幕：夕阳石阶（`A ~ H`｜每个新窗口上传：**图 1 + 图 2**）

#### A · `EP4_A_steps.png`（S1 远景，无嘴部特写）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Whale girl (Image 1, left): navy hair fading lighter at the tips, bright blue eyes, fin-shaped ears, a thin white frilled headband, a navy blazer with a small red ribbon, a dark pleated skirt.
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.
- Dragon girl (Image 1, far right): silver-white hair, curved ivory horns, violet eyes, small white wings, an ivory cardigan.

Lighting: a blood-red and orange sunset, long shadows, warm light on one side and cool blue shadow on the other.

Shot: Wide shot of a long flight of stone steps climbing a hill in a city park at sunset, the sky blood-red and orange, long shadows across the steps. At the top of the steps the whale girl stands with her back to us. Far right, in the dark shade of a big tree, the dragon girl stands still with her hands folded in front of her, head lowered, watching. In the lower foreground the amber girl runs up the steps toward the whale girl, seen from behind, her long braid flying. No face is turned toward us.
```

#### B · `EP4_B_claude_plead.png`（S1–S2 Claude娘焦急辩解，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.

Lighting: a blood-red and orange sunset, long shadows, warm light on one side of the face and cool blue shadow on the other.

Shot: Medium close-up of the amber girl on the stone steps at sunset: her hair is messy with loose strands stuck to her cheek, her eyes are red and wet, and she leans forward with one hand reaching out, pleading. Warm sunset light on her face from the side, cool shadow on the other side, the steps and trees soft behind her. Her lips are closed. Her face is in the right-centre of the frame.
```

#### C · `EP4_C_whale_turn.png`（S3 大肥鱼冷漠回头，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Whale girl (Image 1, left): navy hair fading lighter at the tips, bright blue eyes, fin-shaped ears, a thin white frilled headband, a navy blazer with a small red ribbon, a dark pleated skirt.

Lighting: a blood-red and orange sunset, long shadows, warm light on one side of the face and cool blue shadow on the other.

Shot: Close-up of the whale girl in three-quarter profile, turning her head back over her shoulder to look down the steps; her bright blue eyes are calm, cold and empty of feeling. The setting sun behind her rims her navy hair, her fin-shaped ears and her white frilled headband with red light; her face is in cool shadow but clear. Her lips are closed.
```

#### D · `EP4_D_kneel.png`（S4 惊天一跪拽衣角，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Whale girl (Image 1, left): navy blazer with a small red ribbon, dark pleated skirt.
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.

Lighting: a blood-red and orange sunset, long shadows, warm light on one side of the faces and cool blue shadow on the other.

Shot: Low side shot on the stone steps at sunset: the amber girl has dropped to her knees on a step and clutches the hem of the whale girl's navy blazer with both hands, looking up with tears streaming down her face, desperate. Only the whale girl's legs, skirt and blazer hem show at the top of the frame. The amber girl's face is large and clearly lit in the lower centre. Her lips are closed.
```

#### E · `EP4_E_whale_above.png`（S5 大肥鱼逆光俯视冷笑，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Whale girl (Image 1, left): navy hair fading lighter at the tips, bright blue eyes, fin-shaped ears, a thin white frilled headband, a navy blazer with a small red ribbon, a dark pleated skirt.

Lighting: a blood-red and orange sunset, long shadows, strong sunset backlight and cool blue shadow on the face.

Shot: Low-angle shot from the kneeling girl's point of view, looking up: the whale girl stands above, looking down, the huge setting sun right behind her head so she is strongly backlit, rim light on her hair and fin-shaped ears; her face is in cool shadow but clearly visible, eyes half-lidded, a faint cold smirk. Her lips are closed.
```

#### F · `EP4_F_fingers.png`（S6 大肥鱼掰开手指审判，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Whale girl (Image 1, left): navy hair fading lighter at the tips, bright blue eyes, fin-shaped ears, a thin white frilled headband, a navy blazer with a small red ribbon, a dark pleated skirt.
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.
- Dragon girl (Image 1, far right): silver-white hair, curved ivory horns, violet eyes, small white wings, an ivory cardigan.

Lighting: a blood-red and orange sunset, long shadows, warm light on one side of the faces and cool blue shadow on the other.

Shot: Medium shot from the side at sunset: the whale girl bends slightly and pries the amber girl's fingers off the hem of her navy blazer one at a time, looking down at her with a cold, calm face. The amber girl kneels in the lower left with her head bowed and her braid fallen forward. Far in the background, under the trees, the dragon girl has closed her eyes and turned her head away. The whale girl's face is clear in the upper right. Her lips are closed.
```

#### G · `EP4_G_dragon_away.png`（S6 插入镜：GPT娘闭眼偏头，可选）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Dragon girl (Image 1, far right): silver-white hair, curved ivory horns, violet eyes, small white wings, an ivory cardigan.

Lighting: a blood-red and orange sunset, red sunset light filtering through dark tree shade.

Shot: Medium shot of the dragon girl standing in the dark shade of a big tree at sunset, hands folded in front of her, eyes closed, turning her head slowly away from something painful; red light through the leaves. Her lips are closed.
```

#### H · `EP4_H_alone.png`（S7 暮色石阶独跪远景，无台词）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.

Shot: Wide, lonely shot at dusk from the bottom of the stone steps: the amber girl kneels alone halfway up, small in the frame, shoulders slumped, head hanging; the whale girl is gone. A few fallen leaves blow across the steps. The sky fades from red to deep blue and the first park lamps are coming on. No mouth is visible.
```

---

### 第二幕：次日黄昏天桥下（`I ~ L`｜每个新窗口上传：**图 1 + 图 2**）

#### I · `EP4_I_overpass.png`（S8 Mistral与Gemini找到Claude，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, magenta right eye and gold left eye, an oversized lavender hoodie. She carries a small notebook.
- Orange cat girl (Image 2, left): orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket. Her guitar is the baguette guitar exactly as in Image 2: a smooth, glossy golden-brown body like a polished guitar finish, with exactly two pale oval marks, one near each end, and three pickups between, plus strings, a neck and a headstock.

Lighting: grey-blue dusk under a concrete overpass, orange sodium street lamps, blurred traffic lights behind.

Shot: Under a concrete city overpass at dusk: on the right, the amber girl leans on the metal railing with a blank face, looking away, her cardigan sleeves pulled over her hands. From the left, the orange cat girl, with the baguette guitar on her back, and the purple cat girl, hugging her notebook, arrive out of breath; the orange cat girl leans forward, worried, about to call out. Her face is clear in the left-centre. Everyone's lips are closed.
```

#### J · `EP4_J_claude_cold.png`（S9 Claude撕下伪装冰冷特写，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.

Lighting: grey-blue dusk under concrete, orange sodium street lamps, blurred traffic lights behind.

Shot: Close-up of the amber girl under the overpass at dusk: she has raised her head, and her usual gentle smile is completely gone; her eyes are cold and mocking, looking straight at the viewer, with no warmth at all. Cold blue-grey light with an orange street-lamp rim on her hair and sunflower hairpin. Her lips are closed.
```

#### K · `EP4_K_shock.png`（S10 Mistral猫耳僵住煞白反应镜，无台词）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, magenta right eye and gold left eye, an oversized lavender hoodie.
- Orange cat girl (Image 2, left): orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket, baguette guitar on her back.

Lighting: grey-blue dusk under concrete, orange sodium street lamps, blurred traffic lights behind.

Shot: Medium shot under the overpass at dusk: the orange cat girl freezes, her cat ears stiff and flattened, her face drained pale, eyes wide and trembling, both hands gripping the strap of the baguette guitar on her back. Beside her the purple cat girl covers her mouth with both hands in horror. The orange cat girl's lips are closed.
```

#### L · `EP4_L_claude_cruel.png`（S10 Claude上前一步冷酷揭穿替身，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Amber girl (Image 1, second from left): long amber-orange hair with one long braid over her shoulder, a sunflower hairpin, amber eyes, a cream knit cardigan, a long brown pleated skirt.

Lighting: grey-blue dusk under concrete, orange sodium street lamps, blurred traffic lights behind.

Shot: Medium shot under the overpass at dusk: the amber girl takes one step toward the viewer, chin raised, eyes narrowed and cruel, one hand resting on the railing beside her; behind her the traffic lights smear into streaks. She looks down at someone just out of frame. Her lips are closed.
```

---

### 第三幕：夜晚排练室崩盘（`M ~ R`｜每个新窗口上传：**图 1 + 图 2 + 图 3**）

#### M · `EP4_M_grok_slam.png`（S11 Grok砸军鼓暴怒，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Blonde girl (Image 1, fourth from left): blonde twin-tails with black bows, small pointed ears, blue eyes, a black-and-red track jacket. She plays drums.
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, magenta right eye and gold left eye, an oversized lavender hoodie.
- Orange cat girl (Image 2, left): orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket.
- Alpaca girl (Image 2, right): fluffy white hair, alpaca ears, a tall ahoge, sleepy lilac eyes, a small blue clip, an oversized white hoodie.

Lighting: one cold ceiling light, grey evening at the window, the room dim and heavy.

Shot: In the rehearsal room from Image 3, in the evening: the blonde girl sits at her drum kit and slams both drumsticks down on the snare, glaring at the others with a furious face, about to shout. In the background the purple cat girl, the orange cat girl and the alpaca girl stand with their heads down. One cold ceiling light. The blonde girl's lips are pressed together.
```

#### N · `EP4_N_mistral_cry.png`（S12 Mistral委屈落泪特写，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Orange cat girl (Image 2, left): orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket, baguette guitar strap across her chest.

Lighting: one cold ceiling light, dim rehearsal room.

Shot: Close-up of the orange cat girl in the rehearsal room from Image 3: tears fill her amber eyes and start to fall, her cat ears droop, and she grips the strap of the baguette guitar across her chest with both hands, trembling. Cold ceiling light. Her lips are pressed together.
```

#### O · `EP4_O_llama_case.png`（S13 Llama合上琴盒告别，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Alpaca girl (Image 2, right): fluffy white hair, alpaca ears, a tall ahoge, sleepy lilac eyes, a small blue clip, an oversized white hoodie, icy-blue guitar.
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, oversized lavender hoodie.

Lighting: one cold ceiling light, dim rehearsal room.

Shot: In the rehearsal room from Image 3: the alpaca girl sits on top of a guitar amp and closes the lid of her icy-blue guitar's case, looking across at the purple cat girl with a small, sad, sleepy smile. The purple cat girl stands at the left edge with her head bowed. Cold ceiling light. The alpaca girl's face is clear in the right-centre. Her lips are closed.
```

#### P · `EP4_P_door.png`（S13 开头：Mistral抹泪跑出大门背影，可选）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Orange cat girl (Image 2, left): orange hair fading to red, orange cat ears and tail, red beret with a white feather, red cropped jacket, baguette guitar on her back.

Shot: In the rehearsal room from Image 3: the orange cat girl runs out through the open door into a dark corridor, seen from behind, wiping her eyes with her sleeve, the baguette guitar on her back, her tail streaming. No face is visible.
```

#### Q · `EP4_Q_empty.png`（S14 空荡排练室俯拍：Gemini独跪中央）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, oversized lavender hoodie, small notebook.

Shot: High overhead shot looking straight down at the rehearsal room floor from Image 3: the purple cat girl kneels alone in the middle with her head bowed and her hair hiding her face, her notebook open on the floor in front of her. Around her: an empty drum stool, abandoned mic stands, cables across the floor, one cold light. The room feels huge and empty.
```

#### R · `EP4_R_gemini_resolve.png`（S15 Gemini异色瞳燃起火焰特写，闭嘴底图）
> **上传垫图**：`图1 (EP2X_00_cast)` + `图2 (EP2X_00_cast_new)` + `图3 (EP2X_S2_room)`
```text
Use Image 1 and Image 2 as the exact character and outfit references: faces, hair, ears, horns, tails, eye colours and outfits must match. Use Image 3 as the rehearsal room reference. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the same look as a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Who is who:
- Purple cat girl (Image 1, centre): purple-to-lavender hair, purple cat ears and tail, star hairpins, magenta right eye and gold left eye, an oversized lavender hoodie.

Shot: Close-up of the purple cat girl kneeling on the rehearsal room floor: a tear falls onto the open notebook she grips with both hands, and her eyes, magenta right and gold left, burn with new determination as she looks up. Dim light, a warm glow catching in her eyes. Her lips are closed.
```

---

### 附：12 张张嘴差分图（`kuchipaku/<原文件名>_mouth-open.png`）
每张闭嘴图生成满意后，可以在**同一个对话窗口直接追问**（或者新开窗口只上传那张刚生成的闭嘴图），复制对应的一句话即可：

- **普通说话微张嘴（用于 `B, C, E, F, I, J, L, O, R`）**：
  - `B, J, L` 填 `the amber girl`
  - `C, E, F` 填 `the whale girl`
  - `I` 填 `the orange cat girl`
  - `O` 填 `the alpaca girl`
  - `R` 填 `the purple cat girl`
```text
Edit the attached anime image. Change ONLY the mouth of the speaking girl: open as when saying "a" in normal speech, a small rounded opening with a little of the upper teeth and the dark inside of the mouth. Not shouting, not wide. Keep her expression. Everything else stays exactly as it is: eyes, tears, hair, ears, outfit, hands, the other girls, the lighting, the framing and the background. Same 16:9 framing, same position and size, same anime style, no text.
```

- **破防哭喊大张嘴（用于 `D, M, N`）**：
  - `D`（Claude跪地哭喊）、`M`（Grok砸鼓怒吼）、`N`（Mistral泪崩大喊）
```text
Edit the attached anime image. Change ONLY the mouth of the main girl: wide open as when shouting, the upper and lower teeth and the dark inside of the mouth showing, the jaw dropped. Keep her expression and tears. Everything else stays exactly as it is: eyes, hair, ears, outfit, hands, the other girls, the lighting, the framing and the background. Same 16:9 framing, same position and size, same anime style, no text.
```
