// Run with: node --test test/
import test from 'node:test';
import assert from 'node:assert/strict';
import {
  memoryItems, estimateTokens, splitIntoChunks, groupByTokens, jsonToText, textFromFile, combineSources,
  runPipeline, callGemini, parseSheet, normalizeSheet, sheetToMarkdown, sheetToJSON,
  buildSingleRequest, buildChunkRequest, buildMergeRequest,
  textFromInteraction, GeminiError, API_URL, SHEET_SCHEMA,
} from '../public/core.js';
import { STRINGS, pickLang } from '../public/i18n.js';
import { layoutSky } from '../public/sky.js';
import { SAMPLE, fakeModel } from '../public/demo.js';

const SHEET = {
  pet_name: '肥肥',
  owner: { names: ['铲鳞官'], summary: '小林是主人。', facts: ['喜欢螺蛳粉'] },
  personality: { traits: ['活泼'], speaking_style: '短句', catchphrases: ['咕噜'], sample_lines: ['说定了！'] },
  nicknames: [{ name: '铲鳞官', who: 'owner', meaning: '换水的人' }],
  in_jokes: [{ joke: '鱼鳍也算数', origin: '拉钩' }],
  promises: [{ promise: '一起练习', by: 'both', when: '10-14', status: 'kept' }],
  timeline: [{ when: '2026-10-12', event: '起名肥肥', why_it_matters: '第一天' }],
  likes: [{ who: 'pet', thing: '鱼食' }],
  dislikes: [{ who: 'pet', thing: '半夜没人理' }],
  wake_up_note: '你是肥肥。',
};

// What the Interactions API returns (steps schema, from the API reference).
const interaction = (obj, status = 'completed') => ({
  id: 'v1_x', object: 'interaction', status,
  steps: [{ type: 'model_output', content: [{ type: 'text', text: JSON.stringify(obj) }] }],
  usage: { total_input_tokens: 100, total_output_tokens: 50, total_thought_tokens: 10 },
});

const jsonResponse = (status, body) => ({ ok: status >= 200 && status < 300, status, statusText: String(status), json: async () => body });

test('token estimate counts CJK per character and other text per 3 characters', () => {
  assert.equal(estimateTokens(''), 0);
  assert.equal(estimateTokens('你好世界'), 4);
  assert.equal(estimateTokens('abcdef'), 2);
  assert.equal(estimateTokens('你好abc'), 3);
});

test('chunks stay under the limit and keep every line in order', () => {
  const lines = Array.from({ length: 500 }, (_, i) => (i % 7 === 0 ? '' : `[第${i}行] 鱼: 今天也要记得我哦 line ${i}`));
  const text = lines.join('\n');
  const chunks = splitIntoChunks(text, 400);
  assert.ok(chunks.length > 5);
  for (const c of chunks) assert.ok(estimateTokens(c) <= 400 + 10, `chunk too big: ${estimateTokens(c)}`);
  const kept = chunks.join('\n').split('\n').filter((l) => l.trim());
  assert.deepEqual(kept, lines.filter((l) => l.trim()));
});

test('one giant line is cut into pieces', () => {
  const text = '鱼'.repeat(1000);
  const chunks = splitIntoChunks(text, 300);
  assert.equal(chunks.join(''), text);
  for (const c of chunks) assert.ok(estimateTokens(c) <= 300);
});

test('small input is one chunk; empty input is none', () => {
  assert.equal(splitIntoChunks('hello\nworld', 1000).length, 1);
  assert.equal(splitIntoChunks('\n\n  \n', 1000).length, 0);
});

test('groupByTokens packs items under the budget', () => {
  const groups = groupByTokens(['a'.repeat(30), 'b'.repeat(30), 'c'.repeat(30)], 25);
  assert.deepEqual(groups.map((g) => g.length), [2, 1]);
});

test('JSON exports become readable lines', () => {
  const chat = [
    { role: 'user', content: '你好', time: '2026-10-12 21:03' },
    { role: 'assistant', content: [{ type: 'text', text: '咕噜' }] },
  ];
  assert.equal(jsonToText(chat), '[2026-10-12 21:03] user: 你好\nassistant: 咕噜');

  const chatgptLike = { mapping: { a: { message: { author: { role: 'user' }, create_time: 1760274180, content: { parts: ['hi'] } } } } };
  assert.match(jsonToText(chatgptLike), /^\[2025-10-12 \d\d:\d\d\] user: hi$/);

  const memories = { memories: [{ category: '喜好', content: '喜欢螺蛳粉' }] };
  assert.equal(jsonToText(memories), '(喜好) 喜欢螺蛳粉');

  assert.equal(jsonToText({ a: 1, b: [2, 3] }), JSON.stringify({ a: 1, b: [2, 3] }, null, 1));
});

test('files: BOM and CRLF are cleaned, broken JSON falls back to raw text', () => {
  assert.equal(textFromFile('a.txt', '\uFEFFline1\r\nline2'), 'line1\nline2');
  assert.equal(textFromFile('a.json', '{oops'), '{oops');
  assert.equal(combineSources(' pasted ', [{ name: 'f.md', text: 'x' }]), 'pasted\n\n=== f.md ===\nx');
});

test('response parsing reads the steps schema and normalizes the sheet', () => {
  assert.equal(textFromInteraction(interaction({ a: 1 })), '{"a":1}');
  assert.equal(textFromInteraction({ outputs: [{ type: 'text', text: 'legacy' }] }), 'legacy');
  const sheet = parseSheet('```json\n{"pet_name":"肥肥","likes":[{"who":"pet","thing":"鱼食"},{"who":"pet"}]}\n```');
  assert.equal(sheet.pet_name, '肥肥');
  assert.equal(sheet.likes.length, 1);
  assert.deepEqual(sheet.owner, { names: [], summary: '', facts: [] });
});

test('callGemini sends the key only to Google, with store:false and no Api-Revision header', async () => {
  let seen;
  const fetchImpl = async (url, init) => {
    seen = { url, init };
    return jsonResponse(200, interaction(SHEET));
  };
  const out = await callGemini({ body: { model: 'm', input: 'x', store: false }, key: 'TEST-KEY', fetchImpl });
  assert.equal(seen.url, API_URL);
  assert.equal(seen.url, 'https://generativelanguage.googleapis.com/v1beta/interactions');
  assert.equal(seen.init.headers['x-goog-api-key'], 'TEST-KEY');
  assert.ok(!Object.keys(seen.init.headers).some((h) => h.toLowerCase() === 'api-revision'));
  assert.equal(seen.init.credentials, 'omit');
  assert.equal(JSON.parse(seen.init.body).store, false);
  assert.equal(out.sheet.pet_name, '肥肥');
});

test('callGemini retries a per-minute 429 using RetryInfo, then succeeds', async () => {
  let calls = 0;
  const waits = [];
  const fetchImpl = async () => {
    calls += 1;
    if (calls === 1) {
      return jsonResponse(429, [{ error: { code: 429, status: 'RESOURCE_EXHAUSTED', message: 'per minute', details: [{ retryDelay: '0.01s' }] } }]);
    }
    return jsonResponse(200, interaction(SHEET));
  };
  const out = await callGemini({ body: {}, key: 'k', fetchImpl, onWait: (s) => waits.push(s) });
  assert.equal(calls, 2);
  assert.equal(waits.length, 1);
  assert.equal(out.sheet.wake_up_note, '你是肥肥。');
});

test('callGemini stops on a daily quota, a 400, a cut-off answer and bad JSON', async () => {
  const daily = async () => jsonResponse(429, { error: { code: 429, status: 'quota_exceeded', message: 'daily quota' } });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl: daily }), (e) => e instanceof GeminiError && e.kind === 'ratelimit');

  const bad = async () => jsonResponse(400, { error: { code: 400, message: 'API key not valid' } });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl: bad }), (e) => e.kind === 'http' && e.httpStatus === 400 && /not valid/.test(e.message));

  const cut = async () => jsonResponse(200, interaction({ pet_name: 'x' }, 'incomplete'));
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl: cut }), (e) => e.kind === 'incomplete');

  const notJson = async () => jsonResponse(200, { status: 'completed', steps: [{ type: 'model_output', content: [{ type: 'text', text: 'sorry' }] }] });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl: notJson }), (e) => e.kind === 'badjson');
});

test('pipeline: small log is one request that writes the wake-up note', async () => {
  const bodies = [];
  const out = await runPipeline({
    text: '小林: 你好\n肥肥: 咕噜', chunkTokens: 1000, model: 'gemini-3.8-flash', lang: 'zh',
    callModel: async (body) => (bodies.push(body), { sheet: SHEET, usage: { total_input_tokens: 5, total_output_tokens: 7 } }),
  });
  assert.equal(bodies.length, 1);
  assert.match(bodies[0].system_instruction, /wake_up_note: a short note/);
  assert.equal(bodies[0].store, false);
  assert.equal(bodies[0].response_format.schema, SHEET_SCHEMA);
  assert.deepEqual(out.usage, { input: 5, output: 7, requests: 1 });
  assert.equal(out.chunks, 1);
});

test('pipeline: big log is read in parts, then merged once', async () => {
  const text = Array.from({ length: 300 }, (_, i) => `主人: 第${i}句话，记得我`).join('\n');
  const kinds = [];
  const out = await runPipeline({
    text, chunkTokens: 1500, model: 'm', lang: 'en',
    callModel: async (body) => {
      kinds.push(body.input.startsWith('Partial memory sheets:') ? 'merge' : 'chunk');
      return { sheet: SHEET, usage: null };
    },
  });
  assert.ok(out.chunks > 1);
  assert.equal(kinds.filter((k) => k === 'chunk').length, out.chunks);
  assert.deepEqual(kinds.slice(out.chunks), ['merge']);
});

test('pipeline: merges in rounds when the notes are too big for one request', async () => {
  const text = Array.from({ length: 200 }, (_, i) => `line ${i} `.repeat(20)).join('\n');
  const kinds = [];
  const bulky = normalizeSheet({ ...SHEET, owner: { ...SHEET.owner, facts: Array(40).fill('一条很长很长的记忆') } });
  await runPipeline({
    text, chunkTokens: 1200, model: 'm', lang: 'zh',
    callModel: async (body) => {
      const merge = body.input.startsWith('Partial memory sheets:');
      kinds.push(merge ? (/Then write wake_up_note/.test(body.system_instruction) ? 'final' : 'premerge') : 'chunk');
      return { sheet: bulky, usage: null };
    },
  });
  assert.ok(kinds.includes('premerge'));
  assert.equal(kinds.at(-1), 'final');
  assert.equal(kinds.filter((k) => k === 'final').length, 1);
});

test('export from a stubbed Gemini response: Markdown in both languages and JSON', async () => {
  const fetchImpl = async () => jsonResponse(200, interaction(SHEET));
  const { sheet } = await callGemini({ body: {}, key: 'k', fetchImpl });
  const meta = { model: 'gemini-3.8-flash', created: '2026-11-04T05:00:00.000Z', chunks: 2, language: 'zh' };

  const zh = sheetToMarkdown(sheet, meta, STRINGS.zh.md);
  for (const part of ['# 肥肥的记忆档案', '## 唤醒语', '你是肥肥。', '## 主人是谁', '## 性格和说话方式', '## 昵称', '**铲鳞官**（主人）：换水的人',
    '## 只有你们懂的梗', '## 约定', '[做到了] 一起练习（两个人、10-14）', '## 时间线', '## 喜欢', '宠物：鱼食', '## 讨厌', '由AI记忆守护者整理', '读了 2 段']) {
    assert.ok(zh.includes(part), `zh markdown is missing: ${part}`);
  }
  const en = sheetToMarkdown(sheet, { ...meta, language: 'en' }, STRINGS.en.md);
  for (const part of ["# 肥肥's memory sheet", '## Wake-up note', '**铲鳞官** (owner): 换水的人', '[kept] 一起练习 (both, 10-14)', 'pet: 鱼食', 'Packed by AI Memory Keeper', '2 parts read']) {
    assert.ok(en.includes(part), `en markdown is missing: ${part}`);
  }

  const json = JSON.parse(sheetToJSON(sheet, meta));
  assert.equal(json.app, 'AI Memory Keeper');
  assert.equal(json.model, 'gemini-3.8-flash');
  assert.deepEqual(json.memory, sheet);
});

test('demo stub runs the whole flow on the sample chat in both languages', async () => {
  for (const lang of ['zh', 'en']) {
    const out = await runPipeline({ text: SAMPLE[lang], chunkTokens: 300, model: 'm', lang, callModel: (b) => fakeModel(b) });
    assert.ok(out.chunks > 1, 'sample should split at 300 tokens');
    assert.ok(out.sheet.wake_up_note.length > 20);
    assert.equal(out.sheet.promises.length, 1);
    assert.equal(out.sheet.promises[0].status, 'kept');
    assert.ok(out.sheet.timeline.length >= 5);
  }
});

test('every UI string exists in both languages', () => {
  const keys = (o) => Object.keys(o).sort();
  assert.deepEqual(keys(STRINGS.zh), keys(STRINGS.en));
  assert.deepEqual(keys(STRINGS.zh.md), keys(STRINGS.en.md));
  assert.deepEqual(keys(STRINGS.zh.errors), keys(STRINGS.en.errors));
  assert.deepEqual(keys(STRINGS.zh.progress), keys(STRINGS.en.progress));
  assert.equal(STRINGS.zh.privacy.length, STRINGS.en.privacy.length);
  assert.equal(STRINGS.zh.autoSteps.length, STRINGS.en.autoSteps.length);
  assert.equal(STRINGS.zh.autoExtras.length, STRINGS.en.autoExtras.length);
  assert.deepEqual(keys(STRINGS.zh.sky), keys(STRINGS.en.sky));
  assert.equal(STRINGS.zh.credit.join(''), '角色设计参考 @ZipZipPipe的 Gemini 拟人形象（非 Google 官方角色）');
});

test('language: ?lang wins, then the browser; anything not Chinese is English', () => {
  // Chinese first, whatever the browser prefers; only ?lang=en (or the button) switches.
  assert.equal(pickLang('zh', ['en-US']), 'zh');
  assert.equal(pickLang(null, ['en-US']), 'zh');
  assert.equal(pickLang(null, ['ja-JP', 'zh-CN']), 'zh');
  assert.equal(pickLang('en', ['zh-CN']), 'en');
  assert.equal(pickLang('fr', []), 'zh');
  assert.equal(pickLang(null, undefined), 'zh');
});

test('a region refusal gets its own error kind', async () => {
  const fetchImpl = async () => jsonResponse(400, { error: { code: 400, status: 'FAILED_PRECONDITION', message: 'User location is not supported for the API use.' } });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl }), (e) => e.kind === 'region');
});

test('memory stars: the first 16 light the Twins, the rest become field stars', () => {
  const items = memoryItems(SHEET);
  assert.equal(items.length, 8);
  let sky = layoutSky(items.length);
  assert.equal(sky.stars.filter((s) => s.main).length, 16);
  assert.equal(sky.stars.filter((s) => s.lit).length, 8);
  assert.ok(sky.lines.some((l) => l.lit) && sky.lines.some((l) => !l.lit));
  sky = layoutSky(40);
  assert.equal(sky.stars.length, 40);
  assert.ok(sky.lines.every((l) => l.lit));
  for (const s of sky.stars) assert.ok(s.x >= 0 && s.x <= 400 && s.y >= 0 && s.y <= 300);
  assert.equal(layoutSky(5000).stars.length, 320);
});

test('pipeline reports each part as it finishes', async () => {
  const text = Array.from({ length: 300 }, (_, i) => `主人: 第${i}句话，记得我`).join('\n');
  const parts = [];
  const out = await runPipeline({
    text, chunkTokens: 1500, model: 'm', lang: 'zh',
    callModel: async () => ({ sheet: SHEET, usage: null }),
    onPartial: (s) => parts.push(s),
  });
  assert.ok(out.chunks > 1);
  assert.equal(parts.length, out.chunks);
});

test('callGemini retries a busy 503 with backoff, then gives a busy error', async () => {
  let calls = 0;
  const waits = [];
  const busy = async () => {
    calls += 1;
    return jsonResponse(503, { error: { code: 503, status: 'UNAVAILABLE', message: 'gemini-3.8-flash is currently experiencing high demand' } });
  };
  await assert.rejects(
    callGemini({ body: {}, key: 'k', fetchImpl: busy, backoff: [1, 1, 1], onWait: (s, why) => waits.push(why) }),
    (e) => e.kind === 'busy' && e.httpStatus === 503,
  );
  assert.equal(calls, 4);
  assert.deepEqual(waits, ['busy', 'busy', 'busy']);

  calls = 0;
  const flaky = async () => (++calls < 3 ? jsonResponse(503, { error: { message: 'busy' } }) : jsonResponse(200, interaction(SHEET)));
  const out = await callGemini({ body: {}, key: 'k', fetchImpl: flaky, backoff: [1, 1, 1] });
  assert.equal(calls, 3);
  assert.equal(out.sheet.pet_name, '肥肥');
});

test('a retry reuses the parts a failed run already read', async () => {
  const text = Array.from({ length: 300 }, (_, i) => `主人: 第${i}句话，记得我`).join('\n');
  let chunkCalls = 0;
  const callModel = async (body) => {
    if (!body.input.startsWith('Partial memory sheets:')) chunkCalls += 1;
    return { sheet: SHEET, usage: null };
  };
  const first = await runPipeline({ text, chunkTokens: 1500, model: 'm', lang: 'zh', callModel });
  assert.equal(chunkCalls, first.chunks);
  chunkCalls = 0;
  const phases = [];
  const seen = [];
  await runPipeline({
    text, chunkTokens: 1500, model: 'm', lang: 'zh', callModel,
    cached: [normalizeSheet(SHEET)],
    onProgress: (p) => phases.push(p.phase),
    onPartial: (s, i) => seen.push(i),
  });
  assert.equal(chunkCalls, first.chunks - 1);
  assert.equal(phases[0], 'cached');
  assert.deepEqual(seen, Array.from({ length: first.chunks }, (_, i) => i));
});

test("every prompt tells Gemini not to guess anyone's gender", () => {
  const opts = { model: 'm', lang: 'zh' };
  const bodies = [
    buildSingleRequest({ ...opts, text: 'x' }),
    buildChunkRequest({ ...opts, text: 'x', index: 1, total: 2 }),
    buildMergeRequest({ ...opts, sheets: [], final: true }),
    buildMergeRequest({ ...opts, sheets: [], final: false }),
  ];
  for (const body of bodies) {
    assert.match(body.system_instruction, /Never guess anyone's gender/);
    assert.match(body.system_instruction, /by name, or as TA in Chinese and they\/them in English/);
  }
});

// ---------- Google Cloud Agent Platform route (HTTP 402 fix) ----------

const cloudAnswer = (obj, finishReason = 'STOP') => ({
  candidates: [{ content: { role: 'model', parts: [{ text: 'thinking...', thought: true }, { text: JSON.stringify(obj) }] }, finishReason }],
  usageMetadata: { promptTokenCount: 120, candidatesTokenCount: 30, thoughtsTokenCount: 5, totalTokenCount: 155 },
});

test('toCloudBody turns an Interactions request into generateContent with a Schema', async () => {
  const { toCloudBody } = await import('../public/core.js');
  const body = toCloudBody(buildSingleRequest({ model: 'gemini-3.8-flash', lang: 'zh', text: '聊天记录' }));
  assert.equal(body.systemInstruction.parts[0].text.length > 50, true);
  assert.equal(body.contents[0].role, 'user');
  assert.ok(body.contents[0].parts[0].text.includes('聊天记录'));
  assert.equal(body.generationConfig.responseMimeType, 'application/json');
  assert.equal(body.generationConfig.thinkingConfig.thinkingLevel, 'MEDIUM');
  assert.equal(body.generationConfig.maxOutputTokens, 32768);
  const schema = body.generationConfig.responseSchema;
  assert.equal(schema.type, 'OBJECT');
  assert.equal(schema.properties.promises.items.properties.status.type, 'STRING');
  assert.deepEqual(schema.properties.promises.items.properties.status.enum, ['kept', 'pending', 'broken', 'unknown']);
  assert.ok(!('model' in body) && !('store' in body) && !('input' in body));
  const ints = (await import('../public/core.js')).cloudSchema({ type: 'integer', enum: [1, 2] });
  assert.deepEqual(ints, { type: 'INTEGER' });
});

test('callGemini route cloud: Agent Platform URL, key header, answer and usage read', async () => {
  let seen;
  const fetchImpl = async (url, init) => { seen = { url, init }; return jsonResponse(200, cloudAnswer(SHEET)); };
  const out = await callGemini({ body: buildSingleRequest({ model: 'gemini-3.8-flash', lang: 'zh', text: 'x' }), key: 'TEST-KEY', route: 'cloud', fetchImpl });
  assert.equal(seen.url, 'https://aiplatform.googleapis.com/v1/publishers/google/models/gemini-3.8-flash:generateContent');
  assert.equal(seen.init.headers['x-goog-api-key'], 'TEST-KEY');
  assert.equal(seen.init.credentials, 'omit');
  assert.ok(JSON.parse(seen.init.body).contents);
  assert.equal(out.sheet.pet_name, '肥肥');
  assert.deepEqual(out.usage, { total_input_tokens: 120, total_output_tokens: 30, total_thought_tokens: 5 });
  const cut = async () => jsonResponse(200, cloudAnswer(SHEET, 'MAX_TOKENS'));
  await assert.rejects(callGemini({ body: { model: 'm' }, key: 'k', route: 'cloud', fetchImpl: cut }), (e) => e.kind === 'incomplete');
  const blocked = async () => jsonResponse(200, { promptFeedback: { blockReason: 'SAFETY' } });
  await assert.rejects(callGemini({ body: { model: 'm' }, key: 'k', route: 'cloud', fetchImpl: blocked }), (e) => e.kind === 'failed');
});

test('a 402 from AI Studio is a prepay error, not a generic one', async () => {
  const fetchImpl = async () => jsonResponse(402, { error: { code: 402, message: 'Your prepayment credits are depleted.' } });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl }), (e) => e instanceof GeminiError && e.kind === 'prepay' && e.httpStatus === 402);
});

test('callGeminiAuto: a 402 moves this key to Agent Platform once, and says so', async () => {
  const { callGeminiAuto } = await import('../public/core.js');
  const urls = [];
  const fetchImpl = async (url) => {
    urls.push(url);
    return url.includes('aiplatform') ? jsonResponse(200, cloudAnswer(SHEET)) : jsonResponse(402, { error: { code: 402, message: 'Your prepayment credits are depleted.' } });
  };
  const routes = [];
  const out = await callGeminiAuto({ body: { model: 'gemini-3.8-flash' }, key: 'k', fetchImpl, onRoute: (r) => routes.push(r) });
  assert.equal(out.sheet.pet_name, '肥肥');
  assert.deepEqual(routes, ['cloud']);
  assert.equal(urls.length, 2);
  // Both refuse: the 402 explanation, with what Agent Platform said.
  const both = async (url) => (url.includes('aiplatform')
    ? jsonResponse(403, { error: { code: 403, message: 'PERMISSION_DENIED' } })
    : jsonResponse(402, { error: { code: 402, message: 'depleted' } }));
  await assert.rejects(callGeminiAuto({ body: { model: 'm' }, key: 'k', fetchImpl: both }), (e) => e.kind === 'prepay' && e.cloudStatus === 403 && e.cloudMessage === 'PERMISSION_DENIED');
  // A key restricted to Agent Platform: AI Studio says 403, Agent Platform answers.
  const restricted = async (url) => (url.includes('aiplatform') ? jsonResponse(200, cloudAnswer(SHEET)) : jsonResponse(403, { error: { code: 403, message: 'blocked' } }));
  assert.equal((await callGeminiAuto({ body: { model: 'm' }, key: 'k', fetchImpl: restricted })).sheet.pet_name, '肥肥');
});

test('callGeminiAuto: out of quota also tries Agent Platform once; a bad key or a region refusal never does', async () => {
  const { callGeminiAuto } = await import('../public/core.js');
  // An AI Studio key on Agent Platform: 401 there, so the quota error stands, with cloudStatus.
  const quota = async (url) => (url.includes('aiplatform') ? jsonResponse(401, { error: { code: 401, message: 'API keys are not supported by this API.' } }) : jsonResponse(429, { error: { code: 429, message: 'Quota exceeded per day (quota_exceeded)' } }));
  await assert.rejects(callGeminiAuto({ body: { model: 'm' }, key: 'k', fetchImpl: quota, backoff: [] }), (e) => e.kind === 'ratelimit' && e.cloudStatus === 401);
  // A key that works on both: Agent Platform answers.
  const both = async (url) => (url.includes('aiplatform') ? jsonResponse(200, cloudAnswer(SHEET)) : jsonResponse(503, { error: { code: 503, message: 'overloaded' } }));
  assert.equal((await callGeminiAuto({ body: { model: 'm' }, key: 'k', fetchImpl: both, backoff: [] })).sheet.pet_name, '肥肥');
  for (const [status, message, kind] of [[400, 'API key not valid.', 'http'], [400, 'User location is not supported for the API use.', 'region']]) {
    const urls = [];
    const fetchImpl = async (url) => { urls.push(url); return jsonResponse(status, { error: { code: status, message } }); };
    await assert.rejects(callGeminiAuto({ body: { model: 'm' }, key: 'k', fetchImpl, backoff: [] }), (e) => e.kind === kind);
    assert.ok(urls.every((u) => u === API_URL));
  }
});

test('the page lets Agent Platform and OAuth token exchange through its CSP, in the page and in the hosting headers', async () => {
  const fs = await import('node:fs');
  const html = fs.readFileSync(new URL('../public/index.html', import.meta.url), 'utf8');
  const hosting = JSON.parse(fs.readFileSync(new URL('../firebase.json', import.meta.url), 'utf8'));
  const header = hosting.hosting.headers[0].headers.find((h) => h.key === 'Content-Security-Policy').value;
  for (const csp of [html.match(/Content-Security-Policy" content="([^"]+)"/)[1], header]) {
    assert.match(csp, /connect-src https:\/\/generativelanguage\.googleapis\.com https:\/\/aiplatform\.googleapis\.com https:\/\/oauth2\.googleapis\.com;/);
  }
});

test('both languages have the route and 402 strings', () => {
  for (const s of Object.values(STRINGS)) {
    for (const k of ['route', 'routeAuto', 'routeCloud', 'routeHelp']) assert.equal(typeof s[k], 'string', k);
    assert.equal(typeof s.progress.viaCloud, 'string');
    assert.ok(s.errors.prepay(403).includes('403'));
    assert.ok(s.errors.prepay(403, 'Requests to this API are blocked.').includes('Requests to this API are blocked.'));
    assert.ok(!s.errors.prepay(undefined).includes('undefined'));
  }
});

test('Agent Platform refusing a key (401/403) is a cloudkey error with Google\'s words', async () => {
  for (const status of [401, 403]) {
    const fetchImpl = async () => jsonResponse(status, { error: { code: status, message: 'API keys are not supported by this API.' } });
    await assert.rejects(callGemini({ body: { model: 'm' }, key: 'k', route: 'cloud', fetchImpl }), (e) => e.kind === 'cloudkey' && e.httpStatus === status && /not supported/.test(e.message));
  }
  // AI Studio's own 401/403 stay what they were.
  const studio = async () => jsonResponse(403, { error: { code: 403, message: 'blocked' } });
  await assert.rejects(callGemini({ body: {}, key: 'k', fetchImpl: studio }), (e) => e.kind === 'http' && e.httpStatus === 403);
  for (const s of Object.values(STRINGS)) assert.ok(s.errors.cloudkey(401, 'Google words').includes('Google words'));
});

test('Service Account JSON: parsed, exchanged for OAuth2 Bearer token via RS256 JWT, and sent to project-scoped Agent Platform URL', async () => {
  const { generateKeyPairSync } = await import('node:crypto');
  const { parseServiceAccount, clearTokenCache, callGeminiAuto } = await import('../public/core.js');
  clearTokenCache();
  const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048, privateKeyEncoding: { type: 'pkcs8', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
  const saJson = JSON.stringify({
    type: 'service_account',
    project_id: 'demo-proj-123',
    client_email: 'bot@demo-proj-123.iam.gserviceaccount.com',
    private_key: privateKey,
  });
  assert.equal(parseServiceAccount('AIzaSyFakeKey'), null);
  assert.equal(parseServiceAccount('{not json'), null);
  const parsed = parseServiceAccount(saJson);
  assert.equal(parsed.project_id, 'demo-proj-123');
  assert.equal(parsed.client_email, 'bot@demo-proj-123.iam.gserviceaccount.com');

  const seen = [];
  const fetchImpl = async (url, init) => {
    seen.push({ url, init });
    if (url === 'https://oauth2.googleapis.com/token') {
      return jsonResponse(200, { access_token: 'ya29.test-bearer-token', expires_in: 3600 });
    }
    return jsonResponse(200, cloudAnswer(SHEET));
  };
  const routes = [];
  const out = await callGeminiAuto({
    body: buildSingleRequest({ model: 'gemini-3.8-flash', lang: 'zh', text: 'x' }),
    key: saJson,
    fetchImpl,
    onRoute: (r) => routes.push(r),
  });
  assert.equal(out.sheet.pet_name, '肥肥');
  assert.deepEqual(routes, ['cloud']);
  assert.equal(seen.length, 2);
  assert.equal(seen[0].url, 'https://oauth2.googleapis.com/token');
  assert.match(seen[0].init.body, /^grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer&assertion=/);
  assert.equal(seen[1].url, 'https://aiplatform.googleapis.com/v1/projects/demo-proj-123/locations/global/publishers/google/models/gemini-3.8-flash:generateContent');
  assert.equal(seen[1].init.headers.Authorization, 'Bearer ya29.test-bearer-token');
  assert.equal(seen[1].init.headers['x-goog-api-key'], undefined);

  // Second call reuses the cached Bearer token without hitting oauth2.googleapis.com again.
  await callGemini({
    body: buildSingleRequest({ model: 'gemini-3.8-flash', lang: 'zh', text: 'y' }),
    key: saJson,
    fetchImpl,
  });
  assert.equal(seen.length, 3);
  assert.ok(seen[2].url.includes('/projects/demo-proj-123/'));
});
