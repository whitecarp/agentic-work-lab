// Page wiring. The key is read from the input when a run starts and lives
// only in this page; nothing here writes to storage, cookies or any server
// other than Google's Gemini API (the CSP enforces that too).
import { STRINGS, LINKS, CREDIT_URL, pickLang } from './i18n.js';
import {
  MODELS, CHUNK_CHOICES, DEFAULT_CHUNK_TOKENS, GeminiError,
  estimateTokens, splitIntoChunks, textFromFile, combineSources,
  runPipeline, callGeminiAuto, memoryItems, sheetToMarkdown, sheetToJSON,
  parseServiceAccount,
} from './core.js';
import { renderSky } from './sky.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
const DEMO = params.has('demo');
const DEMO_CHUNK = 300;

let lang = pickLang(params.get('lang'), navigator.languages || [navigator.language]);
let files = []; // { name, size, text }
let loadedSaJson = null; // canonical Service Account JSON string when loaded from a .json file
let result = null; // { sheet, meta }
let running = null; // AbortController while a run is going
let partialItems = []; // memory stars gathered so far in this run
let partsCache = { sig: null, parts: [] }; // parts a failed run finished, so a retry skips them
let outLangTouched = false;
let demo = null;

const t = () => STRINGS[lang];

function el(tag, cls, text) {
  const node = document.createElement(tag);
  if (cls) node.className = cls;
  if (text != null) node.textContent = text;
  return node;
}

function link(href, text) {
  const a = el('a', null, text);
  a.href = href;
  a.target = '_blank';
  a.rel = 'noopener noreferrer';
  return a;
}

function fillList(ul, items) {
  ul.replaceChildren(...items.map((i) => el('li', null, i)));
}

// Text with `code` spans, built as DOM nodes (never innerHTML).
function richText(parent, text) {
  for (const part of String(text).split(/(`[^`]+`)/g)) {
    if (!part) continue;
    const code = part.length > 2 && part.startsWith('`') && part.endsWith('`');
    parent.appendChild(code ? el('code', null, part.slice(1, -1)) : document.createTextNode(part));
  }
  return parent;
}

// Step 1 carries the one-line install in a box with a copy button.
function fillAutoSteps(ol, items, s) {
  if (!ol || !items) return;
  ol.replaceChildren(...items.map((text, i) => {
    const li = richText(el('li'), text);
    if (i === 0 && s?.installLine) {
      const box = el('div', 'install-box');
      const code = el('code', null, s.installLine); code.id = 'install-line';
      const copy = el('button', 'ghost', s.copy); copy.id = 'copy-install'; copy.type = 'button';
      box.append(code, copy);
      li.append(box);
    }
    return li;
  }));
}
async function copyInstall() {
  const code = $('install-line'), button = $('copy-install');
  if (!code || !button) return;
  try {
    await navigator.clipboard.writeText(code.textContent);
  } catch {
    getSelection().selectAllChildren(code);
    document.execCommand('copy');
  }
  button.textContent = t().copied;
  setTimeout(() => { if ($('copy-install')) $('copy-install').textContent = t().copy; }, 1500);
}

// Feature tiles: a short title, then the details.
function fillAutoFeatures(ul, items) {
  if (!ul || !items) return;
  ul.replaceChildren(...items.map(({ title, body }) => {
    const li = el('li');
    li.append(el('h3', null, title), richText(el('p'), body));
    return li;
  }));
}

// ---------- art slots ----------

// Shows the gradient behind a slot if its image is missing. Inline onerror
// would be blocked by the CSP, and module scripts run after images may fail.
function initArt() {
  for (const img of document.querySelectorAll('img[data-art]')) {
    const missing = () => img.closest('.art')?.classList.add('missing');
    if (img.complete && !img.naturalWidth) missing();
    img.addEventListener('error', missing);
  }
}

// ---------- language ----------

function fillModelOptions() {
  const s = t();
  const current = $('model').value || MODELS[0].id;
  $('model').replaceChildren(...MODELS.map((m, i) => {
    const notes = [m.free ? s.modelFree : s.modelPaid, i === 0 && s.modelDefault].filter(Boolean);
    const opt = el('option', null, `${m.id}${s.md.lp}${notes.join(s.md.sep)}${s.md.rp}`);
    opt.value = m.id;
    opt.selected = m.id === current;
    return opt;
  }));
}

// AI Studio first (free tier), or straight to Google Cloud's Agent Platform (Cloud billing).
function fillRouteOptions() {
  const s = t();
  const current = $('route').value || 'auto';
  $('route').replaceChildren(...[['auto', s.routeAuto], ['cloud', s.routeCloud]].map(([value, label]) => {
    const opt = el('option', null, label);
    opt.value = value;
    opt.selected = value === current;
    return opt;
  }));
}

function fillChunkOptions() {
  const s = t();
  const current = Number($('chunk').value) || (DEMO ? DEMO_CHUNK : DEFAULT_CHUNK_TOKENS);
  const choices = DEMO ? [DEMO_CHUNK, ...CHUNK_CHOICES] : CHUNK_CHOICES;
  $('chunk').replaceChildren(...choices.map((n) => {
    const opt = el('option', null, n === DEMO_CHUNK && DEMO ? s.chunkDemo : s.tokens(n));
    opt.value = String(n);
    opt.selected = n === current;
    return opt;
  }));
}

function applyLang() {
  const s = t();
  document.documentElement.lang = s.htmlLang;
  document.title = s.appName;
  document.querySelectorAll('[data-t]').forEach((node) => {
    const v = s[node.dataset.t];
    if (typeof v === 'string') node.textContent = v;
  });
  document.querySelectorAll('[data-alt]').forEach((img) => (img.alt = s[img.dataset.alt] || ''));
  $('lang').textContent = s.langSwitch;
  $('paste').placeholder = s.pastePlaceholder;
  fillList($('privacy-list'), s.privacy);
  fillList($('rules'), s.rules);
  fillAutoSteps($('auto-steps'), s.autoSteps, s);
  fillAutoFeatures($('auto-extras'), s.autoExtras);
  $('auto-demo').setAttribute('aria-label', s.autoArtAlt);
  fillModelOptions();
  fillRouteOptions();
  fillChunkOptions();
  if (!outLangTouched) $('out-lang').value = lang;
  const [before, handle, after] = s.credit;
  $('credit').replaceChildren(before, link(CREDIT_URL, handle), after);
  renderFiles();
  updateStats();
  updateKeyBadge();
  if (running) drawSky(partialItems, false);
  else if (result) renderResult();
}

// ---------- key & JSON token ----------

function activeKey() {
  return loadedSaJson || $('key').value.trim();
}

function updateKeyBadge() {
  const badge = $('key-badge');
  if (!badge) return;
  const raw = activeKey();
  if (!raw) {
    badge.hidden = true;
    badge.className = 'badge key-badge';
    return;
  }
  const sa = parseServiceAccount(raw);
  badge.hidden = false;
  if (sa) {
    badge.textContent = t().keyBadgeJson(sa.project_id);
    badge.className = 'badge key-badge is-cloud';
  } else {
    badge.textContent = t().keyBadgeStudio;
    badge.className = 'badge key-badge';
  }
}

async function importKeyJsonFile(file) {
  if (!file) return;
  try {
    const raw = await file.text();
    const sa = parseServiceAccount(raw);
    if (!sa) return setStatus(t().errors.badkeyjson, 'error');
    loadedSaJson = JSON.stringify(sa);
    $('key').value = `${sa.client_email} (${sa.project_id})`;
    $('route').value = 'cloud';
    updateKeyBadge();
    setStatus('', '');
  } catch {
    setStatus(t().errors.badkeyjson, 'error');
  }
}

// ---------- input ----------

function currentText() {
  return combineSources($('paste').value, files);
}

let statsTimer = 0;
function updateStats() {
  clearTimeout(statsTimer);
  statsTimer = setTimeout(() => {
    const text = currentText();
    $('empty-art').hidden = Boolean(text.trim());
    if (!text.trim()) return ($('stats').textContent = '');
    const chunks = splitIntoChunks(text, Number($('chunk').value)).length;
    $('stats').textContent = t().stats(text.length, estimateTokens(text), chunks);
  }, 200);
}

function renderFiles() {
  $('file-list').replaceChildren(...files.map((f, i) => {
    const li = el('li');
    li.append(el('span', null, `${f.name} · ${(f.size / 1024).toFixed(1)} KB`));
    const rm = el('button', 'ghost small', t().removeFile);
    rm.type = 'button';
    rm.addEventListener('click', () => {
      files.splice(i, 1);
      renderFiles();
      updateStats();
    });
    li.append(rm);
    return li;
  }));
}

async function addFiles(list) {
  for (const file of list) {
    if (!/\.(txt|md|markdown|json|log)$/i.test(file.name)) {
      setStatus(t().fileRead(file.name), 'error');
      continue;
    }
    try {
      const raw = await file.text();
      // Safety: if a user drops a Service Account JSON key into the chat drop zone, load it
      // into the key slot instead of sending a private key as chat text.
      if (/\.json$/i.test(file.name) && parseServiceAccount(raw)) {
        await importKeyJsonFile(file);
        continue;
      }
      files.push({ name: file.name, size: file.size, text: textFromFile(file.name, raw) });
    } catch {
      setStatus(t().fileRead(file.name), 'error');
    }
  }
  renderFiles();
  updateStats();
}

// ---------- running ----------

function setStatus(text, kind = '', more = null) {
  $('status').className = kind;
  $('status').replaceChildren(text);
  if (more) $('status').append(' ', more);
}

function progressText(p) {
  const pr = t().progress;
  if (p.phase === 'single') return pr.single;
  if (p.phase === 'merge') return pr.merge(p.n);
  return pr[p.phase](p.i, p.n);
}

function showError(e) {
  const s = t();
  const errs = s.errors;
  if (e instanceof GeminiError && e.kind === 'region') return setStatus(errs.region, 'error', link(LINKS.REGIONS, s.regionsLink));
  if (e instanceof GeminiError && e.kind === 'prepay') {
    const more = el('span');
    more.append(link(LINKS.BILLING, errs.billingLink), ' · ', link(LINKS.CLOUD_KEYS, errs.cloudKeysLink));
    return setStatus(errs.prepay(e.cloudStatus, e.cloudMessage), 'error', more);
  }
  if (e instanceof GeminiError && e.kind === 'cloudkey') return setStatus(errs.cloudkey(e.httpStatus, e.message), 'error', link(LINKS.CLOUD_KEYS, errs.cloudKeysLink));
  if (e instanceof GeminiError && e.kind === 'busy') {
    const alt = MODELS.find((m) => m.free && m.id !== $('model').value) || MODELS[0];
    return setStatus(errs.busy(alt.id), 'error');
  }
  let text;
  if (!(e instanceof GeminiError)) text = errs.http('?', e.message);
  else if (e.kind === 'http') text = errs.http(e.httpStatus, e.message);
  else text = errs[e.kind] || errs.http(e.httpStatus || '?', e.message);
  setStatus(text, 'error');
}

function toggleRunning(on) {
  $('run').disabled = on;
  $('stop').hidden = !on;
}

function drawSky(items, done) {
  renderSky($('sky'), items, { connect: done });
  $('sky-label').textContent = done ? t().sky.done(items.length) : t().sky.gathering(items.length);
}

async function run() {
  const s = t();
  const text = currentText();
  if (!text.trim()) return setStatus(s.errors.nothing, 'error');
  const key = activeKey();
  if (!DEMO && !key) return setStatus(s.errors.nokey, 'error');
  if (!$('adult').checked) return setStatus(s.errors.adult, 'error');

  const model = $('model').value;
  const outLang = $('out-lang').value;
  running = new AbortController();
  const { signal } = running;
  toggleRunning(true);
  partialItems = [];
  $('result').hidden = false;
  $('result').dataset.state = 'loading';
  drawSky(partialItems, false);
  document.querySelector('.actions').scrollIntoView({ behavior: 'smooth', block: 'start' });

  // Auto: AI Studio first; once Agent Platform answers for this key, the rest of the run goes there.
  let route = $('route').value === 'cloud' ? 'cloud' : 'auto';
  const callModel = DEMO
    ? (body) => demo.fakeModel(body, signal)
    : (body) => callGeminiAuto({
      body, key, signal, route,
      onRoute: (r) => { route = r; },
      onWait: (sec, why) => setStatus(why === 'busy' ? t().progress.busy(sec) : t().progress.wait(sec)),
    });
  try {
    const chunkTokens = Number($('chunk').value);
    const petName = $('pet-name').value.trim();
    const ownerName = $('owner-name').value.trim();
    // Same input and settings as a failed run: reuse its finished parts.
    const sig = [chunkTokens, outLang, petName, ownerName, text].join('\u0000');
    if (partsCache.sig !== sig) partsCache = { sig, parts: [] };
    const out = await runPipeline({
      text, chunkTokens, model, lang: outLang, petName, ownerName, callModel,
      cached: partsCache.parts,
      onProgress: (p) => setStatus(progressText(p)),
      onPartial: (sheet, i) => {
        partsCache.parts[i] = sheet;
        partialItems = partialItems.concat(memoryItems(sheet));
        drawSky(partialItems, false);
      },
    });
    partsCache = { sig: null, parts: [] };
    result = {
      sheet: out.sheet,
      meta: { model: DEMO ? `${model} (demo)` : model, created: new Date().toISOString(), chunks: out.chunks, language: outLang },
    };
    renderResult();
    setStatus(t().progress.done(out.usage) + (!DEMO && route === 'cloud' ? ` ${t().progress.viaCloud}` : ''), 'ok');
  } catch (e) {
    showError(e);
    if (result) renderResult();
    else $('result').hidden = true;
  } finally {
    running = null;
    toggleRunning(false);
  }
}

// ---------- result ----------

function itemList(items) {
  const ul = el('ul');
  for (const [strong, rest] of items) {
    const li = el('li');
    if (strong) li.append(el('strong', null, strong));
    if (rest) li.append(document.createTextNode(strong ? ` ${rest}` : rest));
    ul.append(li);
  }
  return ul;
}

function renderResult() {
  const s = t();
  const m = s.md;
  const sh = result.sheet;
  const who = (w) => m.who[w] || w;
  $('result').hidden = false;
  $('result').dataset.state = 'done';
  drawSky(memoryItems(sh), true);
  $('r-title').textContent = m.title(sh.pet_name);
  $('wake-text').textContent = sh.wake_up_note || s.empty;

  const root = $('sheet');
  root.replaceChildren();
  const section = (title, nodes) => {
    const sec = el('section', 'sheet-sec');
    sec.append(el('h3', null, title));
    nodes = nodes.filter(Boolean);
    sec.append(...(nodes.length ? nodes : [el('p', 'muted', s.empty)]));
    root.append(sec);
  };
  const p = sh.personality;
  section(m.owner, [
    sh.owner.names.length && el('p', null, `${m.calledBy} ${sh.owner.names.join(m.sep)}`),
    sh.owner.summary && el('p', null, sh.owner.summary),
    sh.owner.facts.length && itemList(sh.owner.facts.map((f) => ['', f])),
  ]);
  section(m.personality, [
    p.traits.length && el('p', null, `${m.traits} ${p.traits.join(m.sep)}`),
    p.speaking_style && el('p', null, `${m.style} ${p.speaking_style}`),
    p.catchphrases.length && el('p', null, `${m.catchphrases} ${p.catchphrases.map(m.quote).join(' ')}`),
    ...p.sample_lines.map((l) => el('blockquote', null, l)),
  ]);
  section(m.nicknames, [sh.nicknames.length && itemList(sh.nicknames.map((n) =>
    [n.name, `${m.lp}${who(n.who)}${m.rp}${n.meaning ? `${m.colon}${n.meaning}` : ''}`]))]);
  section(m.jokes, [sh.in_jokes.length && itemList(sh.in_jokes.map((j) => [j.joke, j.origin && `— ${j.origin}`]))]);
  section(m.promises, [sh.promises.length && itemList(sh.promises.map((x) =>
    [`[${m.status[x.status] || x.status}]`, `${x.promise}${m.lp}${[who(x.by), x.when].filter(Boolean).join(m.sep)}${m.rp}`]))]);
  section(m.timeline, [sh.timeline.length && itemList(sh.timeline.map((e) =>
    [e.when || '?', `${e.event}${e.why_it_matters ? ` — ${e.why_it_matters}` : ''}`]))]);
  section(m.likes, [sh.likes.length && itemList(sh.likes.map((l) => [`${who(l.who)}${m.colon}`, l.thing]))]);
  section(m.dislikes, [sh.dislikes.length && itemList(sh.dislikes.map((l) => [`${who(l.who)}${m.colon}`, l.thing]))]);
}

function fileBase() {
  const name = (result.sheet.pet_name || 'pet').replace(/[\\/:*?"<>|\s]+/g, '-').slice(0, 40);
  return `memory-${name}-${result.meta.created.slice(0, 10)}`;
}

function download(name, text, type) {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const a = el('a');
  a.href = url;
  a.download = name;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}

async function copyWake() {
  const text = result?.sheet.wake_up_note;
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    getSelection().selectAllChildren($('wake-text'));
    document.execCommand('copy');
  }
  $('copy-wake').textContent = t().copied;
  setTimeout(() => ($('copy-wake').textContent = t().copy), 1500);
}

// ---------- start ----------

async function init() {
  initArt();
  $('terms-link').href = LINKS.TERMS;
  $('regions-link').href = LINKS.REGIONS;
  $('key-link').href = LINKS.KEY_PAGE;
  if ($('cloud-key-link')) $('cloud-key-link').href = LINKS.CLOUD_KEYS;
  if (DEMO) {
    demo = await import('./demo.js');
    $('demo-banner').hidden = false;
    $('sample').hidden = false;
    $('key').disabled = true;
    if ($('key-file')) $('key-file').disabled = true;
    $('settings').open = true;
  }
  applyLang();

  $('lang').addEventListener('click', () => {
    lang = lang === 'zh' ? 'en' : 'zh';
    const url = new URL(location.href);
    url.searchParams.set('lang', lang);
    history.replaceState(null, '', url);
    applyLang();
  });
  $('paste').addEventListener('input', updateStats);
  $('chunk').addEventListener('change', updateStats);
  $('out-lang').addEventListener('change', () => (outLangTouched = true));
  $('files').addEventListener('change', (e) => {
    addFiles([...e.target.files]);
    e.target.value = '';
  });
  const drop = $('drop');
  drop.addEventListener('dragover', (e) => {
    e.preventDefault();
    drop.classList.add('over');
  });
  drop.addEventListener('dragleave', () => drop.classList.remove('over'));
  drop.addEventListener('drop', (e) => {
    e.preventDefault();
    drop.classList.remove('over');
    addFiles([...e.dataTransfer.files]);
  });
  $('sample').addEventListener('click', () => {
    $('paste').value = demo.SAMPLE[lang];
    updateStats();
  });
  $('key').addEventListener('input', () => {
    loadedSaJson = null;
    if (parseServiceAccount($('key').value.trim())) $('route').value = 'cloud';
    updateKeyBadge();
  });
  if ($('key-file')) {
    $('key-file').addEventListener('change', async (e) => {
      await importKeyJsonFile(e.target.files?.[0]);
      e.target.value = '';
    });
  }
  $('clear-key').addEventListener('click', () => {
    loadedSaJson = null;
    $('key').value = '';
    updateKeyBadge();
  });
  $('run').addEventListener('click', run);
  $('stop').addEventListener('click', () => running?.abort());
  $('copy-wake').addEventListener('click', copyWake);
  // The install box is rebuilt on every language switch, so listen on its list.
  $('auto-steps').addEventListener('click', (e) => { if (e.target.closest('#copy-install')) copyInstall(); });
  $('dl-md').addEventListener('click', () =>
    download(`${fileBase()}.md`, sheetToMarkdown(result.sheet, result.meta, STRINGS[result.meta.language].md), 'text/markdown;charset=utf-8'));
  $('dl-json').addEventListener('click', () =>
    download(`${fileBase()}.json`, sheetToJSON(result.sheet, result.meta), 'application/json;charset=utf-8'));
}

init();
