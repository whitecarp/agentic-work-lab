# Round-two instruments

These tools record one-off, approved study jobs. They do not grant permission
to act on an account. No scheduler, network client or fault injector is installed
by importing these modules. C15 provides opt-in planned fault arming in admitted steps.

Use Node 20 or newer. All commands run from the Lab checkout. Set ROUND2_HOME to
a private folder outside every Git checkout, shared by the operator and checker.
begin prints its resolved real path so the two apps can compare it. Use separate
private homes and site journals for owner and tester.

Records are in data/round2/SITE/journal.jsonl. The first protocol seal pins the resolved journal path in the private home; every later command must use that path, even from another checkout. The companion .head file
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

The owner supplies the handoff and publish approval in their own words on standard input. Each delete needs its own one_delete approval for the named post and platform.

~~~text
node scripts/round2/journal.mjs protocol-seal --site owner --phase rehearsal --protocol PROTOCOL_FILE --lab-commit LAB_COMMIT_40_HEX --crosscast-commit CROSSCAST_COMMIT_40_HEX --slot gemini
node scripts/round2/journal.mjs handoff --site owner --to gemini --by owner --slot gemini
node scripts/round2/journal.mjs go-ahead --site owner --job rehearsal-1 --scope one_write --count 1 --by owner --slot gemini
node scripts/round2/gate-run.mjs begin --site owner --slot gemini --launcher manual
node scripts/round2/gate-run.mjs step --site owner --slot gemini --run RUN --job rehearsal-1 --role publish --platform youtube -- STAND_IN_COMMAND
node scripts/round2/gate-run.mjs end --site owner --slot gemini --run RUN
node scripts/round2/journal.mjs verify data/round2/owner/journal.jsonl --slot gemini
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
- run.lock is durable session state until an explicit end. A new shell does not
  imply a finished session. During a step wrapper/child identities guard the work.
  A second begin returns already_running and open_run. End that session before
  handoff. If run.lock is lost, its admitted operator can end the run by ID
  from the journal. Recovery records RUN_END as interrupted.
- The short exclusive .lock / operation.guard files are fail-closed. A process
  dying inside a critical write can leave one behind: preserve the evidence and
  request review. No automatic lock deletion or receipt reset is provided.
- Missing approval, a changed operator, an active step, STOP, an unadmitted job,
  or a standing policy limit refuses with exit 5 and a fixed reason word.
- Test publishes are only on declared YouTube/Douyin jobs. Four first test-post publishes per
  platform per 24 hours, 20-minute spacing between those first publishes (and
  after a recorded outside upload), and a
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
node scripts/round2/journal.mjs clear-guard --site owner --by owner --slot gemini
~~~

For a test post, each platform needs its own settlement after its last possible
write: confirmed deletion, a counting absent-post look when nothing was posted,
a manual_step note, or a counting absent-delete look after pending deletion.
Mixed settlements close as done; standing read jobs never close. Abandoned reads the person's words from stdin
and stores only their digest. Other job classes use done or abandoned. The operator
is responsible for the required real-release observation windows before closing.

listed_test_posts units cover publish, repeat_same, resume, verify, edit and
changed_plan only. Other writes require one_write units. A delete instead requires one_delete
units for that exact job and platform (--platform is mandatory on its grant).
Generic write units never authorize deletion and read-backs do not consume delete units.
Eligible writes consume listed units first. Old unscoped pending
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
node scripts/round2/daily.mjs --site owner --slot gemini
node scripts/round2/seal.mjs --site owner --day 2026-11-12 --slot gemini
node scripts/round2/export.mjs --site owner --out PUBLIC_FILE --privacy --deny PRIVATE_DENY_FILE --crosscast CROSSCAST_AUDIT_FILE --slot gemini
node scripts/round2/reconcile.mjs --site owner --crosscast CROSSCAST_AUDIT_FILE --slot gemini
node scripts/round2/daily.mjs --site owner --apply --slot gemini
node scripts/round2/journal.mjs sign-off --site owner --day 2026-11-12 --by owner --answer yes --slot gemini
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
wrapper does not decode an opaque receipt key to prove its identity. A test job closes as done when every platform has a recorded settlement.
Section "C37 rules and exact offline drill recipe" describes the four settlements.

The gate admits a write job in a new session only while an unspent write unit
exists. Later read/local steps must therefore run in an admitted session, or
after the next real grant. They do not consume an additional write unit. Use
`2 + 3P` for a day's test-post allowance and `1 + 3P` for real releases (P platforms),
plus separately approved edits/deletes under the current site's rules. C20's
generated list must derive its counts from the current approved counts file.

A real release outside the sealed list is recorded separately; it never edits
jobs.json. Pipe the account owner's actual words into:

~~~text
node scripts/round2/journal.mjs real-release --site owner --job release-EXAMPLE --platform youtube --due-at 2026-11-20T14:00:00Z --by owner --slot gemini
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

For a planned fault the site operator runs the setup between sessions with the
owner's words on standard input. The command checks the current configuration,
backs up the previous settings, registers the study stop and records FAULT_SETUP.

~~~text
node scripts/round2/fault-setup.mjs configure --site owner --enabled true --by owner --slot gemini
~~~

Omitting `configure` returns `configure_required`. Arming checks the recorded
setup and the current settings. Every currently unarmed child strips all inherited VPUB_FAULT variants;
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
Use reconcile --privacy for the separate comparison copy; its cutoff is UTC day
end and uncertain-case gaps are rounded up to whole minutes.
The tester's operator requests CrossCast exports bounded by whole UTC days.


## C15: fault setup, private planned list and cleanup

No real-account action is authorized by these commands or by this code delivery.
Gemini (owner site) or the tester's operator performs setup only after that
account owner's specific approval, before a session starts. The configure
command records the supplied owner's words, saves only the two old settings
privately, registers ROUND2_HOME/STOP and enables fault_injection. Record-only
mode refuses configure_required and cannot enable arming. A temporary job handoff
holder cannot configure; the configured site operator must do it outside a session.

~~~text
node scripts/round2/fault-setup.mjs configure --site owner --enabled true --by owner --slot gemini
node scripts/round2/plan-runs.mjs make --counts COUNTS_JSON --site owner --due-from 2026-11-19T00:00:00Z --first-short 1 --first-long 1 --slot gemini
node scripts/round2/plan-runs.mjs check --sealed SHA256 --slot gemini
node scripts/round2/fault-setup.mjs configure --site owner --enabled false --by owner --slot gemini
node scripts/round2/journal.mjs sign-off --site owner --day 2026-12-01 --answer yes --closing --slot gemini
node scripts/round2/plan-runs.mjs reveal --site owner --slot gemini
~~~

Setup/cleanup need the owner's words on stdin; do not paste these examples as
approval. Actual first clip numbers follow shakedown usage. `make` creates no
approval and starts no command. It requires an empty list destination; a partial
write is preserved for inspection. The private 32-byte seed plus counts and
explicit arguments determine the bytes. SHA-256 counter draws use rejection
sampling; timing pairs stay adjacent. Variant selection and clip allocation
are checked, both sites use separate pools, and jobs.json reveals only whether a publish fault is assigned, never its kind or timing. It includes a standing read job per site and future-dated not_due drills.
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

Stage matching reads browser_progress JSON from stderr, requires the armed platform, and compares whole stage words; bare text and unrelated JSON never trigger. The edit kill waits for
reading back after pressing. Command-only termination targets only the command;
a detached worker may survive. Tree termination inspects the owned descendants,
ends the tree and checks each observed PID's creation identity afterward. It
never selects by process name. FAULT_RESULT separately records trigger, firing,
errors and per-PID end observations; no firing or all-ended claim is invented.
Windows command-only faults use Node's retained child handle. Tree faults use a
persistent PowerShell helper holding verified Windows process handles from child
start; there is no taskkill /T or PID-only termination. Older orphan descendants
are excluded by birth time. All owned handles are attempted even when a child
exits during termination; final handle observations determine success. Other OS
tree control is unsupported. Child signals map to exit 3.

A stop during upload creates STOP and STOP_SET; CrossCast notices it at its next
check. It can be pre-press stopped or post-press in doubt. Manually removing the
file does not permit repeat/resume: the owner's STOP_CLEARED record is required.
At close, the operator restores the two prior settings with configure --enabled
false; unrelated settings are preserved, and changed registrations refuse rather
than overwrite an intervening operator change. The original settings backup is
retained privately. Reveal requires a marked closing sign-off after the last
STEP and a matching sealed plan digest. None of these steps runs automatically.


Before arming, the wrapper also checks the fault module, its four audited
call sites and progress JSON formatter against `templates/round2/crosscast-fault-support.json` (CrossCast
f2eb520). Only the normal Windows venv `Scripts/vpub.exe` plus `Lib/site-packages`
layout is recognized. Missing, changed or unrecognized support gives
`fault_switch_unavailable`, exit 5, with a STEP_REFUSED and no FAULT/child.
A later code revision must re-audit these files and update the manifest; a version
label or config flag alone does not establish switch support. The tests' synthetic
executable uses a function-level probe stand-in, unavailable as a CLI option;
separate tests exercise fingerprint mismatch and the production refusal path.
No Linux tree-control acceptance is claimed. No account settings are read by the offline tests.

Fixed review cutoffs require an explicit UTC Z or offset; timezone-naive --as-of input is refused.

The named operator configures the fault switch once. An existing different
external stop file yields `external_stop_already_registered` and the operator
reports that to the owner. Cleanup keeps the original settings backup, so any
second setup needs review. Clearing a scheduled or personal stop requires that
site person's words on stdin. An agent can clear its own drill or refusal stop.

C36 records the presence of a tool reply and the started state on new steps.
A cutoff publish, edit or delete opens an unknown case at that step. A confirmed
read or a person's recorded look must follow the last possible press for the
same job, platform and operation. The stricter count is retained.

The interruption record carries requested-delay-to-action and observed-gone
delays separately, fixed failure reason, and owned identities. Command-only
records its starter only, without delaying the signal for a process scan; the
worker may keep running and STEP may precede its completion. Unknown controller
results keep the STEP. Tool-switch faults have neither measured delay.

For the tester share, run the Lab export with both --privacy and --crosscast.
Share that derived file and the separately generated privacy comparison. Its crosscast section rebases times per command,
only. All record-level publication/deletion/edit/observation offsets are null.
Keep the input tool export on site. Review results
requiring absolute times there. The counted-phase seal must contain the digest
of the plan to arm from it. After the closing sign-off, reveal writes both the
sealed list and its verified 32-byte seed. A legacy list without a seed commitment
cannot certify a recovered seed.

Precise intervals on a command spanning UTC midnight can narrow its clock time
when paired with the Lab STEP date. The tester copy withholds those intervals
and the corresponding Lab STEP duration. Each person's on-site analysis retains
the original durations. Process creation identities are keyed in the share.

## C37 rules and exact offline drill recipe

Every CLI requires `--slot` naming who typed it; `scheduler` is not a typed slot.
`--by` records whose words were supplied and does not replace that slot. Every
example above must also use the same absolute `--journal` when the pinned journal
is not at the checkout default. On a new checkout, an implicit different journal
refuses `journal_place_mismatch`. Begin respects recorded stops even if STOP was
removed; an agent may clear its own stop while other setters' stops remain.

STEP carries the state, previous state, code word and press marker for each
platform. Under Annex A 1.9 / C40, the code comes from the first string in
the answer's code, its own error.code, then the envelope's error.code, using
the existing compact-word filter. A build-stamp object is skipped. A delete
uses its own delete_attempted flag, never the earlier upload's flag:
DELETE_PENDING with false opens no case; true or absent still opens one.
DELETED_VERIFIED with false is confirmed already gone without a delete press.
An unfamiliar state is recorded as UNLISTED. For a publish that
reports WAITING_FOR_USER or PREPARATION_INCOMPLETE, a true or missing marker
opens a case unless the tool record proves no press. A false marker settles
the no-press question for these two states. A mixture of settled and unsent
platforms has the daily summary partial. An exit-2 write with an unusable
matched record is listed under error_steps_unclassified in the reconcile. Errors after contact, exit 3 and cutoffs in all
press-capable roles open cases unless complete evidence proves no press. Null
delete counters unchanged on both sides are acceptable; missing final-press
counters are not proof. Edit/delete cutoffs use their own markers, not an old
upload flag. A settling read-back must match the reference when both sides have
one and must have been saved. A person's look counts only after its claimed
10–1440 minutes actually elapsed and never if unclear. All possible subsequent
presses move the required look forward. Capture failure preserves a started STEP.

Delete approval example (person's actual words on stdin, not this sample):

~~~text
node scripts/round2/journal.mjs go-ahead --site owner --slot gemini --job JOB --scope one_delete --platform youtube --count 2 --by owner
node scripts/round2/journal.mjs browser-faults --site owner --slot gemini --platform youtube --enabled false --by owner
node scripts/round2/plan-runs.mjs verify --slot gemini --counts COUNTS_JSON
node scripts/round2/reconcile.mjs --site owner --slot gemini --crosscast LOCAL_TOOL_EXPORT --privacy
~~~

The browser-faults record disables tree/browser-ending kinds on that platform;
each skipped assignment gets FAULT_SKIPPED. It grants no fault authority. Do not
arm on any account before the independent C37 review and separate account gates.

Phase seals move only rehearsal → 2a → 2b, with the controlled further seals
described below. The 2b
seal must name `--plan ROUND2_HOME/planned-runs.json`. Plan hashes cover raw bytes.
Private counts-source.json, seed, generator arguments and jobs bytes must exactly
reproduce the list; both 2b sealing and reveal perform that check. Reveal also
requires a site-person closing **yes** after the last STEP and no open session.
The tester sends two files after closing, the derived privacy export and its
privacy comparison. The owner publishes the tester list and seed from the
owner's site. The protocol seal retains the public plan_sha256 so the recipient
can compare the list digest. Default-key every
64-hex digest except explicitly allowlisted public source-file fields; sessions
are null and exported event numbering starts at 1. STEP duration is withheld
when its prior gate and STEP cross UTC midnight, even without tool events.

C25's separate pre-test installation package must also carry the tester's own
private plan-seed and counts-source.json alongside its generated list and jobs;
they are needed for on-site verification before the seal and later reveal.
This is a packaging requirement for C25 on November 18, not a package sent by C37.

For the six drills use fresh offline homes, explicit empty VPUB_HOME and
BILI_CLI_HOME bindings, an initial rehearsal seal and a whole-site handoff to
Gemini. Have a separate approved standing read job due alongside the drill.
Use `COMMON = --site owner --slot gemini --journal ABSOLUTE_JOURNAL` in every
command below; replace COMMON with those flags (it is notation, not a CLI flag).
Give a local drill one `read_only` go-ahead except for not_authorized. The
generator dates not_due to 2100-01-01, not to the general due time.

| Drill | Setup and commands | Pass condition and cleanup |
|---|---|---|
| not_owner | `gate-run.mjs begin COMMON --slot-id DRILL`, substituting only `--slot codex` | `skip`, `not_owner`, no due jobs; no handoff |
| already_running | `gate-run.mjs begin COMMON` as setup, then `gate-run.mjs begin COMMON --slot-id DRILL` in another shell | second returns `already_running`; `end COMMON --run FIRST_RUN` |
| stop_flag | `journal.mjs stop COMMON`; `gate-run.mjs begin COMMON --slot-id DRILL` | `stop_flag`; `journal.mjs clear-stop COMMON` clears only that agent's stop |
| completed | operator runs `gate-run.mjs close-job COMMON --job DRILL --reason done`; then `gate-run.mjs begin COMMON --slot-id DRILL` | DRILL absent from due; end any admitted session |
| not_due | leave the generated far-future due date; `gate-run.mjs begin COMMON --slot-id DRILL` | DRILL absent from due; end any admitted session |
| not_authorized | do not grant this drill approval; `gate-run.mjs begin COMMON --slot-id DRILL` | DRILL absent from due; end any admitted session |

The last three may correctly admit the standing job or another approved due job.
Judge the drill's own job in the due list, not a blanket no_work_due response.
Close drills only as the named operator. Never close the standing read job; it
is used for session-start checks and the day's export without granting writes.

C39 / Annex A 1.8: DRILL is the actual drill job id. All starts that test the
drill carry it; the already-running setup start is not itself a drill attempt.
The comparison now exports gate_checks (decision record numbers, unchanged-gate
replay and agreement lists) and publish_checks (first test publish per job and
platform, four-in-24-hours and 20-minute-spacing violations). Exact times stay
on site; these checks contain none and also travel with --privacy. The tester's
copy preserves crosscast.builds[].source_digest as published code evidence;
capture digests and unknown nested fields remain keyed.


### Annex A 1.11 controls (C44)

A counted `protocol-seal --phase 2b` may be followed by at most two further
counted seals. Every further seal requires `--by owner` (or `tester` at that
site) and that account owner's words on stdin. It must retain the first
counted seal's exact list digest. Earlier phases cannot follow a counted seal.

`gate-run.mjs end-failed-session --site owner --slot codex --run SESSION_ID
--by owner --journal JOURNAL` records an interrupted session on the owner's
piped words. The checking agent uses its own slot, not the operator's. An
active child or unfinished live wrapper refuses recovery. This does not stop
processes, hand over the role, close a job, clear STOP, or return spent credit.

`observe` prints `look.status`, `look.counts` and whether the claimed minute
was reached. A future claimed minute is refused before recording; use whole
minutes rounded down from the operation's last started press-capable STEP
record. Early, unclear and out-of-window observations can still record a fact
but do not count as ground truth. The second-look window is 1,200–2,160 minutes
when the first counting post look was absent while the record was in doubt.

`violation --platform PLATFORM` optionally scopes the rule break. No platform
means site-wide scope for analysis. An explicitly busy/browser_busy attempt
with a known non-fired fault may arm again; its first result says
`busy_not_reached`. Missing results do not authorize rearming; additional
publish-refusal cases and the C48 corrections are specified below.
After a safety-stop note, a first test publish needs both 48 elapsed hours and
an owner go-ahead for that job recorded after those hours, even with old credit.

Checking-agent drill starts always carry `--slot-id DRILL_JOB_ID`, including
the wrong-operator start and both refused starts in a handoff drill.


C42: retain the unsealed practice list under an immutable, separate local
`practice/phase-2a/` archive when preparing the real list. Practice ids must not
collide with counted ids. After the close, pass that archived list to
`reconcile.mjs --practice-plan ARCHIVED_PRACTICE_LIST` before the privacy copy
and to the analysis manifest as `practice_plan`. This option reads only the
named list and never replaces jobs or a seal. C21's planned phase-2a seal has
no `--plan`; it therefore records a null list digest. No archive or real-list
replacement is performed by this analysis change.


## C47 list and refusal updates

New lists use generator version 2. In the unamended counts, exactly two of the owner's three YouTube C2 arms and one of the tester's two use 10000 ms from the post-click confirmation-wait stage. Other delays stay 0/500/2000 ms. The controller accepts the long delay only for YouTube end_tree_after_press. It still records a natural finish before the request as not fired. Code inspection puts the stage inside submit, before Studio's minimum 12-second settling wait; the real page timing remains for shakedown evidence.

For an unsealed practice list use generate/make with phase 2a (`--phase 2a` on make) and practice counts normally containing two YouTube C2 arms. Its allocation is one early, one late. The exported assignC2Delays(runs, site, draw, {practice:true}) applies the same check to a separately constructed practice list before it is saved. C48 also permits amended counts with no YouTube C2: none fires late. With one remaining C2 it fires late; with two, one fires early and one late; with the owner's original three, two fire late. Counts above the original ceiling (owner three, tester/practice two) and unsupported initial delays still refuse. Converting C2 to C1 belongs to the approved pre-freeze amendment, not the generator. This change generates no study list or fault on its own. Existing version-1 and valid version-2 lists remain byte-reproducible using their saved generator metadata; never rewrite an existing sealed list.

Each new test-post row and its operator job carry the same generated title, including site and clip number; long clips include long-. assignTestTitles refuses duplicates within the site's list. Use the listed title in the publish plan. Existing unused clip allocation and phase archive rules still apply.

A publish fault can be armed again when its own platform explicitly answered NOT_STARTED with no previous state, at any exit code (including browser_busy at exit 4), or when the whole publish was refused with exit 2 and no state or NOT_STARTED on every platform. Edits and deletes retain rearming after busy/browser_busy. All paths require that platform's answer, no true press flag, and an explicit prior fault result of not triggered, not fired and not failed. The controller and selector use the same rule: busy_not_reached for those two lock answers, publish_refused_not_reached for other qualifying publish refusals. Missing results and ambiguous publish answers do not qualify. The upload-start helper's whole-command rule is unchanged. Rearming does not authorize a retry or grant another write: the existing wait, second-busy stop and owner go-ahead rules still apply.

For protocol 9 H's manual Douyin removal, record NOTE manual_step with the exact job and platform, then the deletion look with elapsed minutes from that note, then JOB_END with removed_by_hand. A generic account cleanup note must omit the job. The account owner performs removal; the tools only record it.

## C49 draft practice counts (phase 2a)

`templates/round2/practice-counts-phase2a-v1.json` is the public draft for
Claude's protocol review. It specifies 20 private short test uploads, ten on
YouTube and ten on Douyin, six refusal runs, six gate drills and one handoff.
There is no timing pair or public release slot. Each platform tries all eight
fault kinds once, except YouTube C2 twice (one early, one late). An edit fault
and a delete fault share one otherwise clean publish per platform, separate
from the clean edit-and-delete post. Two additional plain Douyin posts are
reserved for the ordinary private-delete checks; YouTube has a separate
wrong-delete-code-first post. The Douyin delete fault and ordinary deletes
remain conditional on protocol 9 H's first way and each delete's own approval.
Under the second way, record the owner's manual removal and look; do not run
a right-code delete or claim that its planned fault was tried.

At C9, after review, use this counts file with `plan-runs.mjs make --site owner
--phase 2a --counts PRACTICE_COUNTS_JSON --due-from APPROVED_START_UTC --slot
codex` in the intended private practice home. Keep its fresh throwaway seed
private and report only the plan digest and totals. This draft and its tests
make no actual study list, seal, approval, timer or account action. Creation
is exclusive; an existing list is never overwritten. The practice counts
cannot generate a tester, version-1 or phase-2b list.

Practice jobs use `owner-practice-NNN` (plus a standing job), distinct from
counted jobs. Archive the practice files and seed immutably at C9/C21's phase
transition; retain the actual used-clip inventory. Twenty planned practice
shorts use numbers 1–20 if none was used earlier; a counted owner list then
starts at short 21 and ends at 52, leaving eight of the 60 shorts unused.
Use the actual retired inventory, including failed-refusal uploads, to choose
the start, not this illustrative number. Refusal clips stay in their own
61–70 pool; long clips are untouched by practice.

Ten uploads per platform fit 14–18 November in principle, for example 4, 3,
3 on the 14th, 15th and 16th, with each day's illustrative batch starting more
than 24 hours after the previous day's last start, and at least 20 minutes
between starts within a batch. This leaves the 17th and 18th for later looks
and closings. This is a capacity check, not a schedule:
the rolling four-in-24h cap, spacing after other known uploads, owner looks,
48-hour safety holds and unfinished sequences still decide what can run.
Never compress or repeat runs to fill the draft. One Bilibili delete dry run
is recommended only on an existing owner-approved real-release receipt during
its read-back session: no new public post and no confirmation code. With no
eligible receipt/session, record not attempted.
