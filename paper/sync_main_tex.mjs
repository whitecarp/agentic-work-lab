// Regenerates main.tex from manuscript_print.html (written by build_publication_pdf.mjs),
// so that the LaTeX source and manuscript.pdf carry the same text, tables, figures and citations.
//   node paper/build_publication_pdf.mjs   # writes manuscript_print.html, manuscript.pdf, figures/figN.pdf
//   node paper/sync_main_tex.mjs           # writes main.tex from the same HTML
// Equations, algorithms, invariants and Proposition 1 come from hand-written LaTeX in tex_blocks/.
// The script stops with an error if it meets a citation that has no entry in references.bib.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(here, 'manuscript_print.html'), 'utf8').replace(/\r\n/g, '\n');
const bib = fs.readFileSync(path.join(here, 'references.bib'), 'utf8');
const block = name => fs.readFileSync(path.join(here, 'tex_blocks', `${name}.tex`), 'utf8').replace(/\r\n/g, '\n').trimEnd();
const bibKeys = new Set([...bib.matchAll(/^@\w+\{([^,]+),/gm)].map(m => m[1]));

// ---------- citations: (first surname, year) -> BibTeX key ----------
const citeMap = {
  adams2016: 'Adams2016', amershi2019: 'Amershi2019', bainbridge1983: 'Bainbridge1983', bass2015: 'Bass2015', berente2021: 'Berente2021',
  bianchi2022: 'Bianchi2022', conforto2016: 'Conforto2016', cooper1990: 'Cooper1990', flyvbjerg2006: 'Flyvbjerg2006', 'garcia-molina1987': 'GarciaMolina1987',
  gray1992: 'gray1992transaction', gregor2013: 'Gregor2013', helland2012: 'Helland2012', hevner2004: 'Hevner2004', holzmann2022: 'Holzmann2022',
  hong2024: 'Hong2023MetaGPT', humble2010: 'Humble2010', kerzner2022: 'Kerzner2022', kleppmann2017: 'Kleppmann2017', leite2019: 'Leite2019',
  muller2024: 'Muller2024', parasuraman2000: 'Parasuraman2000', peffers2007: 'Peffers2007', pmi2021: 'PMI2021', qian2024: 'Qian2024ChatDev',
  raisch2021: 'Raisch2021', runeson2009: 'Runeson2009', saltzer1984: 'Saltzer1984', schneider1990: 'Schneider1990', sein2011: 'Sein2011',
  shahin2017: 'Shahin2017', shinn2023: 'Shinn2023Reflexion', shneiderman2020: 'Shneiderman2020', too2014: 'Too2014', venable2016: 'Venable2016',
  wang2024: 'Wang2024Agent', whyte2016: 'Whyte2016', wieringa2014: 'Wieringa2014', wieringa2015: 'Wieringa2015', wu2024: 'Wu2023AutoGen', yao2023: 'Yao2023ReAct',
  bakhshi2026: 'Bakhshi2026', dacre2026: 'Dacre2026', getiryaman2025: 'GetirYaman2025', khoee2026: 'Khoee2026', liu2025: 'Liu2025',
  liu2026: 'Liu2026', mcgrath2025: 'McGrath2025', papadonikolaki2026: 'Papadonikolaki2026', shin2026: 'Shin2026'
};
const cited = new Set();
const keyFor = (names, year) => {
  const first = names.replace(/^e\.g\.,\s*/, '').split(/\s+(?:et al\.|&)\s*|\s*&\s*/)[0].trim()
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/\s+/g, '');
  const k = citeMap[first + year];
  if (!k) throw new Error(`No reference for citation "${names}, ${year}"`);
  if (!bibKeys.has(k)) throw new Error(`Key ${k} missing from references.bib`);
  cited.add(k);
  return k;
};

// ---------- inline HTML -> LaTeX ----------
const ent = { amp: '&', lt: '\uE010', gt: '\uE011', nbsp: '\u00A0', quot: '"', ldquo: '\u201C', rdquo: '\u201D', lsquo: '\u2018', rsquo: '\u2019',
  mdash: '\u2014', ndash: '\u2013', minus: '\u2212', hellip: '\u2026', times: '\u00D7', le: '\u2264', ge: '\u2265', ne: '\u2260', asymp: '\u2248',
  rarr: '\u2192', larr: '\u2190', uarr: '\u2191', isin: '\u2208', cup: '\u222A', and: '\u2227', or: '\u2228', not: '\u00AC', empty: '\u2205',
  hArr: '\u21D4', infin: '\u221E', bull: '\u2022', Ropf: '\u211D', Sigma: '\u03A3', sigma: '\u03C3', tau: '\u03C4', Delta: '\u0394',
  uuml: '\u00FC', ouml: '\u00F6', aacute: '\u00E1' };
const decode = s => s.replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(+n)).replace(/&([A-Za-z]+);/g, (m, n) => (n in ent ? ent[n] : (() => { throw new Error(`Unknown entity ${m}`); })()));
const mathChar = { '\u00D7': '\\times', '\u2264': '\\le', '\u2265': '\\ge', '\u2260': '\\neq', '\u2248': '\\approx', '\u2192': '\\rightarrow', '\u2190': '\\leftarrow',
  '\u2191': '\\uparrow', '\u2208': '\\in', '\u222A': '\\cup', '\u2227': '\\land', '\u2228': '\\lor', '\u00AC': '\\neg', '\u2205': '\\varnothing', '\u21D4': '\\Leftrightarrow',
  '\u221E': '\\infty', '\u211D': '\\mathbb{R}', '\u03A3': '\\Sigma', '\u03C3': '\\sigma', '\u03C4': '\\tau', '\u0394': '\\Delta', '\u2212': '-', '\u220E': '\\blacksquare' };
const textChar = { '\u2014': '---', '\u2013': '--', '\u201C': '``', '\u201D': "''", '\u2018': '`', '\u2019': "'", '\u2026': '\\ldots{}', '\u00A0': '~',
  '\u2022': '\\textbullet{}', '\u00FC': '\\"{u}', '\u00F6': '\\"{o}', '\u00E1': "\\'{a}", '\uE010': '\\textless{}', '\uE011': '\\textgreater{}' };
const escText = s => s.replace(/[\\{}$&#%_~^]/g, c => ({ '\\': '\\textbackslash{}', '{': '\\{', '}': '\\}', '$': '\\$', '&': '\\&', '#': '\\#', '%': '\\%', '_': '\\_', '~': '\\textasciitilde{}', '^': '\\textasciicircum{}' }[c]))
  .replace(/[\u2014\u2013\u201C\u201D\u2018\u2019\u2026\u00A0\u2022\u00FC\u00F6\u00E1\uE010\uE011]/g, c => textChar[c])
  .replace(/[\u00D7\u2264\u2265\u2260\u2248\u2192\u2190\u2191\u2208\u222A\u2227\u2228\u00AC\u2205\u21D4\u221E\u211D\u03A3\u03C3\u03C4\u0394\u2212\u220E]/g, c => `$${mathChar[c]}$`);
const escCode = s => {
  let out = escText(s).replace(/--/g, '-{}-');
  // let short CLI flags such as --verify-only break at their inner hyphen inside narrow table cells
  if (s.length <= 14 && /^--[a-z]+(-[a-z]+)+$/.test(s)) out = out.replace(/([a-z])-([a-z])/g, '$1-\\allowbreak{}$2');
  // allow fine-grained line breaks in long identifiers and 64-character SHA-256 digests so nothing overflows the margin
  if (s.length > 14) out = out.replace(/(\\_|[\/.\-]|[0-9a-f]{4})/g, '$1\\allowbreak{}');
  return out;
};
const stash = [];
const keep = latex => `\uE100${stash.push(latex) - 1}\uE101`;
const subText = s => s.replace(/<em>([A-Za-z])<\/em>/g, '$1').replace(/<[^>]+>/g, '');
const mathSub = s => { const t = subText(decode(s)); return /^[A-Za-z]$/.test(t) ? t : `\\text{${t.replace(/_/g, '\\_')}}`; };
function inline(src, { tableCell = false } = {}) {
  let s = src;
  // Citations, before any other conversion (entities decoded first so names match).
  s = s.replace(/&amp;/g, '&').replace(/M&uuml;ller/g, 'Müller').replace(/H&ouml;st/g, 'Höst');
  s = s.replace(/\(((?:e\.g\.,\s*)?[A-Z][^()]*?,\s*\d{4}(?:;\s*[A-Z][^()]*?,\s*\d{4})*)\)/g, (m, inner) => {
    const items = inner.split(/;\s*/);
    if (!items.every(it => /^(e\.g\.,\s*)?[A-Z][A-Za-zÀ-ž'\- .&]*,\s*\d{4}$/.test(it))) return m;
    const eg = /^e\.g\.,\s*/.test(items[0]);
    const keys = items.map(it => { const [, names, year] = it.match(/^(.*),\s*(\d{4})$/); return keyFor(names, year); });
    return keep(eg ? `\\citep[e.g.,][]{${keys.join(', ')}}` : `\\citep{${keys.join(', ')}}`);
  });
  s = s.replace(/([A-Z][A-Za-zÀ-ž\-]+(?: et al\.| & [A-Z][A-Za-zÀ-ž\-]+)?)('s)? \((\d{4})\)/g, (m, names, poss, year) => {
    const k = keyFor(names, year);
    return keep(poss ? `\\citeauthor{${k}}'s \\citeyearpar{${k}}` : `\\citet{${k}}`);
  });
  s = s.replace(/&(?![A-Za-z#])/g, '&amp;'); // restore lone ampersands for the generic decoder
  // Math patterns.
  s = s.replace(/<em>R<sub>p<\/sub><\/em>/g, () => keep('$\\mathcal{R}_p$'))
    .replace(/<em>G<\/em><sub>plan<\/sub>/g, () => keep('$\\mathcal{G}_{\\text{plan}}$'))
    .replace(/<em>V<\/em><sub>sched<\/sub>/g, () => keep('$\\mathcal{V}_{\\text{sched}}$'))
    .replace(/<em>([SJD])<\/em>(?![a-z])/g, (m, c) => keep(`$\\mathcal{${c}}$`))
    .replace(/<em>([A-Za-z])<sub>(.*?)<\/sub><\/em>/g, (m, b, sub) => keep(`$${b}_{${mathSub(sub)}}$`))
    .replace(/<em>([A-Za-z])<\/em><sub>(.*?)<\/sub>/g, (m, b, sub) => keep(`$${b}_{${mathSub(sub)}}$`))
    .replace(/&(sigma|tau|Delta|Sigma);<sub>(.*?)<\/sub>/g, (m, g, sub) => keep(`$\\${g}_{${mathSub(sub)}}$`))
    .replace(/&Delta;<em>P<\/em>/g, () => keep('$\\Delta P$'))
    .replace(/\b(id|acct|wid|fname|done|auth|size|basename)<sub>(.*?)<\/sub>/g, (m, w, sub) => keep(`$\\text{${w}}_{${mathSub(sub)}}$`))
    .replace(/<em>([A-Za-z])<\/em>(?![A-Za-z])/g, (m, c) => keep(`$${c}$`))
    .replace(/&Ropf;<sup>\+<\/sup>/g, () => keep('$\\mathbb{R}^{+}$'));
  // Tags to markers, then escape text, then markers to LaTeX.
  s = s.replace(/<br\s*\/?>/g, '\uE001')
    .replace(/<strong>/g, '\uE002').replace(/<\/strong>/g, '\uE003')
    .replace(/<em>/g, '\uE004').replace(/<\/em>/g, '\uE005')
    .replace(/<sub>/g, '\uE006').replace(/<\/sub>/g, '\uE007')
    .replace(/<sup>/g, '\uE008').replace(/<\/sup>/g, '\uE009');
  s = s.replace(/<code>([\s\S]*?)<\/code>/g, (m, c) => keep(`\\texttt{${escCode(decode(c.replace(/<[^>]+>/g, '')))}}`));
  if (/<[a-zA-Z\/]/.test(s)) throw new Error(`Unhandled tag in: ${s.slice(0, 160)}`);
  s = escText(decode(s));
  s = s.replace(/\uE001/g, tableCell ? '\\newline ' : '\\\\ ')
    .replace(/\uE002/g, '\\textbf{').replace(/\uE003/g, '}')
    .replace(/\uE004/g, '\\emph{').replace(/\uE005/g, '}')
    .replace(/\uE006/g, '\\textsubscript{').replace(/\uE007/g, '}')
    .replace(/\uE008/g, '\\textsuperscript{').replace(/\uE009/g, '}');
  for (let pass = 0; pass < 3; pass++) s = s.replace(/\uE100(\d+)\uE101/g, (m, i) => stash[+i]);
  return s.replace(/[ \t]*\n[ \t]*/g, ' ').replace(/ {2,}/g, ' ').trim();
}

// ---------- block parser ----------
function nextElement(src, pos) {
  const re = /<([a-zA-Z0-9]+)((?:\s+[^>]*?)?)>/g;
  re.lastIndex = pos;
  const m = re.exec(src);
  if (!m) return null;
  const tag = m[1].toLowerCase();
  let depth = 1, i = re.lastIndex;
  const tagRe = new RegExp(`<(/?)${tag}(?:\\s[^>]*)?>`, 'gi');
  tagRe.lastIndex = i;
  let t;
  while ((t = tagRe.exec(src))) { depth += t[1] ? -1 : 1; if (depth === 0) break; }
  if (depth !== 0) throw new Error(`Unclosed <${tag}> at ${m.index}`);
  return { tag, attrs: m[2] || '', inner: src.slice(i, t.index), start: m.index, end: tagRe.lastIndex };
}
const attr = (attrs, name) => (attrs.match(new RegExp(`${name}="([^"]*)"`)) || [])[1] || '';
const children = (src) => { const out = []; let p = 0, e; while ((e = nextElement(src, p))) { out.push(e); p = e.end; } return out; };

// ---------- tables ----------
const textWidthPt = 451;
function table(el, n) {
  const caption = inline(el.inner.match(/<caption>([\s\S]*?)<\/caption>/)[1].replace(/^Table \d+:\s*/, ''));
  const head = el.inner.match(/<thead>([\s\S]*?)<\/thead>/)[1];
  const ths = [...head.matchAll(/<th([^>]*)>([\s\S]*?)<\/th>/g)].map(m => ({ num: /class="num"/.test(m[1]), w: +((m[1].match(/width:\s*([\d.]+)%/) || [])[1] || 10), text: m[2] }));
  const ncol = ths.length;
  const tabSepPt = ncol >= 7 ? 2.2 : 3;
  const factor = (1 - (2 * tabSepPt * ncol + 12) / textWidthPt) * (ncol >= 8 ? 0.96 : 0.985);
  const spec = ths.map(c => `>{${c.num ? '\\raggedleft' : '\\raggedright\\hspace{0pt}'}\\arraybackslash}p{${(c.w / 100 * factor).toFixed(3)}\\textwidth}`).join('');
  const rows = [...el.inner.match(/<tbody>([\s\S]*?)<\/tbody>/)[1].matchAll(/<tr([^>]*)>([\s\S]*?)<\/tr>/g)];
  const big = /allow-break/.test(el.attrs);
  const lines = [];
  lines.push('\\begin{table}[htbp]', '\\centering', `\\caption{${caption}}`, `\\label{tab:${n}}`, big ? '\\tiny' : '\\scriptsize', `\\setlength{\\tabcolsep}{${tabSepPt}pt}`, `\\begin{tabular}{${spec}}`, '\\toprule');
  lines.push(ths.map(c => `\\textbf{${inline(c.text, { tableCell: true })}}`).join(' & ') + ' \\\\', '\\midrule');
  for (const [, rattrs, rinner] of rows) {
    const cls = attr(rattrs, 'class');
    const tds = [...rinner.matchAll(/<td([^>]*)>([\s\S]*?)<\/td>/g)].map(m => ({ span: +(m[1].match(/colspan="(\d+)"/) || [0, 1])[1], text: m[2] }));
    if (cls === 'total-row') lines.push('\\midrule');
    const cells = tds.map(c => {
      let body = inline(c.text, { tableCell: true });
      if (cls === 'total-row') body = `\\textbf{${body}}`;
      if (c.span > 1) return cls === 'subhead' ? `\\multicolumn{${c.span}}{l}{\\textit{${body}}}` : `\\multicolumn{${c.span}}{>{\\raggedright\\arraybackslash}p{${(0.97 * factor).toFixed(3)}\\textwidth}}{${body}}`;
      return body;
    });
    if (cls === 'subhead' && lines.at(-1) !== '\\midrule') lines.push('\\addlinespace');
    lines.push(cells.join(' & ') + ' \\\\');
  }
  lines.push('\\bottomrule', '\\end{tabular}', '\\end{table}');
  return lines.join('\n');
}

// ---------- assemble ----------
const body = html.slice(html.indexOf('<body>') + 6, html.indexOf('</body>'));
let tableNo = 0, figNo = 0;
const out = [];
for (const el of children(body)) {
  const cls = attr(el.attrs, 'class');
  const tex = attr(el.attrs, 'data-tex');
  if (el.tag === 'h1' || cls === 'author-block') continue; // title and author are in the preamble below
  if (cls === 'abstract-box') {
    const p = el.inner.match(/<p[^>]*>([\s\S]*?)<\/p>/)[1];
    const parts = p.split(/<br\s*\/?>/).map(x => inline(x));
    const kw = inline(el.inner.match(/<div class="keywords">([\s\S]*?)<\/div>/)[1].replace(/<strong>Keywords:<\/strong>\s*/, ''));
    out.push('\\begin{abstract}', '\\noindent', parts.join(' \\\\\n'), '', '\\vspace{0.5em}', '\\noindent', `\\textbf{Keywords:} ${kw}`, '\\end{abstract}', '', '\\newpage');
    continue;
  }
  if (cls === 'highlights-box') {
    const items = [...el.inner.matchAll(/<li>([\s\S]*?)<\/li>/g)].map(m => `    \\item ${inline(m[1])}`);
    out.push('\\section*{Highlights}', '\\begin{itemize}[leftmargin=1.5em]', ...items, '\\end{itemize}', '', '\\newpage');
    continue;
  }
  if (el.tag === 'h2') {
    const title = el.inner.trim();
    if (title === 'References') continue;
    const m = title.match(/^(\d+)\.\s+(.*)$/);
    out.push('', m ? `\\section{${inline(m[2])}}\n\\label{sec:${m[1]}}` : `\\section*{${inline(title)}}`);
    continue;
  }
  if (el.tag === 'h3') {
    const m = el.inner.trim().match(/^(\d+)\.(\d+)\s+(.*)$/);
    if (!m) throw new Error('Unnumbered h3: ' + el.inner);
    out.push('', `\\subsection{${inline(m[3])}}\n\\label{sec:${m[1]}.${m[2]}}`);
    continue;
  }
  if (el.tag === 'p') { out.push('', inline(el.inner)); continue; }
  if (el.tag === 'ol' || el.tag === 'ul') {
    if (cls === 'ref-list') continue;
    const env = el.tag === 'ol' ? 'enumerate' : 'itemize';
    const items = children(el.inner).filter(c => c.tag === 'li').map(c => `    \\item ${inline(c.inner)}`);
    out.push('', `\\begin{${env}}[leftmargin=1.8em]`, ...items, `\\end{${env}}`);
    continue;
  }
  if (el.tag === 'table') { out.push('', table(el, ++tableNo)); continue; }
  if (cls === 'figure-box') {
    figNo++;
    const cap = inline(el.inner.match(/<div class="figure-caption">([\s\S]*?)<\/div>/)[1].replace(/^<strong>Figure \d+\.<\/strong>\s*/, ''));
    out.push('', '\\begin{figure}[htbp]', '\\centering', `\\includegraphics[width=\\textwidth]{figures/fig${figNo}.pdf}`, `\\caption{${cap}}`, `\\label{fig:${figNo}}`, '\\end{figure}');
    continue;
  }
  if (tex) { out.push('', block(tex)); continue; }
  throw new Error(`Unhandled block <${el.tag} class="${cls}">`);
}

const unused = [...bibKeys].filter(k => !cited.has(k));
if (unused.length) throw new Error('references.bib has uncited entries: ' + unused.join(', '));

const preamble = String.raw`\documentclass[11pt,a4paper]{article}

% Generated by paper/sync_main_tex.mjs from manuscript_print.html; edit build_publication_pdf.mjs and re-run both scripts,
% or edit this file and make the same change in build_publication_pdf.mjs.
\usepackage[utf8]{inputenc}
\usepackage[T1]{fontenc}
\usepackage{geometry}
\geometry{margin=1in}
\usepackage{amsmath,amssymb,amsfonts,amsthm}
\usepackage{booktabs}
\usepackage{array}
\usepackage{graphicx}
\usepackage{hyperref}
\usepackage{xcolor}
\usepackage{enumitem}
\usepackage{caption}
\usepackage{algorithm}
\usepackage[noend]{algpseudocode}
\usepackage{natbib}
\usepackage{setspace}
\onehalfspacing
\setlength{\emergencystretch}{2em}

\hypersetup{
    colorlinks=true,
    linkcolor=blue!70!black,
    citecolor=blue!70!black,
    urlcolor=blue!70!black
}

\theoremstyle{definition}
\newtheorem{proposition}{Proposition}
\newtheorem{invariant}{Governance Invariant}

\title{\textbf{${inline(body.match(/<h1[^>]*>([\s\S]*?)<\/h1>/)[1])}}}

\author{
  \textbf{HuaiCheng Sheng}$^{1,*}$ \\
  \small $^{1}$Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China \\
  \small $^{*}$Corresponding author. E-mail: \texttt{ZackSheng@gmail.com} \,|\, ORCID: \href{https://orcid.org/0009-0003-2900-6377}{\texttt{0009-0003-2900-6377}}
}

\date{November 2026}

\begin{document}

\maketitle
`;
const tex = preamble + '\n' + out.join('\n').replace(/\n{3,}/g, '\n\n') + '\n\n\\bibliographystyle{apalike}\n\\bibliography{references}\n\n\\end{document}\n';
fs.writeFileSync(path.join(here, 'main.tex'), tex);
console.log(`main.tex written: ${tableNo} tables, ${figNo} figures, ${cited.size} cited references (references.bib has ${bibKeys.size}).`);
