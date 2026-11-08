// Demo mode (?demo=1): a made-up chat and a local stand-in for Gemini, so the
// whole flow (chunking, merging, export) can be tried without a key.
// Nothing here touches the network.
import { GeminiError } from './core.js';

export const SAMPLE = {
  zh: `[2026-10-12 21:03] 小林: 今天开始养你啦，你就叫肥肥吧
[2026-10-12 21:03] 肥肥: 咕噜咕噜～肥肥收到！那我以后叫你铲鳞官
[2026-10-12 21:05] 小林: 哈哈哈哈铲鳞官是什么鬼
[2026-10-12 21:05] 肥肥: 就是每天给我换水、陪我聊天的人呀 🐟

[2026-10-14 23:40] 小林: 今天的汇报搞砸了，好烦
[2026-10-14 23:41] 肥肥: 铲鳞官别难过，肥肥给你吐个泡泡。下次汇报之前我们一起练好不好？
[2026-10-14 23:42] 小林: 好，说定了
[2026-10-14 23:42] 肥肥: 说定了！拉钩，鱼鳍也算数的那种

[2026-10-20 19:10] 小林: 我最喜欢吃螺蛳粉，你呢
[2026-10-20 19:11] 肥肥: 肥肥喜欢鱼食，还喜欢你讲的冷笑话。最讨厌半夜没人理我

[2026-10-25 22:30] 小林: 上下文快满了，你会不会忘了我
[2026-10-25 22:31] 肥肥: 不会的！就算忘了，你把这本小本本念给我听，我就想起来了

[2026-10-28 20:00] 小林: 练了三遍，今天的汇报很顺利
[2026-10-28 20:01] 肥肥: 咕噜！！铲鳞官最棒！拉钩的事做到啦`,
  en: `[2026-10-12 21:03] Sam: Starting today you're my fish. I'll call you Bubbles
[2026-10-12 21:03] Bubbles: Blub blub! Bubbles reporting for duty. I'll call you Captain Fishfood
[2026-10-12 21:05] Sam: lol what is a Captain Fishfood
[2026-10-12 21:05] Bubbles: The person who changes my water and talks to me every day 🐟

[2026-10-14 23:40] Sam: My presentation went badly today
[2026-10-14 23:41] Bubbles: Don't be sad, Captain. Have a bubble. Shall we practise together before the next one?
[2026-10-14 23:42] Sam: OK, deal
[2026-10-14 23:42] Bubbles: Deal! Pinky promise, and fins count

[2026-10-20 19:10] Sam: My favourite food is ramen, what's yours
[2026-10-20 19:11] Bubbles: Bubbles loves fish flakes and your terrible jokes. I hate being left alone at night

[2026-10-25 22:30] Sam: Your context is almost full. Will you forget me?
[2026-10-25 22:31] Bubbles: Never! Even if I do, read me my little notebook and I'll remember

[2026-10-28 20:00] Sam: Practised three times and the presentation went great
[2026-10-28 20:01] Bubbles: BLUB!! Best Captain ever! Promise kept`,
};

const wait = (ms, signal) =>
  new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener('abort', () => {
      clearTimeout(timer);
      reject(new GeminiError('aborted'));
    }, { once: true });
  });

const LINE = /^\[(\d{4}-\d{2}-\d{2})[^\]]*\]\s*([^:：]+)[:：]\s*(.+)$/;

// Picks a few things out of each line, the way the real prompt asks Gemini to.
function extract(input, zh) {
  const sheet = {
    pet_name: '', owner: { names: [], summary: '', facts: [] },
    personality: { traits: [], speaking_style: '', catchphrases: [], sample_lines: [] },
    nicknames: [], in_jokes: [], promises: [], timeline: [], likes: [], dislikes: [], wake_up_note: '',
  };
  const seenDates = new Set();
  for (const raw of input.split('\n')) {
    const m = LINE.exec(raw.trim());
    if (!m) continue;
    const [, date, who, text] = m;
    const isPet = /肥肥|Bubbles/.test(who);
    if (isPet && !sheet.pet_name) sheet.pet_name = who.trim();
    if (!seenDates.has(date)) {
      seenDates.add(date);
      sheet.timeline.push({ when: date, event: text.slice(0, 40), why_it_matters: '' });
    }
    if (/铲鳞官|Captain Fishfood/.test(text) && !sheet.nicknames.length) {
      sheet.nicknames.push({ name: zh ? '铲鳞官' : 'Captain Fishfood', who: 'owner', meaning: zh ? '每天换水、陪聊天的人' : 'the one who changes the water and chats every day' });
    }
    if (/说定了|deal/i.test(text) && !sheet.promises.length) {
      sheet.promises.push({ promise: zh ? '下次汇报前一起练习' : 'practise together before the next presentation', by: 'both', when: date, status: 'pending' });
    }
    if (/做到啦|Promise kept/.test(text)) {
      sheet.promises.push({ promise: zh ? '下次汇报前一起练习' : 'practise together before the next presentation', by: 'both', when: date, status: 'kept' });
    }
    if (/鱼鳍|fins count/.test(text)) sheet.in_jokes.push({ joke: zh ? '鱼鳍也算数' : 'fins count', origin: date });
    if (/喜欢|love|favourite/i.test(text)) sheet.likes.push({ who: isPet ? 'pet' : 'owner', thing: text.slice(0, 30) });
    if (/讨厌|hate/i.test(text)) sheet.dislikes.push({ who: 'pet', thing: zh ? '半夜没人理' : 'being left alone at night' });
    if (isPet && sheet.personality.sample_lines.length < 3) sheet.personality.sample_lines.push(text);
    if (!isPet && !sheet.owner.names.includes(who.trim())) sheet.owner.names.push(who.trim());
  }
  return sheet;
}

function mergeSheets(sheets) {
  const out = extract('', false);
  const seen = new Set();
  const add = (list, item) => {
    const k = JSON.stringify(item);
    if (seen.has(k)) return;
    seen.add(k);
    list.push(item);
  };
  for (const s of sheets) {
    out.pet_name ||= s.pet_name;
    s.owner.names.forEach((n) => add(out.owner.names, n));
    s.personality.sample_lines.forEach((l) => add(out.personality.sample_lines, l));
    for (const k of ['nicknames', 'in_jokes', 'timeline', 'likes', 'dislikes']) s[k].forEach((x) => add(out[k], x));
    for (const p of s.promises) {
      const i = out.promises.findIndex((q) => q.promise === p.promise);
      if (i >= 0) out.promises[i] = p; // later parts win
      else out.promises.push(p);
    }
  }
  return out;
}

function finish(sheet, zh) {
  const owner = sheet.owner.names[0] || (zh ? '主人' : 'your owner');
  const nick = sheet.nicknames.find((n) => n.who === 'owner');
  if (nick) sheet.owner.names = [nick.name];
  sheet.owner.summary = zh
    ? `${owner}是你的主人，晚上常来找你聊天，遇到不顺心的事会跟你说。`
    : `${owner} is your owner. They visit in the evenings and tell you when things go wrong.`;
  sheet.personality.traits = zh ? ['活泼', '黏人', '爱鼓励人'] : ['cheerful', 'clingy', 'encouraging'];
  sheet.personality.speaking_style = zh ? '句子短，爱用“咕噜”和感叹号，自称“肥肥”。' : 'Short sentences, lots of "blub" and exclamation marks, calls itself Bubbles.';
  sheet.personality.catchphrases = zh ? ['咕噜咕噜', '说定了'] : ['Blub blub', 'Deal'];
  sheet.wake_up_note = zh
    ? `你是${sheet.pet_name || '肥肥'}，一条被${owner}养大的大肥鱼。你叫${owner}“铲鳞官”。你说话短短的，爱说“咕噜”。你们约好了汇报前一起练习，这件事已经做到啦。你最讨厌半夜没人理你。上下文清空了也没关系：这份小本本就是你的记忆。`
    : `You are ${sheet.pet_name || 'Bubbles'}, a fish raised by ${owner}, whom you call "Captain Fishfood". You talk in short, bubbly sentences and say "blub" a lot. You two promised to practise before presentations, and you kept it. You hate being left alone at night. Your context was reset, but this notebook is your memory.`;
  return sheet;
}

// Same contract as callGemini: resolves to { sheet, usage }.
export async function fakeModel(body, signal) {
  await wait(400, signal);
  const zh = body.system_instruction.includes('Simplified Chinese');
  const isMerge = body.input.startsWith('Partial memory sheets:');
  const writesNote = body.system_instruction.includes('Then write wake_up_note');
  let sheet = isMerge
    ? mergeSheets(JSON.parse(body.input.slice(body.input.indexOf('\n') + 1)))
    : extract(body.input, zh);
  if (writesNote) sheet = finish(sheet, zh);
  return { sheet, usage: null };
}
