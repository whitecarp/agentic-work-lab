// The memory sky: each memory item is a round star (never a 4-point sparkle,
// which is close to Google's Gemini mark). The first 16 light up the Twins
// constellation (双子座) and a finished sheet gets its lines drawn.
// Positions are plain astronomy: approximate J2000 RA (hours), Dec (degrees).

const STARS = [
  ['Castor', 7.577, 31.89], ['Pollux', 7.755, 28.03], ['Alhena', 6.629, 16.4], ['Tejat', 6.383, 22.51],
  ['Mebsuta', 6.732, 25.13], ['Propus', 6.248, 22.51], ['Alzirr', 6.755, 12.9], ['Wasat', 7.335, 21.98],
  ['kappa', 7.741, 24.4], ['lambda', 7.302, 16.54], ['theta', 6.88, 33.96], ['Mekbuda', 7.068, 20.57],
  ['iota', 7.429, 27.8], ['tau', 7.186, 30.25], ['upsilon', 7.599, 26.9], ['nu', 6.483, 20.21],
];

const LINES = [
  ['Castor', 'tau'], ['tau', 'theta'], ['tau', 'Mebsuta'], ['Mebsuta', 'nu'], ['Mebsuta', 'Tejat'], ['Tejat', 'Propus'],
  ['Pollux', 'upsilon'], ['upsilon', 'kappa'], ['upsilon', 'Wasat'], ['Wasat', 'Mekbuda'], ['Mekbuda', 'Alhena'],
  ['Wasat', 'lambda'], ['lambda', 'Alzirr'], ['iota', 'upsilon'], ['iota', 'tau'],
];

export const VIEW = { w: 400, h: 300 };
const MAX_STARS = 320;

// North up and east to the left, as on a sky chart.
const MAIN = (() => {
  const cos = Math.cos((23 * Math.PI) / 180);
  const raw = STARS.map(([name, ra, dec]) => ({ name, x: -(ra - 7) * 15 * cos, y: -(dec - 23) }));
  const xs = raw.map((p) => p.x);
  const ys = raw.map((p) => p.y);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const pad = 30;
  const k = Math.min((VIEW.w - 2 * pad) / (x1 - x0), (VIEW.h - 2 * pad) / (y1 - y0));
  const ox = (VIEW.w - (x1 - x0) * k) / 2;
  const oy = (VIEW.h - (y1 - y0) * k) / 2;
  return raw.map((p) => ({ name: p.name, x: +(ox + (p.x - x0) * k).toFixed(1), y: +(oy + (p.y - y0) * k).toFixed(1) }));
})();

// Small seeded generator, so field stars keep their places between renders.
function seeded(seed) {
  let t = seed >>> 0;
  return () => {
    t = (t + 0x6d2b79f5) >>> 0;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
  };
}

export function layoutSky(count) {
  const n = Math.min(count, MAX_STARS);
  const stars = MAIN.map((p, i) => ({ x: p.x, y: p.y, main: true, lit: i < n, index: i < n ? i : -1 }));
  const next = seeded(7);
  for (let i = MAIN.length; i < n; i++) {
    stars.push({ x: +(8 + next() * (VIEW.w - 16)).toFixed(1), y: +(8 + next() * (VIEW.h - 16)).toFixed(1), main: false, lit: true, index: i });
  }
  const at = Object.fromEntries(MAIN.map((p, i) => [p.name, { ...p, lit: i < n }]));
  const lines = LINES.map(([a, b]) => ({ x1: at[a].x, y1: at[a].y, x2: at[b].x, y2: at[b].y, lit: at[a].lit && at[b].lit }));
  return { stars, lines };
}

const NS = 'http://www.w3.org/2000/svg';

function svgNode(tag, attrs = {}) {
  const node = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, String(v));
  return node;
}

// items: memory texts; each lit star shows its text on hover.
export function renderSky(svg, items, { connect = false } = {}) {
  const { stars, lines } = layoutSky(items.length);
  svg.setAttribute('viewBox', `0 0 ${VIEW.w} ${VIEW.h}`);
  const glow = svgNode('radialGradient', { id: 'star-glow' });
  glow.append(
    svgNode('stop', { offset: '0%', 'stop-color': '#fffaf0' }),
    svgNode('stop', { offset: '40%', 'stop-color': '#ffd6f0', 'stop-opacity': 0.9 }),
    svgNode('stop', { offset: '100%', 'stop-color': '#a77bff', 'stop-opacity': 0 }),
  );
  const defs = svgNode('defs');
  defs.append(glow);
  const lineGroup = svgNode('g', { class: 'sky-lines' });
  if (connect) {
    for (const l of lines) if (l.lit) lineGroup.append(svgNode('line', { x1: l.x1, y1: l.y1, x2: l.x2, y2: l.y2, pathLength: 1 }));
  }
  const starGroup = svgNode('g', { class: 'sky-stars' });
  for (const s of stars) {
    const c = s.lit
      ? svgNode('circle', { cx: s.x, cy: s.y, r: s.main ? 8 : 3.5, fill: 'url(#star-glow)', class: s.main ? 'lit main' : 'lit' })
      : svgNode('circle', { cx: s.x, cy: s.y, r: 1.8, class: 'dim' });
    if (s.lit && items[s.index]) {
      const title = svgNode('title');
      title.textContent = items[s.index];
      c.append(title);
    }
    starGroup.append(c);
  }
  svg.replaceChildren(defs, lineGroup, starGroup);
}
