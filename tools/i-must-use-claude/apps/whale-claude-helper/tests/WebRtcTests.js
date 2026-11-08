"use strict";

// Runs the shipped page in an isolated VM. Fake RTC, DOM, and timers never use a browser or network.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const html = fs.readFileSync(path.join(__dirname, "..", "assets", "score-page.html"), "utf8");
const scripts = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
assert.equal(scripts.length, 1, "test the single shipped inline script");
const source = scripts[0][1].replace(/\}\)\(\);\s*$/, "globalThis.pageUnderTest={detectWebrtc,show,definitions};\n})();");
assert.notEqual(source, scripts[0][1], "expose page functions without copying their implementation");

async function flush() {
  for (let i = 0; i < 8; i++) await Promise.resolve();
}

class FakeClock {
  constructor() { this.now = 0; this.nextId = 1; this.timers = new Map(); }
  setTimeout(callback, delay) {
    const id = this.nextId++;
    this.timers.set(id, { at: this.now + delay, callback });
    return id;
  }
  clearTimeout(id) { this.timers.delete(id); }
  async tick(milliseconds) {
    const target = this.now + milliseconds;
    for (;;) {
      const next = [...this.timers].filter(([, timer]) => timer.at <= target)
        .sort((a, b) => a[1].at - b[1].at || a[0] - b[0])[0];
      if (!next) break;
      this.now = next[1].at;
      this.timers.delete(next[0]);
      next[1].callback();
      await flush();
    }
    this.now = target;
    await flush();
  }
}

class FakeElement {
  constructor(tag) { this.tag = tag; this.children = []; this.listeners = {}; this._text = ""; }
  set textContent(value) { this._text = String(value); this.children = []; }
  get textContent() { return this._text; }
  appendChild(child) { this.children.push(child); }
  addEventListener(name, callback) { this.listeners[name] = callback; }
}

function harness(options = {}) {
  const clock = new FakeClock(), connections = [], sets = [], elements = new Map(), sent = [];
  class TrackedSet extends Set {
    constructor(...args) { super(...args); sets.push(this); }
  }
  class FakeRTC {
    constructor(config) {
      if (options.constructorError) throw options.constructorError;
      this.config = config;
      this.closeCount = 0;
      this.localDescriptionCount = 0;
      this.iceGatheringState = "new";
      connections.push(this);
    }
    createDataChannel() {
      if (options.channelError) throw options.channelError;
      if (options.onChannel) options.onChannel(this);
      return {};
    }
    createOffer() {
      if (options.offerThrow) throw options.offerThrow;
      return options.offer || Promise.resolve({ type: "offer", sdp: "fake" });
    }
    setLocalDescription() {
      this.localDescriptionCount++;
      if (options.localThrow) throw options.localThrow;
      return options.local || Promise.resolve();
    }
    close() {
      this.closeCount++;
      if (options.closeError) throw options.closeError;
    }
  }
  const document = {
    getElementById(id) {
      if (!elements.has(id)) elements.set(id, new FakeElement(id));
      return elements.get(id);
    },
    createElement: tag => new FakeElement(tag)
  };
  const sandbox = {
    console, Promise, Set: TrackedSet, document,
    navigator: { userAgent: "FakeBrowser", platform: "FakeOS", language: "en-US", languages: ["en-US"] },
    setTimeout: (callback, delay) => clock.setTimeout(callback, delay),
    clearTimeout: id => clock.clearTimeout(id),
    AbortController: class { constructor(){this.signal={};} abort(){} },
    async fetch(url, request) {
      if(!options.allowFixtureSend)throw new Error("network access is forbidden in WebRTC tests");
      sent.push(JSON.parse(request.body)); return {ok:true};
    }
  };
  if (!options.unsupported) sandbox.RTCPeerConnection = FakeRTC;
  sandbox.window = sandbox;
  vm.createContext(sandbox);
  vm.runInContext(source.replace('__WEBRTC_ONLY__',options.webRtcOnly?'true':'false').replace('__TOKEN__','WebRtcOnlyOfflineFixtureSession'), sandbox, { filename: "assets/score-page.html" });
  return {
    clock, connections, sets, elements, sent, page: sandbox.pageUnderTest,
    get pc() { return connections[0]; },
    candidate(text) { this.pc.onicecandidate?.({ candidate: text === null ? null : { candidate: text } }); },
    error() { this.pc.onicecandidateerror?.({ errorCode: 701, errorText: "private 192.0.2.91", address: "192.0.2.91", url: "stun:private.example:3478" }); },
    complete() { this.pc.iceGatheringState = "complete"; this.pc.onicegatheringstatechange?.(); }
  };
}

async function begin(options) {
  const h = harness(options);
  h.settled = false;
  h.promise = h.page.detectWebrtc();
  h.promise.then(value => { h.settled = true; h.result = value; });
  await flush();
  return h;
}

function cleaned(h) {
  assert.equal(h.clock.timers.size, 0, "deadline timer cleared");
  for (const pc of h.connections) {
    assert.equal(pc.closeCount, 1, "connection closed once");
    assert.equal(pc.onicecandidate, null);
    assert.equal(pc.onicegatheringstatechange, null);
    assert.equal(pc.onicecandidateerror, null);
  }
  for (const set of h.sets) assert.equal(set.size, 0, "temporary addresses cleared");
}

function privateResult(result) {
  const serialized = JSON.stringify(result);
  for (const secret of ["192.0.2.10", "192.0.2.91", "2001:0db8:0000:0000:0000:0000:0000:0042", "private.example", "candidate:", "errorText", "errorCode", "address"]) {
    assert.ok(!serialized.includes(secret), "result excludes raw network detail: " + secret);
  }
  assert.ok(result.display.length <= 256);
}

const ipv4 = "candidate:1 1 UDP 2122260223 192.0.2.10 52704 typ host";
const ipv6 = "candidate:2 1 UDP 2122260223 2001:0db8:0000:0000:0000:0000:0000:0042 52704 typ host";
const tests = [];
function test(name, run) { tests.push({ name, run }); }

test("candidate at 1.2 seconds is measured instead of the old 1-second timeout", async () => {
  const h = await begin();
  await h.clock.tick(1000);
  assert.equal(h.settled, false);
  await h.clock.tick(200);
  h.candidate(ipv4); h.candidate(null);
  const result = await h.promise;
  assert.equal(result.status, "measured"); assert.equal(result.risk, 0.5);
  assert.match(result.display, /count: 1/); assert.doesNotMatch(result.display, /Partial/);
  privateResult(result); cleaned(h);
});

test("no candidates remains pending until exactly 8 seconds then is unknown timeout", async () => {
  const h = await begin();
  await h.clock.tick(7999); assert.equal(h.settled, false);
  await h.clock.tick(1);
  assert.equal(h.result.status, "timeout"); assert.equal(h.result.risk, null);
  assert.match(h.result.display, /8/); privateResult(h.result); cleaned(h);
});

test("701 with completed empty gathering is an unknown server probe failure", async () => {
  const h = await begin(); h.error();
  await flush(); assert.equal(h.settled, false, "a single failed route is not an immediate terminal result");
  h.candidate(null);
  const result = await h.promise;
  assert.equal(result.status, "server_error"); assert.equal(result.risk, null);
  privateResult(result); cleaned(h);
});

test("701 without gathering completion remains server_error at the deadline", async () => {
  const h = await begin(); h.error(); await h.clock.tick(8000);
  assert.equal(h.result.status, "server_error"); assert.equal(h.result.risk, null);
  privateResult(h.result); cleaned(h);
});

test("a later successful candidate wins over an earlier STUN route error", async () => {
  const h = await begin(); h.error(); h.candidate(ipv4); h.complete();
  const result = await h.promise;
  assert.equal(result.status, "measured"); assert.equal(result.risk, 0.5);
  assert.doesNotMatch(result.display, /Partial/); privateResult(result); cleaned(h);
});

test("usable candidates stay measured with partial text if collection times out", async () => {
  const h = await begin(); h.candidate(ipv4); h.error(); await h.clock.tick(8000);
  assert.equal(h.result.status, "measured"); assert.equal(h.result.risk, 0.5);
  assert.match(h.result.display, /Partial/); privateResult(h.result); cleaned(h);
});

test("completed empty gathering is measured zero without a STUN error", async () => {
  const h = await begin(); h.complete();
  const result = await h.promise;
  assert.equal(result.status, "measured"); assert.equal(result.risk, 0);
  assert.match(result.display, /candidates: 0/); cleaned(h);
});

test("empty candidate ends a generation, not all gathering", async () => {
  const h = await begin(); h.candidate(""); await flush();
  assert.equal(h.settled, false);
  await h.clock.tick(1200); h.candidate(ipv4); h.candidate(null);
  assert.equal((await h.promise).risk, 0.5); cleaned(h);
});

test("empty candidate alone reaches the unknown timeout", async () => {
  const h = await begin(); h.candidate(""); await h.clock.tick(8000);
  assert.equal(h.result.status, "timeout"); cleaned(h);
});

test("null candidate finishes completed zero gathering", async () => {
  const h = await begin(); h.candidate(null);
  const result = await h.promise;
  assert.equal(result.status, "measured"); assert.equal(result.risk, 0); cleaned(h);
});

test("missing API is explicitly unsupported and creates no connection or timer", async () => {
  const h = await begin({ unsupported: true });
  assert.equal(h.result.status, "unsupported"); assert.equal(h.result.risk, null);
  assert.equal(h.connections.length, 0); cleaned(h);
});

for (const stage of ["constructorError", "channelError", "offerThrow", "localThrow"]) {
  for (const name of ["NotSupportedError", "SecurityError"]) {
    test(stage + " " + name + " is classified without exposing its message", async () => {
      const error = Object.assign(new Error("private 192.0.2.91"), { name });
      const h = await begin({ [stage]: error });
      const result = await h.promise;
      assert.equal(result.status, name === "NotSupportedError" ? "unsupported" : "error");
      assert.equal(result.risk, null); privateResult(result); cleaned(h);
    });
  }
}

for (const stage of ["offer", "local"]) {
  for (const name of ["NotSupportedError", "OperationError"]) {
    test(stage + " rejected promise " + name + " is classified and cleaned", async () => {
      const error = Object.assign(new Error("private 192.0.2.91"), { name });
      const h = await begin({ [stage]: Promise.reject(error) });
      const result = await h.promise;
      assert.equal(result.status, name === "NotSupportedError" ? "unsupported" : "error");
      assert.equal(result.risk, null); privateResult(result); cleaned(h);
    });
  }
}

test("all raw addresses are deduplicated then omitted from the result", async () => {
  const h = await begin(); h.candidate(ipv4); h.candidate(ipv4); h.candidate(ipv6); h.complete();
  const result = await h.promise;
  assert.match(result.display, /count: 2/); privateResult(result); cleaned(h);
  assert.deepEqual(Object.keys(result).sort(), ["display", "risk", "status"]);
});

test("stale saved callbacks after a deadline cannot change results or retain addresses", async () => {
  const h = await begin();
  const candidate = h.pc.onicecandidate, error = h.pc.onicecandidateerror, state = h.pc.onicegatheringstatechange;
  await h.clock.tick(8000);
  const before = JSON.stringify(h.result);
  candidate({ candidate: { candidate: ipv4 } }); candidate({ candidate: null }); error({ errorCode: 701 });
  h.pc.iceGatheringState = "complete"; state(); await flush();
  assert.equal(JSON.stringify(h.result), before); privateResult(h.result); cleaned(h);
});

test("completion and deadline at the same time resolve and close once", async () => {
  const h = await begin();
  const state = h.pc.onicegatheringstatechange;
  h.clock.setTimeout(() => { h.pc.iceGatheringState = "complete"; state(); }, 8000);
  await h.clock.tick(8000);
  assert.equal(h.result.status, "timeout"); cleaned(h);
});

test("late offer resolution does not set a local description on a closed connection", async () => {
  let resolveOffer;
  const h = await begin({ offer: new Promise(resolve => { resolveOffer = resolve; }) });
  await h.clock.tick(8000);
  resolveOffer({ type: "offer", sdp: "fake" }); await flush();
  assert.equal(h.pc.localDescriptionCount, 0); assert.equal(h.result.status, "timeout"); cleaned(h);
});

test("late offer rejection cannot replace a completed measured result", async () => {
  let rejectOffer;
  const h = await begin({ offer: new Promise((_, reject) => { rejectOffer = reject; }) });
  h.candidate(ipv4); h.candidate(null); await flush();
  rejectOffer(new Error("private 192.0.2.91")); await flush();
  assert.equal(h.result.status, "measured"); privateResult(h.result); cleaned(h);
});

test("synchronous gathering completion does not leave a deadline behind", async () => {
  const h = await begin({ onChannel: pc => pc.onicecandidate({ candidate: null }) });
  assert.equal(h.result.status, "measured"); assert.equal(h.pc.localDescriptionCount, 0); cleaned(h);
});

test("a throwing close still resolves and clears the handlers and timer", async () => {
  const h = await begin({ closeError: new Error("close failed") }); h.candidate(null);
  assert.equal((await h.promise).status, "measured"); cleaned(h);
});

for (const [status, label] of [
  ["timeout", "超时 · 未知 / Timed out"],
  ["server_error", "STUN 失败 · 未知 / STUN failed"],
  ["unsupported", "不支持 · 未知 / Unsupported"],
  ["error", "失败 · 未知 / Error"],
  ["unavailable", "不可用 · 未知 / Unavailable"]
]) {
  test(status + " displays a distinct unknown status without scoring it as zero", async () => {
    const h = harness();
    const signals = Array.from(h.page.definitions, definition => ({
      id: definition.id, status: definition.id === "webrtcLeak" ? status : "measured",
      risk: definition.id === "webrtcLeak" ? null : 0, display: "safe summary"
    }));
    h.page.show({ browserId: "Other", signals });
    const row = h.elements.get("rows").children[4];
    assert.equal(row.children[1].textContent, label); assert.equal(row.children[1].className, "unknown");
    assert.equal(row.children[2].textContent, "—");
    assert.match(h.elements.get("score").textContent, /measured subtotal/);
    assert.match(h.elements.get("coverage").textContent, /9 \/ 10/);
  });
}

test("WebRTC-only click skips all other signal collectors and returns only one visible row", async () => {
  const h=harness({webRtcOnly:true,unsupported:true,allowFixtureSend:true});
  let otherCalls=0;
  for(const definition of h.page.definitions)if(definition.id!=="webrtcLeak")definition.run=()=>{otherCalls++;throw new Error("must not inspect this signal");};
  await h.elements.get("start").listeners.click();
  assert.equal(otherCalls,0);assert.equal(h.sent.length,1);
  const payload=h.sent[0];assert.equal(payload.signals.length,10);
  for(const signal of payload.signals.filter(s=>s.id!=="webrtcLeak")){
    assert.equal(signal.status,"unavailable");assert.equal(signal.risk,null);assert.equal(signal.fonts,undefined);
    assert.match(signal.display,/WebRTC-only session/);
  }
  assert.equal(payload.signals.find(s=>s.id==="webrtcLeak").status,"unsupported");
  assert.equal(h.elements.get("rows").children.length,1);
  assert.equal(h.elements.get("points-header").hidden,true);
  assert.match(h.elements.get("score").textContent,/Result unknown/);
  assert.equal(h.elements.get("return").hidden,false);
  cleaned(h);
});

test("WebRTC-only measured candidate is probe returned, never a safety pass or account score", async () => {
  const h=harness({webRtcOnly:true});
  const signals=Array.from(h.page.definitions,d=>({id:d.id,status:d.id==='webrtcLeak'?'measured':'unavailable',risk:d.id==='webrtcLeak'?0.5:null,display:'count only'}));
  h.page.show({browserId:'Other',signals});
  assert.equal(h.elements.get('score').textContent,'探测已返回 / Probe returned');
  assert.equal(h.elements.get('rows').children.length,1);
  assert.equal(h.elements.get('rows').children[0].children[2].hidden,true);
  assert.match(h.elements.get('coverage').textContent,/not DNS privacy or account availability/);
});

test("notice discloses the 8-second wait and explains candidate counts", async () => {
  assert.match(html, /最多等待 8 秒/); assert.match(html, /waits up to 8 seconds/);
  assert.match(html, /Candidate counts do not confirm a leak/);
  assert.match(html, /zero candidates do not prove network safety/);
});

(async () => {
  let passed = 0;
  for (const entry of tests) {
    try { await entry.run(); passed++; console.log("PASS " + entry.name); }
    catch (error) { console.error("FAIL " + entry.name); throw error; }
  }
  console.log("WebRTC: " + passed + "/" + tests.length + " tests passed (offline).");
})().catch(error => { console.error(error); process.exitCode = 1; });
