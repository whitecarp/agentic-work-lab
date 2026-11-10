// Reads the paper's inputs from fixed commits, so every paper script gives the same output
// whatever branch or commit is checked out, on any operating system.
//
// Agentic Work Lab data: the 3 November snapshot, commit e3205d6 (tag paper-data-snapshot-e3205d6).
//   That commit is not in main's history, so 8add4b5 is accepted as well: it is on main and holds
//   byte-identical copies of every input. A copy with no Git history (a ZIP download or an archive
//   deposit) may read the twelve table inputs from disk, and only if each one matches the SHA-256
//   list below.
// PublishOps CrossCast code: the evaluated commit 75b7abc and the v0.16.0 release commit a46bd19,
//   read from a clone of that repository. They are never read from its working tree.
//
// Text is returned with LF line endings, so Windows and other systems agree.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

export const PINS = Object.freeze({
  lab: Object.freeze(['e3205d60da976fd2f6b172a210bf8e44712ba5dc', '8add4b58e4bc9c28c9210af0d10264c83ce0be22']),
  labTag: 'paper-data-snapshot-e3205d6',
  crosscastEvaluated: '75b7abcdd97de3e6a0b11df70742ea275d2422f2',
  crosscastRelease: 'a46bd19372ea3c0c7c4672642d4d56db1b1f42c7',
});

// SHA-256 of each table input at the snapshot, computed on its text with LF line endings.
export const INPUT_SHA256 = Object.freeze({
  'data/operations-events.json': '4488de21835cfbecf8cc550d9eb2b5191e78da6cd98166dd01c51bb674390bbe',
  'data/scheduled-run-evidence.json': '6f5e39607b2146c71a1472100b3cd76c0424d03c6796a7ec15a42e7d1d6695d4',
  'data/production-update-2026-11-03.json': 'd3142d4ef3ae52bb15675642c6d53ca8458244a6e9cb02d5f0c4933544115fd8',
  'media/manifest.json': '026f705bcc9958203e492ae8fbf901a294e0a48f9e2dbb07eb3c32bdf2e7e0a1',
  'media/SHA256SUMS.txt': '74d98585f4e830e0ca18402333190513da3c77d9f2161d7eb3c16581c8cf4d6a',
  'data/creator-video-snapshot.json': '740d1c8faffbf36f679f9f181dc52e5f8dbbd53775ed2580ff83f23a3b25624d',
  'data/channel-weekly.json': 'b6f4cf388049e1bac4fbc8134096b0894f7fa42f7e3fba6c63ae67c6d1d3c6ea',
  'data/snapshots/2026-10-28-afternoon/creator-video-snapshot.json': '50e3d188f0f2774dfa492662c9604565c2fbc2b57835524cf1f6aa22874dfc6b',
  'data/episode03-24h-2026-10-28.json': '6d82afe45851b3290cc5dcf35b43f8f1b0435e023790f557745a4db120dbfd37',
  'data/whale-log02-24h-2026-10-29.json': 'c7f8bac77a07a36371f8cf457fe8600951e3be4a91387974488bbb5586120bfc',
  'results/validation.json': '8d1826250ff92cbb854e73c7c42c9513341eb94116dbe1d841d96ec4b61d159b',
  'tests/workflows.test.mjs': 'c031956fe63af28a3ff1310b288ba7067ac0396f07a0ce1223f96c68075883da',
});

// The numbers the manuscript and the supplement print in fixed text. A builder stops if its data differ.
export const SNAPSHOT_FACTS = Object.freeze({
  manifestItems: 30, manifestBytes: 2211716129, manifestReleaseTags: 7,
  journalRecords: 69, channelSnapshotRecords: 55,
  crosscastSourceFiles: 123, crosscastSourceLines: 26972, crosscastTestFiles: 126, crosscastTestFunctions: 2737,
});

export class PinError extends Error {}

const toLf = text => text.replace(/\r\n/g, '\n');
const sha256 = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');

function git(dir, args) {
  return execFileSync('git', ['-C', dir, ...args], { maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
}
function hasCommit(dir, id) {
  try { git(dir, ['cat-file', '-e', `${id}^{commit}`]); return true; } catch { return false; }
}
// True only when dir is itself the top folder of a Git checkout. A plain copy that merely sits inside
// some other repository must not be read through that repository's history.
function sameFolder(a, b) {
  const real = p => {
    let full = path.resolve(p);
    try { full = fs.realpathSync.native(full); } catch { /* keep the resolved path */ }
    return process.platform === 'win32' ? full.toLowerCase() : full;
  };
  return real(a) === real(b);
}
function isGitCheckout(dir) {
  try { return sameFolder(git(dir, ['rev-parse', '--show-toplevel']).toString().trim(), dir); } catch { return false; }
}
function isShallow(dir) {
  try { return git(dir, ['rev-parse', '--is-shallow-repository']).toString().trim() === 'true'; } catch { return false; }
}
function blob(dir, commit, rel) {
  try { return git(dir, ['cat-file', 'blob', `${commit}:${rel}`]); }
  catch (e) { throw new PinError(`Could not read ${rel} at commit ${commit.slice(0, 7)} in ${dir}: ${String(e.stderr || e.message).trim().split('\n')[0]}`); }
}
function tree(dir, commit, prefix) {
  const args = ['ls-tree', '-r', '-z', '--name-only', commit];
  if (prefix) args.push('--', prefix.replace(/\/?$/, '/'));
  return git(dir, args).toString('utf8').split('\0').filter(Boolean); // Git's own order: by path, byte for byte
}

/** The Lab's files as they were at the 3 November snapshot. */
export function openLab(labDir) {
  const commit = isGitCheckout(labDir) ? PINS.lab.find(id => hasCommit(labDir, id)) : undefined;
  const history = isGitCheckout(labDir);
  const explain = history
    ? `This clone has neither snapshot commit (${PINS.lab.map(id => id.slice(0, 7)).join(' or ')}). Fetch it with:  git fetch origin tag ${PINS.labTag}`
    : 'This copy has no Git history, so only the twelve table inputs can be read, and only if they match the snapshot.';
  const verify = (rel, text) => {
    const want = INPUT_SHA256[rel];
    if (want && sha256(text) !== want) {
      throw new PinError(`${rel} does not match the 3 November snapshot (tag ${PINS.labTag}). ${commit ? 'The pinned commit returned different content.' : 'This copy holds a later or edited file. Use a full clone, or the files of the snapshot.'}`);
    }
    return text;
  };
  return {
    source: commit ? `git:${commit}` : 'files-verified-by-sha256',
    commit,
    read(rel) {
      if (commit) return verify(rel, toLf(blob(labDir, commit, rel).toString('utf8')));
      if (!INPUT_SHA256[rel]) throw new PinError(`${rel} is not one of the twelve verified table inputs. ${explain}`);
      let text;
      try { text = fs.readFileSync(path.join(labDir, rel), 'utf8'); }
      catch { throw new PinError(`${rel} is missing from ${labDir}. ${explain}`); }
      return verify(rel, toLf(text));
    },
    json(rel) { return JSON.parse(this.read(rel)); },
    /** Paths under a folder at the snapshot, relative to the repository root. Needs Git history. */
    list(prefix) {
      if (!commit) throw new PinError(`Listing ${prefix} needs the snapshot commit. ${explain}`);
      return tree(labDir, commit, prefix);
    },
  };
}

/** PublishOps CrossCast at one fixed commit, read from a clone's Git objects. */
export function openCrossCast(ccDir, commit) {
  if (commit !== PINS.crosscastEvaluated && commit !== PINS.crosscastRelease) {
    throw new PinError(`${commit} is not one of the two CrossCast commits the paper evaluates.`);
  }
  if (!fs.existsSync(ccDir) || !isGitCheckout(ccDir)) {
    throw new PinError(`No clone of publishops-crosscast at ${ccDir}. Clone https://github.com/whitecarp/publishops-crosscast next to this repository (full history), or set CROSSCAST_REPO to a clone.`);
  }
  if (!hasCommit(ccDir, commit)) {
    const advice = isShallow(ccDir)
      ? `It is a shallow clone. Fetch its full history:  git -C "${ccDir}" fetch --unshallow`
      : `Fetch the upstream history:  git -C "${ccDir}" fetch https://github.com/whitecarp/publishops-crosscast.git`;
    throw new PinError(`The publishops-crosscast clone at ${ccDir} lacks commit ${commit.slice(0, 7)}. ${advice}`);
  }
  return {
    source: `git:${commit}`,
    commit,
    files(prefix) { return tree(ccDir, commit, prefix); },
    read(rel) { return toLf(blob(ccDir, commit, rel).toString('utf8')); },
    /** Size in bytes of the file as stored in Git (the same on every system). */
    size(rel) { return Number(git(ccDir, ['cat-file', '-s', `${commit}:${rel}`]).toString().trim()); },
  };
}

/** Returns the options given to a script, and stops on any that it does not know (a mistyped --check must not start a build). */
export function knownOptions(allowed) {
  const given = process.argv.slice(2);
  const unknown = given.filter(option => !allowed.includes(option));
  if (unknown.length) {
    throw new PinError(`Unknown option ${unknown.join(' ')}. ${allowed.length ? `Use ${allowed.join(' or ')}, or no option.` : 'This script takes no options.'}`);
  }
  return given;
}

/** Stops a builder when its data differ from the numbers printed in its fixed captions. */
export function assertSnapshot(checks) {
  const wrong = Object.entries(checks).filter(([key, value]) => SNAPSHOT_FACTS[key] !== value);
  if (wrong.length) {
    throw new PinError('The data differ from the numbers this document prints: ' + wrong.map(([key, value]) => `${key} is ${value}, expected ${SNAPSHOT_FACTS[key]}`).join('; '));
  }
}

/** Runs a script body and turns a PinError into a clear message and exit code 1. */
export function failClearly(main) {
  try { return main(); }
  catch (e) {
    if (!(e instanceof PinError)) throw e;
    console.error(`\nSTOPPED: ${e.message}\n`);
    process.exit(1);
  }
}

/** For scripts written as top-level statements: turns any PinError into a clear message and exit code 1. */
export function stopClearlyOnPinError() {
  process.on('uncaughtException', e => {
    if (e instanceof PinError) { console.error(`\nSTOPPED: ${e.message}\n`); process.exit(1); }
    console.error(e);
    process.exit(1);
  });
}

/** Names of the files directly inside a folder (no subfolders), from a list of repository paths. */
export function directChildren(paths, folder) {
  const prefix = folder.replace(/\/?$/, '/');
  return paths.filter(p => p.startsWith(prefix) && !p.slice(prefix.length).includes('/')).map(p => p.slice(prefix.length));
}
