# Round-two instruments

These tools record one-off, approved study jobs. They do not grant permission
to act on an account. No scheduler, network client or fault injector is installed
by importing these modules. C15 provides opt-in planned fault arming in admitted steps.

Use Node 20 or newer. All commands run from the Lab checkout. Set ROUND2_HOME to
a private folder outside every Git checkout, shared by the operator and checker.
begin prints its resolved real path so the two apps can compare it. Use separate
private homes and site journals for owner and tester.

Public records are in data/round2/SITE/journal.jsonl. The companion .head file
retains the last count and digest and detects a removed final record. External
seals/anchors remain necessary against an actor rewriting the journal and its
head together. Modules 3 and 4 provide seals and export. Never publish private homes
or raw captures.

## Offline setup example

The commands below illustrate the interface, not account authorization. Use
stand-ins and empty homes until the study's separate account gates are cleared.

Before a protocol seal or any `begin`, create private `ROUND2_HOME/site.json`
with `{"site":"owner","vpub_home":"ABSOLUTE_EXISTING_DIRECTORY"}`
(use `tester` for the tester site). For rehearsals use an empty directory outside
Git. The wrapper reads only directory metadata, never its profiles or sign-ins.
`protocol-seal` records `vpub_home_sha256`, an HMAC-SHA-256 of the resolved path
using the site's private pseudonym salt. The public binding cannot be checked
against guessed account names without that salt.
The first bound seal fixes that directory across later phases. Missing, invalid,
unsealed or changed setup refuses `begin` and `step` with fixed `vpub_home_*`
words. Legacy journals remain readable but cannot admit a child until a bound
protocol seal is made. Every child receives this resolved `VPUB_HOME`, overriding
the invoking shell, as well as `VPUB_BROWSER_IDLE_SECONDS=0`. This pins a path,
not the identity of a directory recreated at the same path; keep it in place.

Keep `ROUND2_HOME/salt` for the entire study. A bound journal whose salt is lost
refuses commands without generating a replacement; losing that salt ends that
journal. `vpub_home_invalid` also covers an unreadable/invalid salt or a leftover
`salt.lock`; the word does not diagnose the folder itself. A refused protocol
seal prints the fixed home-error word and appends no new seal.
Legacy journals **without** a binding can receive their first bound seal.
Journals already sealed with the former unkeyed binding (through Lab `5fcd753`)
cannot continue under this code; preserve them and start a new rehearsal journal.

The wrapper removes every inherited `BILI_CLI_HOME` setting from child processes,
so Bilibili uses its standard location. If a site needs a custom location, add
`"bili_cli_home":"ABSOLUTE_EXISTING_DIRECTORY"` to private `site.json` **before**
the first seal. That resolved path becomes part of the same keyed binding and
is set for every child. Adding, removing or changing it after sealing refuses
commands and cannot be replaced by a later seal. Offline rehearsals must use
an explicit empty custom Bilibili directory; they must not invoke Bilibili with
the standard live location. Validation reads only directory metadata.
The standard Bilibili location still follows `APPDATA` on Windows and
`XDG_CONFIG_HOME` on systems using that convention. To pin a location independently
of those shell settings, name `bili_cli_home` before the first seal.

Every study CrossCast export must specify `--since PHASE_SEAL_UTC` and
`--until CLOSE_UTC` (and `--times exact` for local reconciliation). Keep the
output outside both data homes. `daily` consumes an existing export; it never
runs CrossCast or constructs an unwindowed export. Without a time window,
CrossCast includes historical ledger publications; a window includes only
publications referred to by events in that window.

Keep jobs.json under ROUND2_HOME, for example:

~~~json
[{"id":"rehearsal-1","class":"write","due_at":"2026-11-11T12:00:00.000Z","run_class":"test_post","platforms":["youtube"]}]
~~~

The owner supplies their own words to standard input for handoff and go-ahead:

~~~text
node scripts/round2/journal.mjs protocol-seal --site owner --phase rehearsal --protocol PROTOCOL_FILE --lab-commit LAB_COMMIT_40_HEX --crosscast-commit CROSSCAST_COMMIT_40_HEX
node scripts/round2/journal.mjs handoff --site owner --to gemini --by owner
node scripts/round2/journal.mjs go-ahead --site owner --job rehearsal-1 --scope one_write --count 1 --by owner
node scripts/round2/gate-run.mjs begin --site owner --slot gemini --launcher manual
node scripts/round2/gate-run.mjs step --site owner --slot gemini --run RUN --job rehearsal-1 --role publish --platform youtube -- STAND_IN_COMMAND
node scripts/round2/gate-run.mjs end --site owner --slot gemini --run RUN
node scripts/round2/journal.mjs verify data/round2/owner/journal.jsonl
~~~

Use --journal PATH for an isolated rehearsal site. It must consistently name the
same journal throughout a run; site mixing is rejected. No recurring schedules
or exec subcommand are provided, following brief v1.1.

Additional journal commands: observe, note, violation, sign-off, stop, clear-stop,
and protocol-seal. All fields follow journal-record-v1.schema.json, which the
library loads directly for validation. Unknown fields and free text are refused.
Words go to private captures; only their digest is public. Protocol text digests
normalize CRLF to LF. Handoff records the owner's words before changing
operator.json. Never edit that file to bypass a handoff.

## Enforcement and limits

- A write spends one unit of GO_AHEAD through an fsynced reservation before
  starting a child. An uncertain/spawn-failed attempt is conservatively spent,
  never automatically refunded or retried.
- A fresh GATE_DECISION is appended before each child to prove that recording
  still works. A broken or unwritable journal means no child; exit 70.
- run.lock is durable session state, owned by the invoking shell between steps.
  During a step it also tracks wrapper/child PIDs. A later begin closes an
  abandoned run as interrupted only when these liveness witnesses are gone.
  It never kills or resumes a child. A live session must explicitly end.
- The short exclusive .lock / operation.guard files are fail-closed. A process
  dying inside a critical write can leave one behind: preserve the evidence and
  request review. No automatic lock deletion or receipt reset is provided.
- Missing approval, a changed operator, an active step, STOP, an unadmitted job,
  or a standing policy limit refuses with exit 5 and a fixed reason word.
- Test publishes are only on declared YouTube/Douyin jobs. Four starts per
  platform per 24 hours, 20-minute spacing after a publish/outside upload, and a
  48-hour warning hold apply. Bilibili deletes are refused. The wrapper binds
  step platform labels to job labels; those labels are operator-supplied metadata,
  not independent proof of the actual account or a private plan's visibility.
  CrossCast's own target/visibility checks remain required.
- Known vpub actions are classified conservatively. In particular, plain vpub
  verify is write-class because CrossCast may finish pending chapters/widgets;
  delete --verify-only and edit --verify-only are read-class. Unknown commands
  are write-class. Only an unknown tool explicitly labelled --tool other may
  use --class read/local. Known tools cannot use that override.
- Windows .cmd/.bat wrappers are not passed through a shell. Use a directly
  executable CLI (.exe) or an explicitly provided interpreter; interpreter
  commands remain conservatively write-class.
- Captures store each stdout/stderr line (including a final unterminated fragment)
  with UTC arrival time and base64 of the exact bytes. Streams are relayed
  unchanged, and child exit codes are preserved (spawn failure is 127).
  An interrupted process tree can leave an open run or partial private capture;
  it never causes an automatic second command.

No command validates a human's claimed identity or consent independently.
Closed identifiers and session labels must be nonprivate study labels.
These instruments provide evidence of the checked workflow, not an OS boundary
against a person launching other commands or editing the underlying files.

## Brief v1.2: sessions and planned runs

A session is one run id. A planned run is one job id and can span sessions.
`end` closes only the session, even when a child returned a refusal or nonzero
exit. A wrapper recording failure marks the session failed; the spent reservation
and any capture remain. A job closes only through `close-job`; there is no reopen.

~~~text
node scripts/round2/gate-run.mjs close-job --site owner --slot gemini --job rehearsal-1 --reason done
node scripts/round2/journal.mjs clear-guard --site owner --by owner
~~~

For a test post, done requires a successful delete_verify after the last write;
nothing_posted requires an absent observation after the last write; removed_by_hand
requires that job's manual_step note. Abandoned reads the person's words from stdin
and stores only their digest. Other job classes use done or abandoned. The operator
is responsible for the required real-release observation windows before closing.

listed_test_posts units cover publish, repeat_same, resume, verify, edit and
changed_plan only. Other writes, especially delete, require one_write units of
that exact job. Eligible writes consume listed units first. Old unscoped pending
reservations are conservatively deducted from both pools; no credit is invented.

Use clear-guard **only after Codex has reviewed the evidence and on the owner's
word**, supplied through stdin. It requires a valid journal and positively dead
PID metadata for every stale guard; live or ambiguous guards stay closed. Recovery
records guard_cleared with the words digest, separately from manual post removal.
It preserves STOP, session state, reservations and receipts; it never kills or
retries a child. A damaged journal, an empty guard or a stale recovery guard needs
separate review, not automatic clearing. PID reuse can conservatively refuse.

## Seal, export, daily check and reconcile (Modules 3 and 4)

~~~text
node scripts/round2/daily.mjs --site owner
node scripts/round2/seal.mjs --site owner --day 2026-11-12
node scripts/round2/export.mjs --site owner --out PUBLIC_FILE --privacy --deny PRIVATE_DENY_FILE --crosscast CROSSCAST_AUDIT_FILE
node scripts/round2/reconcile.mjs --site owner --crosscast CROSSCAST_AUDIT_FILE
node scripts/round2/daily.mjs --site owner --apply
node scripts/round2/journal.mjs sign-off --site owner --day 2026-11-12 --by owner --answer yes
~~~

All accept --journal PATH for isolated rehearsals. --crosscast and --deny are
optional. The person supplies a real sign-off answer separately: daily never
creates one. Its dry run prints six summary lines, two lists and two prompt
lines. Daily partitions and seals use the UTC calendar date, not the local
production date; use --day for each UTC day with records. An earlier unsealed
day can be covered by a later seal, but that does not supply its sign-off.
--apply seals the checked prefix and prints the prompt. A later
SIGN_OFF is a new journal record and is covered by a later day's seal.

Seals contain the journal prefix count/head, counts by type, Node/journal
versions and the previous seal's SHA-256. Their bytes are canonical UTF-8 with
LF. Existing different seals are refused, including a second same-day seal after
more records. Use seal once the day's recording is ready, not after every step.
The local seals/.head digest inventory detects accidental removal/rewriting,
including the last seal. A crash between the seal and its inventory can require
review; no silent repair occurs. These are local anchors, not network timestamps
or protection against rewriting both evidence and anchors. Keep external copies
under the protocol's separate sealing procedure.

Export is deterministic JSON (round2-export/1). Every original journal record is
schema/chain checked and every existing seal is verified. Missing seals are
explicitly reported as unsealed_records; no seal is invented. The denylist is
one private substring per line and is applied before privacy coarsening. Output
cannot overwrite the journal/seal directory or the private home. Captures are
not opened by export. --privacy replaces all clock timestamps with UTC dates, drops every minute-valued field and marks the records as derived. All source-chain
digests (prev, source head, record source_sha256, seal heads/previous digests,
seal source_sha256, and SIGN_OFF heads) are HMAC commitments under the site's
private salt with a common domain. Equal original digests remain equal, so a
reader can check link consistency, including across seals. The reader cannot
recompute the source chain from the derived copy or brute-force a timestamp
against an unkeyed digest. A bound site's missing salt refuses export.
Words digests are also keyed in the privacy copy to protect legacy records.
Exported IDs must be nonprivate study labels even when pattern-valid. CrossCast
relative seconds are not minute-coarse times; its separate export scope must
follow the protocol and consent, not an assumption that relative means coarse.

Reconcile checks capture bytes against STEP digests, identifies open uncertain
steps, fixed refusal codes, violations, and started publish/edit/delete steps
still owed a qualifying look. The jobs.json classes exclude refusal jobs and
drills; missing class metadata is reported as unknown. A 2-minute look is retained
but does not satisfy the protocol's window. A look at 10 through 60 minutes is
on_time; after 60 through 1,440 minutes it satisfies the obligation as late.
look_checks preserves those statuses and early observation sequence numbers.
Without a qualifying look, status is pending or early until 24 hours after the
STEP, then no_ground_truth. Unclear looks and looks after 24 hours never qualify.
The daily summary includes these all-record status counts and lists that day's
go-aheads and observations before prompting for sign-off. Failed spawns and read-only edit verification do not create an
owed look. This is not the separate full 24-hour release schedule audit.
Only a matching clear observation or successful operation read-back settles
uncertainty. A successful vpub verify may close a post despite its write class.
Intervening write-class steps keep it open unless a complete, uniquely bound
CrossCast command proves no press: a refusal at exit 2, or repeat_same/resume/
verify at exit 0 or 4. The before/after snapshots must be present and unchanged,
with no call, span, press or write evidence and no reported gaps/damaged loss
records. Without this evidence the strict open count stays. In-doubt steps are
grouped by job/platform/operation from the first step, under the planned rule
of one attempt per platform per job; steps lists every contributing record.
Plain vpub verify remains write-class. Printed session
counts use run ids; planned runs use job ids. No missed_slots/timer is created.

Each step gives its child AWL_RUN_ID, the durable reservation's random 32-hex id,
and records it as STEP.d.awl_run_id. It overrides an inherited value only in that
child; the parent environment is unchanged. CrossCast records this separately
from its own command run id at command_start and exports it in events and runs.
Reconcile joins a unique write command to a unique admitted write STEP by this
id, with relative or exact exports. It never falls back to time coverage.
write_bindings gives matched, missing_id, unknown_id, ambiguous_id,
ineligible_step or unverifiable for each observed write command. ungated_writes
counts those without a proven binding; it is not a count of platform mutations.
A missing STEP after a wrapper crash remains unmatched even if a private
reservation exists. Reused ids, failed spawns and read/local STEPs cannot cover a
write. Old records lacking the optional id remain readable and are not backfilled.
command_binding is journal_only without input and unavailable for damaged
CrossCast chains, where ungated_writes and matched_write_commands stay null.
time_alignment remains diagnostic only; commands_without_time_coverage is
replaced by write_bindings. An id is a correlation label, not a secret or proof
against deliberate environment copying. Lost commands remain unobservable.

Both Lab export and reconcile carry the closed global `crosscast_integrity`
counters and flags, including unreadable loss records and losses with unknown
original times. Missing older fields stay null; unknown text is not copied.
An unreadable loss record cannot be assigned to a particular command, so the
reconcile cannot use an otherwise matched refusal as proof that no operation
occurred when that flag is present. It keeps the case open for review.

Both Lab export and reconcile carry CrossCast per-command dropped_events,
sequence_gap, chain_ok and loss_complete. Older inputs give null for absent
loss counts. False loss_complete reflects CrossCast's process-local lower bound:
zero reported drops does not prove zero loss on process death. No private
CrossCast fields are copied into Lab exports. CrossCast hashes the previous line
including LF; this journal hashes the previous line excluding LF.

### Brief v1.3 recording changes (C27)

Wrapper refusals now append STEP_REFUSED with closed reason words and no child.
Invalid descriptors become null or an empty platform list; rejected arguments
are never copied. A journal failure remains exit 70. GATE_DECISION jobs retain
due_at, completed and authorized, exactly as passed to planRun, including the
per-child recheck. Old records stay readable and are not rewritten.

Use journal.mjs observe --platform youtube (or another planned platform).
An observation must name one platform on a multi-platform job. A legacy look
without a platform can qualify only when a single platform is established.
One look covers one job/platform/operation after its last started write-class
step that can press (post: publish/repeat_same/changed_plan/resume; edit; delete).
Plain verify, edit --verify-only and delete --verify-only do not move that point.
It never covers another platform. The person's minutes_after remains the source
for the window; recorded_before_claimed_minute flags entries written before the
minute they claim. Later transcription is allowed. This flag is reported for
review, not silently used to rewrite or discard the person's statement.

SIGN_OFF records words_sha256 (null when no explanation is supplied for yes).
Pipe the person's explanation into sign-off when answering no; an empty no is
refused. The original words remain in private captures, never the public log.
Every child gets VPUB_BROWSER_IDLE_SECONDS=0 regardless of the parent setting.

### C34: supplemental releases, approvals and fault setup

On a multi-platform job, keyed resume/verify/edit/delete steps (including delete
dry run and read-backs) require exactly one --platform from the job. Publish
steps retain the entire platform set. This is an operator declaration; the
wrapper does not decode an opaque receipt key to prove its identity. A test job
can close as done only with successful delete read-backs for every platform.
Nothing_posted requires post-absence observations for every platform and no
preceding delete step; post-delete absence is not evidence of never posting.

The gate admits a write job in a new session only while an unspent write unit
exists. Later read/local steps must therefore run in an admitted session, or
after the next real grant. They do not consume an additional write unit. Use
`2 + 3P` for a day's test-post allowance and `1 + 3P` for real releases (P platforms),
plus separately approved edits/deletes under the current site's rules. C20's
generated list must derive its counts from the current approved counts file.

A real release outside the sealed list is recorded separately; it never edits
jobs.json. Pipe the account owner's actual words into:

~~~text
node scripts/round2/journal.mjs real-release --site owner --job release-EXAMPLE --platform youtube --due-at 2026-11-20T14:00:00Z --by owner
~~~

REAL_RELEASE records class real_release, platforms, due time, allowance and a
private words commitment. It does not grant execution: a separate go-ahead is
required. The gate and reconcile merge these records with planned jobs, refuse
duplicate ids, and require one look per platform. Resume the current session or
begin the next one after recording the new job.

New words commitments at both sites are SHA-256(random 32-byte nonce || words).
The nonce is kept beside the private .words.txt as .words.nonce. Neither leaves
the site. Repeated short phrases produce different commitments. Old records
are never rewritten; retaining their original plain hashes outside privacy
mode must be reviewed under the protocol's publication rules.

The daily CrossCast export is run by Gemini in the operator's shell through the
wrapper, with --since at the phase seal (and --until at close), to a file outside
the CrossCast data home. `vpub audit export` and `vpub plan` are local-class;
Codex reads the resulting artifact and never runs CrossCast in the signed-in
home. A dedicated approved local/read job can admit these export steps without
inventing a write allowance for an exhausted release job.

C15 integrates the setup guard into planned-fault arming:
Gemini/the tester's operator, on that account owner's explicit word, registers
ROUND2_HOME/STOP using CrossCast setup --external-stop-file and enables
fault_injection in the selected config before the first planned shakedown fault.
After that setup, pipe the owner's words into:

~~~text
node scripts/round2/fault-setup.mjs --site owner --enabled true --by owner
~~~

This reads only the named config and records FAULT_SETUP; it changes no CrossCast
configuration. It requires both enabled fault configuration and exact study
stop registration. C15 must call assertFaultReady(ctx, kind) immediately before
arming: the saved record and current config must agree, and a stop-file fault
requires current registration. It must never record a fired fault from setup
alone. Every currently unarmed child strips all inherited VPUB_FAULT variants;
C15 may add the selected variable only after its per-step checks.

At close the authorized operator turns the setting off and removes/restores
the study stop registration, then records --enabled false through the same
command. Any existing unrelated stop registration must be reviewed before
setup and restored by that operator where appropriate. Codex does not make
these signed-in configuration changes. A stop is a checkpoint-based control;
record actual press/termination evidence rather than assuming that creating
the stop guarantees zero subsequent presses.
The operator can use the read-class command vpub status and inspect
background_browsers for an earlier live shared browser. This only reports;
the wrapper does not close an existing browser.

Reconcile prints in_doubt_open and in_doubt_open_strict. The former disregards
an intervening tool refusal only with a unique matched command, intact chain,
recorded start/end, zero reported losses, unchanged before/after press fields,
and no recorded platform call, span, press or write. Missing evidence retains
the strict result. This is proof within recorded events, not a guarantee
against unrecorded events or process death. ignored_refused_steps lists every
such exception; wrapper refusals themselves never started a child.


C35: `reconcile --as-of ISO_UTC_TIME` fixes and records the moment used for the
missing-ground-truth deadline; use the same value when reproducing a result.
`daily --as-of ISO_UTC_TIME` passes it through. Daily output explicitly marks
selected-UTC-day counts versus all-record running totals. A closing answer uses
`journal sign-off ... --closing`; ordinary answers record `closing: false`.
A repeat_same, changed_plan or resume requires an actually started publish for
that job/platform in the same planned run, including an earlier admitted session.
A failed spawn cannot satisfy it (`repeat_without_publish`).
The tester's copy says `dates_only_UTC`; compute timing results on site first.
The tester's operator requests CrossCast exports bounded by whole UTC days.


## C15: fault setup, private planned list and cleanup

No real-account action is authorized by these commands or by this code delivery.
Gemini (owner site) or the tester's operator performs setup only after that
account owner's specific approval, before a session starts. The configure
command records the supplied owner's words, saves only the two old settings
privately, registers ROUND2_HOME/STOP and enables fault_injection. Record-only
mode remains available to audit settings the operator changed separately.

~~~text
node scripts/round2/fault-setup.mjs configure --site owner --enabled true --by owner
node scripts/round2/plan-runs.mjs make --counts COUNTS_JSON --site owner --due-from 2026-11-19T00:00:00Z --first-short 1 --first-long 1
node scripts/round2/plan-runs.mjs check --sealed SHA256
node scripts/round2/fault-setup.mjs configure --site owner --enabled false --by owner
node scripts/round2/journal.mjs sign-off --site owner --day 2026-12-01 --answer yes --closing
node scripts/round2/plan-runs.mjs reveal --site owner
~~~

Setup/cleanup need the owner's words on stdin; do not paste these examples as
approval. Actual first clip numbers follow shakedown usage. `make` creates no
approval and starts no command. It requires an empty list destination; a partial
write is preserved for inspection. The private 32-byte seed plus counts and
explicit arguments determine the bytes. SHA-256 counter draws use rejection
sampling; timing pairs stay adjacent. Variant selection and clip allocation
are checked, both sites use separate pools, and jobs.json reveals no faults.
The owner refusal pool may be reused only while those clips were never uploaded;
retire any accidentally published refusal clip before another session. A tester
refusal receives its own unused clip. Supplemental real releases still use the
separate journal command. Real-slot/handoff day numbers describe the plan;
the human operator applies the protocol's actual daily gates and clip retirement.

A planned fault is selected only for one eligible private/unlisted YouTube or
private Douyin test-post job, the specified role, and a vpub executable. The
wrapper validates the current config immediately before appending FAULT and
spawning; any FAULT already recorded for this job/role consumes that choice,
even if it never fires. The frozen plan digest is checked when sealed. Inherited
VPUB_FAULT is removed on unarmed steps. Armed children receive before_press,
after_press, delete_readback_lost, or external; CrossCast's own in-tool private
visibility guard remains mandatory. The supported study build must include B8;
this Lab code alone does not prove an arbitrary installed executable supports it.

Stage matching uses whole stage words, not substrings. The edit kill waits for
reading back after pressing. Command-only termination targets only the command;
a detached worker may survive. Tree termination inspects the owned descendants,
ends the tree and checks each observed PID's creation identity afterward. It
never selects by process name. FAULT_RESULT separately records trigger, firing,
errors and per-PID end observations; no firing or all-ended claim is invented.
Windows uses PID-targeted taskkill, Linux uses the detached process group;
other OS tree inspection is explicitly unsupported. Child signals map to exit 3.

A stop during upload creates STOP and STOP_SET; CrossCast notices it at its next
check. It can be pre-press stopped or post-press in doubt. Manually removing the
file does not permit repeat/resume: the owner's STOP_CLEARED record is required.
At close, the operator restores the two prior settings with configure --enabled
false; unrelated settings are preserved, and changed registrations refuse rather
than overwrite an intervening operator change. The original settings backup is
retained privately. Reveal requires a marked closing sign-off after the last
STEP and a matching sealed plan digest. None of these steps runs automatically.


Before arming, the wrapper also checks the fault module and its four audited
call sites against `templates/round2/crosscast-fault-support.json` (CrossCast
f2eb520). Only the normal Windows venv `Scripts/vpub.exe` plus `Lib/site-packages`
layout is recognized. Missing, changed or unrecognized support gives
`fault_switch_unavailable`, exit 5, with a STEP_REFUSED and no FAULT/child.
A later code revision must re-audit these files and update the manifest; a version
label or config flag alone does not establish switch support. The tests' synthetic
executable uses a function-level probe stand-in, unavailable as a CLI option;
separate tests exercise fingerprint mismatch and the production refusal path.
Linux process-group code is implemented but this Windows validation does not
claim Linux acceptance. No real installed executable or real settings were read.
