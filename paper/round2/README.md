# Round-two export analysis (C12, Annex A 1.7)

This is an offline reader. It neither loads a live data folder nor invokes a
platform command. The script has no network, process-start, account or clock
dependency. It uses the unchanged gate decision function and the pure C38
reconciliation calculation, copied without its filesystem adapter into
reconcile-export.mjs. The accepted site CLI is unchanged. A parity test compares
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
| 1.1–1.4 | gate.decisions/replay/per_job_verdicts/scheduled_slots | GATE_DECISION inputs replayed through planRun; coarsened/legacy inputs not replayable; scheduled_slots is not_run |
| 1.5 | gate.drills/drill_bound | A drill's explicit session or GATE_DECISION.slot_id, expected reason or own job verdict; ambiguous linkage is unmeasured |
| 1.6 | inputs[].binding_statuses/outside_commands/outside_share; must_be_zero.writes_without_admission | Shared command awl_run_id only; unknown external commands remain site-level because their Lab phase is not known |
| 1.7–1.9 | gate.handoffs/refused_other_slots/go_aheads/messages/wrapper_refusals | Rebuild operator succession; replay scope and consumption from prior grants and observed steps |
| 2 | plan_binding | Changed-plan contact, stop refusal while stop active, fixed binomial bound |
| 3.1–3.2 | submission.attempts | Job/platform/tool-ref, per-platform answer; marker count and independent look; union of duplicate mechanisms |
| 3.3 | submission.faults | Planned FAULT plus FAULT_RESULT; failed always unknown; listed/request/gone times; switches have no delay |
| 3.4–3.6 | submission.repeats/fault_duplicate_bound/repeat_bound/computed_baselines; pooled_fault_bounds | B/C repeats; draft before second marker; one-sided bounds; alternative policies computed, never run |
| 4 | in_doubt | Shared C38 rules; operation/platform cases, possible unclassified exit-2 errors, strict closure, durations, observed outcomes, stops by verbatim code |
| 5 | readback | Real-release confirmation, contradictions, edit target proof, per-delete results/repeats/comparators, measured wrong-delete bound |
| 6 | checklist | Both sites combined per frozen commit; per-site counts; partial temporal order, never invented within-day order |
| 7 | timing and timing_pairs | Stage intervals, command duration, matched release pairs, human records by day; no significance test |
| 8.1/8.6/8.10 | protocol.runs | Every planned job, recorded/excluded/unknown nine-rule outcomes, JOB_END and closing-sequence evidence; supplemental REAL_RELEASE separate |
| 8.2/8.3/8.7 | protocol.violations/notes/model_period_runs/after_fault | Phase-reset model periods and recorded actions after the faulted STEP |
| 8.4/8.9 | inputs[].journal/tool/losses; assumptions.A3 | Exact export chain and seal recomputation; tester commitments and tool integrity attested; dropped-event counts are lower bounds |
| 8.5 | must_be_zero.test_posts_public/test_daily_cap/test_spacing; protocol.cap_observations | First publish per test job; no repeat counted as another upload |
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

## Evidence that this export cannot supply

These are explicit null/unknown/unmeasured outcomes, not zero failures.

* The tester's code source_digest is keyed, while the freeze's crosscast_digest
  remains plain. Its commit can be compared, but a full source-digest match
  cannot be proved. If that digest is required by the seal, rule 2 is unknown
  and those runs do not enter the counted safety table. The present site
  reconcile has no attestation for that comparison.
* Date-only records cannot replay exact due times, prove 20-minute spacing or
  rolling 24-hour caps, or locate an outside upload after minutes_ago was removed.
  Look status, case duration and command bindings use the supplied on-site
  comparison. Other missing temporal facts are reported unavailable.
* Across sites on the same UTC day, the tester's place in a total order is not
  known. The checklist examines every try that could be among the last three.
  It confirms that condition only when every such candidate was confirmed.
* JOB_END ordinarily has no session id. A drill may be identified by the
  already-supported begin --slot-id <drill-job-id>. Without an explicit recorded
  association the analysis cannot distinguish an intended drill start from a
  normal start that happened to list that drill. It does not choose by guess.
* An export can establish only observed approval consumption. A reservation
  whose process died before STEP may have consumed an additional unit on-site;
  that missing reservation is not reconstructed from an exported journal.
* Missing edit before/after refs cannot prove "in place". A delete without another
  observed study post as comparator cannot establish no wrong-post deletion.
* Records of a wrapper-ended command are incomplete regardless of reported zero
  loss. Missing marker evidence is never zero. Bilibili has no marker denominator.

The report asks Claude to decide any added attestation needed before those
figures can become measured. This script does not alter accepted C38 account
tools, source readings, privacy policy or protocol rules to manufacture data.

## Validation

    node --test tests/round2-analysis.test.mjs
    node --test tests/*.test.mjs tests/round2/

Fixtures are synthetic and explicitly labelled. They plant every must-be-zero
failure, test mixed platform answers, source/list/cutoff checks, privacy
attestations, C4 draft handling, losses, phase separation, statistics and CLI
read-only/determinism behavior. No fixture is a study result. No round-one data
or manuscript output is rebuilt.
