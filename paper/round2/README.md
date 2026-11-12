# Round-two export analysis (C12 / C45, Annex A 1.12)

This is an offline reader. It neither loads a live data folder nor invokes a
platform command. The script has no network, process-start, account or clock
dependency. It uses the unchanged gate decision function and the pure C38
reconciliation calculation, copied without its filesystem adapter into
reconcile-export.mjs. Both paths share press-evidence and timing checks. A parity test compares
its result with the offline calculation.

## Invocation

Paths inside the input manifest are relative to that manifest, not the shell.
The manifest names only exports and the revealed list/seed. Do not point it at
ROUND2_HOME, VPUB_HOME, BILI_CLI_HOME or a sign-in folder.

    node paper/reproduce_round2_tables.mjs --inputs <export-manifest.json>
    node paper/reproduce_round2_tables.mjs --inputs <export-manifest.json> --write
    node paper/reproduce_round2_tables.mjs --inputs <export-manifest.json> --check

Default input is paper/round2-inputs.json. No real input manifest or measured
summary is created before the study closes. Default output, and the only default
write, is paper/round2_metrics_summary.json. --summary selects a separate output
for synthetic tests. --as-of overrides every site's fixed cutoff. Normally put
each site's closing sign-off time in its manifest row instead. The tester uses
the UTC date of its closing sign-off, exactly as its on-site comparison does.

No mode flag prints JSON without writing. --write prints and saves that JSON.
--check prints round2_summary_matches and exits 0 only when the saved LF text is
identical. Differences, missing files, invalid inputs and unknown/duplicate or
conflicting options exit 1. --check never writes. A verdict containing failures
is evidence, not a parser error; --check compares that verdict too.

Example manifest (paths are placeholders, not invitations to open live folders):

    {
      "schema": "round2-analysis-manifest/1",
      "checklist": "checklist.json",
      "sites": [
        {
          "site": "owner",
          "journal": "owner-export.json",
          "crosscast": "owner-windowed-tool-export.json",
          "reconcile": "owner-comparison.json",
          "plan": "owner-revealed-planned-runs.json",
          "seed": "owner-revealed-plan-seed",
          "counts": "owner-counts-source.json",
          "as_of": "2026-11-29T23:59:00.000Z"
        },
        {
          "site": "tester",
          "journal": "tester-share.json",
          "reconcile": "tester-comparison.json",
          "plan": "tester-revealed-planned-runs.json",
          "seed": "tester-revealed-plan-seed",
          "counts": "tester-counts-source.json",
          "as_of": "2026-11-29"
        }
      ]
    }

The tester's tool copy is read only from journal.crosscast. No separate original
tool export is requested. The owner supplies both revealed plans and their seeds
at close. Seeds are 32-byte files. Their digests must agree with the lists; lists
must match the freeze seal. Supplying counts also reruns the deterministic
generator and checks the entire list, not only its totals. Counts are optional
because Annex A's required input list does not include the counts source.
A windowed tool export with nonempty since is mandatory. Owner command times
must lie inside that window. Every input must have a yes closing sign-off at
the specified cutoff and the comparison must record that same cutoff/count.
A job spanning two phase seals is refused instead of combining 2a and 2b.

checklist-v1.json supplies the 19 cells of protocol section 13. An input checklist
may add real-edit cells or record a reason/unsupported_by_design decision.
These fields describe scope, never replace a measured outcome. Absent reasons
print no_recorded_reason.

## Output and field mapping

| Annex | Output | Source and meaning |
|---|---|---|
| 1.1–1.4 | gate.decisions/replay/replay_rows/per_job_verdicts/scheduled_slots | planRun replay: exact owner export recomputed and compared; tester uses on-site attestation; missing rows not replayable; scheduled_slots is not_run |
| 1.5 | gate.drills/drill_bound | All decisions with GATE_DECISION.slot_id equal to the drill id, including starts after JOB_END; no named start is unmeasured |
| 1.6 | inputs[].binding_statuses/outside_commands/outside_share; must_be_zero.writes_without_admission | Shared command awl_run_id only; unknown external commands remain site-level because their Lab phase is not known |
| 1.7–1.9 | gate.handoffs/refused_other_slots/go_aheads/messages/wrapper_refusals | Rebuild operator succession; replay scope and consumption from prior grants and observed steps |
| 2 | plan_binding | Changed-plan contact, stop refusal while stop active, fixed binomial bound |
| 3.1–3.2 | submission.attempts | Job/platform/tool-ref, per-platform answer; marker count and independent look; union of duplicate mechanisms |
| 3.3 | submission.faults | Planned FAULT plus FAULT_RESULT; failed always unknown; listed/request/gone times; switches have no delay |
| 3.4–3.6 | submission.repeats/fault_duplicate_bound/repeat_bound/computed_baselines; pooled_fault_bounds | B/C repeats; draft before second marker; one-sided bounds; alternative policies computed, never run |
| 4 | in_doubt | Shared C38/C40 rules; operation/platform cases, possible unclassified exit-2 errors, strict closure, durations, observed outcomes, stops by verbatim code |
| 5 | readback | Real-release confirmation, contradictions, edit target proof, per-delete results/repeats/comparators, measured wrong-delete bound; 5.4 separates found_already_gone and failed_before_press |
| 6 | checklist | Both sites combined per frozen commit; per-site counts; partial temporal order, never invented within-day order |
| 7 | timing and timing_pairs | Stage intervals, command duration, matched release pairs, human records by day; no significance test |
| 8.1/8.6/8.10 | protocol.runs | Every planned job, recorded/excluded/unknown nine-rule outcomes, JOB_END and closing-sequence evidence; supplemental REAL_RELEASE separate |
| 8.2/8.3/8.7 | protocol.violations/notes/model_period_runs/after_fault | Phase-reset model periods and recorded actions after the faulted STEP |
| 8.4/8.9 | inputs[].journal/tool/losses; assumptions.A3 | Exact export chain and seal recomputation; tester commitments and tool integrity attested; dropped-event counts are lower bounds |
| 8.5 | must_be_zero.test_posts_public/test_daily_cap/test_spacing; protocol.cap_observations | First publish per test job/platform; no repeat consumes another upload; source shown on each on-site or recomputed check |
| 8.8 | protocol.runs[].outside_upload; counted_without_outside_upload | Upload-phase window only; unknown timing omitted from the clean-window subset |
| 9 | assumptions | A1 outside writes/extra copies, A2 recoverable marker evidence, A3 integrity limitation, A4 second-marker draft proof |

Each phase/site/platform/code-stamp stratum has all_recorded, counted and
counted_without_outside_upload. A platform-null row contains the site's aggregate
and gate figures; do not add it to platform rows. Fault bounds pooled over sites
are separately keyed by phase/commit. Other platforms never supply evidence for
an attempt's answer. Lab refs and tool refs have different secrets and are never
joined. STEP.result is not used as a per-platform outcome.

The outcome priority table is printed in the summary. Source helpers are frozen
by the Lab commit alongside the entry script. Their hashes should travel with
the freeze review, not be replaced after results are observed.

C40 selects the first string from the answer's code, its own error.code, then
the envelope's error.code, retaining the existing compact-code sanitization.
A worker build-stamp object never masks the actual error word. The operation's
press flag is final_submit_attempted for posts and delete_attempted for deletes;
an absent delete flag is null, never borrowed from the earlier upload.
For 5.4, pressed is that answer flag and press_count is the independent count
of intact deletion audit markers. DELETE_PENDING with a false flag is
failed_before_press, not pending and not an in-doubt case. DELETED_VERIFIED
with a false flag is found_already_gone and still confirmed gone. True or
absent pending-delete flags still open a case. The existing records are never
rewritten; pre-C40 missing flags cannot be reconstructed from a raw receipt.

## C41 figure corrections (Annex A 1.9)

* **4.4 / 5.2:** every in-doubt case uses its counting look for the outcome,
  regardless of whether a person or a tool read-back closed it. An absent look
  after a confirming read-back contributes to both nothing-posted / false-alarm
  counts and contradicted confirmations. Existing press-evidence denominators
  and the C4 click adjustment remain unchanged.
* **7.3:** `observation_checks` in the on-site comparison carries one row per
  recorded look, with record number, job, platform, operation and classification.
  Stated minutes below 10 are early, 10 through 60 on time, over 60 through
  1,440 late, and over 1,440 outside the window. Unclear is a separate primary
  status; `timing_status` also preserves its time category. Each look counts
  once, including repeated, early, premature and no-obligation looks. These
  workload rows never change the look chosen as ground truth. The comparison
  exports categories, not minutes or clock times. The owner recomputes and
  compares; a dates-only site uses the supplied per-record attestation, with
  provenance. Missing, duplicate or inconsistent attestations remain unknown.
* **8.10:** `protocol.real_release_24h` prints `not_applicable` for the tester
  with no real releases. Exact-owner releases retain the existing 24-hour
  closure check and expose per-platform results. An unexpected dates-only
  real release stays unknown, not exempt or verified.

## Evidence that this export cannot supply

These are explicit null/unknown/unmeasured outcomes, not zero failures.

* C39 keeps only crosscast.builds[].source_digest plain in the tester's tool
  copy, in addition to the existing public source digests. Both sites compare
  it with the freeze's crosscast_digest. Capture and unknown nested digests
  remain keyed. Old copies with a keyed source digest fail the stamp match.
* Date-only gate replay and first-publish spacing/cap figures use the supplied
  comparison's gate_checks and publish_checks, computed before redaction.
  Every result names its record number and provenance; the owner recomputes
  and compares both fields. Missing/duplicate rows remain unknown, not zero.
  Neither field exposes exact clock times. Outside-upload timing still cannot
  be located after minutes_ago was removed and remains unknown where needed.
* Across sites on the same UTC day, the tester's place in a total order is not
  known. The checklist examines every try that could be among the last three.
  It confirms that condition only when every such candidate was confirmed.
* JOB_END ordinarily has no session id. Every drill start must use
  begin --slot-id <drill-job-id>, including the completed kind which closes
  first. All matching decisions anywhere in its phase are judged. Without an explicit recorded
  association the analysis cannot distinguish an intended drill start from a
  normal start that happened to list that drill. It does not choose by guess.
* An export can establish only observed approval consumption. A reservation
  whose process died before STEP may have consumed an additional unit on-site;
  that missing reservation is not reconstructed from an exported journal.
  Any unspent-unit count derived from those steps is therefore an upper limit.
* A shared attempt ref does not prove the platform post id stayed the same.
  Confirmed edit read-back is the stated basis; the existing export has only
  id_present, and the edit-start snapshot omits even that bit. The latest
  preceding reading is labelled separately. No actual id or keyed id is compared.
  A delete without a speaking study-post comparator is unmeasured.
* Records of a wrapper-ended command are incomplete regardless of reported zero
  loss. Missing marker evidence is never zero. Bilibili has no marker denominator.

C39 implements the on-site checks and digest exception decided in Annex A 1.8.
It does not change gate decisions, account actions, raw records or the protocol.
Other missing figures stay explicit for Claude's independent reading.

## Validation

    node --test tests/round2-analysis.test.mjs
    node --test tests/*.test.mjs tests/round2/

Fixtures are synthetic and explicitly labelled. They plant every must-be-zero
failure, test mixed platform answers, source/list/cutoff checks, privacy
attestations, C4 draft handling, losses, phase separation, statistics and CLI
read-only/determinism behavior. No fixture is a study result. No round-one data
or manuscript output is rebuilt.


## C42 / Annex A 1.11 (supersedes earlier counter and look rules)

`--last-day YYYY-MM-DD` (or manifest `last_day`) is the inclusive UTC first-step
cutoff, default 2026-11-29. It is printed in the result. An optional per-site
manifest `practice_plan` supplies phase 2a only; its seal is checked against that
list, never the phase-2b list. Pass the identical archived practice list to the
site's `reconcile.mjs --practice-plan` command. With no practice list, unmatched
jobs and every related record remain in `inputs[].unplanned_records`, unmeasured.
The standing read job is deliberately not called unplanned.

The maximum saved final_presses is read across the receipt, snapshots and every
surviving transition. Incomplete commands keep their mark and remain measurable
when a post-command saved counter is available. A positive second marker always
counts, including in excluded or incomplete runs. A whole command without a
press establishes no press; empty-before/nonempty-after is the expected ledger
merge, while unequal numeric counters produce count_changed_without_press.

Repeat rows are command/attempt pairs. Their alternative proof is the unchanged
known saved counter. First markers are separate from repeated markers, unknown
tries remain in the refusal checklist, and restarted uploading is a failure.
Delete repeats use their own command's press records, not the original flag.
Edit confirmation uses matching Lab record codes when both exist, not equality
of a tool ref. Any public or duplicate look is retained as failure evidence.

The last counting look decides; the absent/in-doubt second-look window is
1,200–2,160 minutes. Missing edit/delete looks do not discard the upload run,
but cannot confirm that operation. A phase-2b failure remains in all_recorded
and beside each bound even if its run is excluded. run_checks travels with the
on-site comparison: all related days' last sign-offs, exact interference window
and the safety-stop/new-grant check, without exact clocks. Mixed registered
frozen versions are excluded as made_on_two_versions, not stamp failures.

New output includes gate.handoff_drills/excessive_go_aheads/safety_stop_restarts,
submission.helper_confirmed_C1, in_doubt.state_changes (figure 4.6), explicit
uncomputable policies, late sign-offs, premature nothing-posted closures and
pooled_secondary_bounds for repeat, changed-plan and measured wrong-delete
figures. No comparator is deliberately kept alive: the 12 November 02:08 CT
correction deletes each post at its own run end. A missing comparator yields
an unmeasured delete, never an assumed clean one.

The full synthetic counts-file round covers both sites (tester dates-only),
34 fault-bound attempts, 100 repeats, 24 changed plans and 60 control drills.
A second variant plants a second marker after a killed command and must fail.
These are offline regression fixtures, not recorded study results.


## C45 and Annex A 1.12

Figure 4.6 lists refusals and unsettled responses. It counts a recorded failure
or a reversal of the press flag before settlement. The first fault bound includes
both counted and all-recorded denominators in each site and platform row.
The historical flat fields in those rows describe all recorded runs.
Phase 2b determines the verdict. Other phases' gaps appear in
`verdict.shakedown_gaps`. Export-wide integrity exceptions still apply to
the evidence they cover. Timing pairs report the observed gap in minutes
from the last upload-phase step of the first release to the second publish
start. Completion requires 20 through 60 minutes, inclusive.

The C1 helper-confirmation figure awaits the first C43 record.
