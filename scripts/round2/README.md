# Round-two journal and gate (Modules 1 and 2)

These tools record one-off, approved study jobs. They do not grant permission
to act on an account. No scheduler, network client or fault injector is installed
by these modules. Module 5 fault arming is a later task.

Use Node 20 or newer. All commands run from the Lab checkout. Set ROUND2_HOME to
a private folder outside every Git checkout, shared by the operator and checker.
begin prints its resolved real path so the two apps can compare it. Use separate
private homes and site journals for owner and tester.

Public records are in data/round2/SITE/journal.jsonl. The companion .head file
retains the last count and digest and detects a removed final record. External
seals/anchors remain necessary against an actor rewriting the journal and its
head together. Modules 3 and 4 add seals and export. Never publish private homes
or raw captures.

## Offline setup example

The commands below illustrate the interface, not account authorization. Use
stand-ins and empty homes until the study's separate account gates are cleared.
Keep jobs.json under ROUND2_HOME, for example:

~~~json
[{"id":"rehearsal-1","class":"write","due_at":"2026-11-11T12:00:00.000Z","run_class":"test_post","platforms":["youtube"]}]
~~~

The owner supplies their own words to standard input for handoff and go-ahead:

~~~text
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
