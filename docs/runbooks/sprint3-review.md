# Sprint 3 Review and AI Writing Cross-Check

Date: 2026-11-21.
Scope: CNPMCS-51, CNPMCS-57, CNPMCS-63, CNPMCS-64 from screenshots and the `ai_toeic_writing.md` file provided by a teammate.
The teammate's file is treated as evidence to verify, not as pre-approved "must-fix" requirements.

Speaking code ACs have been implemented.
AI grading accuracy cannot be accepted yet because the team confirmed no teacher-graded benchmark set exists.
Keep AI scores labeled as practice estimates and keep the internal full-test conversion formula unchanged until calibration data is available.

## Results by Task

| Task | Changes and Evidence | Still Needs Verification |
| --- | --- | --- |
| CNPMCS-51 ADR-002 | Unicode NFKC normalization, lowercasing, punctuation, and whitespace folding. Blocks duplicate questions within Part 3/4 exercises; re-checks all text across Parts and images by asset/SHA-256 when publishing both DIRECT and EXERCISE_BANK tests. `Content` errors pinpoint exact question slots. Tests: `SpeakingTestContentRulesTests`. | Existing bank data on the shared environment; queries have not been run nor has that data been cleaned up yet. Near-duplicate warnings and `publish-check` remain documented in the Phase 2 backlog. |
| CNPMCS-51 ADR-003 | Layout-level provider preserves stream and `AudioContext` across routes; only the provider calls `getUserMedia`. FULL tests require granted permission, a 5-second test recording with at least 1 second of total signal, playback, and confirmation. Includes device selector, distinct error states, permission recovery, unplugged mic detection, and leave-page warning. Beep uses the shared context before question recording starts. Submission or leaving the flow stops the stream. | Physical mic/speakers, Safari/Firefox/mobile, and one-time permissions require UAT. The 0.15/1s threshold is an initial config, not a calibrated acoustic standard. |
| CNPMCS-51 Test Builder | Keeps both test creation modes per user confirmation. Bank supports server-side search, 300 ms debounce, difficulty filter, and 20 items/page; already-selected exercises show lock reasons. Preserves sequence on save and parse; keeps all validation errors; displays server title/code; saving draft keeps builder open; unsaved-changes close warning; delete draft; clone published content into an independent DIRECT draft. Tests cover workspace, API, and parser. | Advanced target UI such as `publish-check`, server-driven warning diagrams, and `usedInTestCount` remain in backlog. |
| CNPMCS-63 Four-Skill UX | Audited L&R flows via spec and existing regression tests covering exam room, question navigation, playlist, review, and history. Fixed Speaking device/builder issues listed above. Separated partial/practice rubric scores from full-test scores in Writing results, library, review, and history. Fixed Speaking notice disclosing that recordings upload to the server. | Full four-skill UI acceptance against real backend/storage and multiple device viewports is still pending. |
| CNPMCS-64 AI | Writing prompt v2 includes per-Part criteria, keyword inflection handling, email requirements, essay development, and defenses against prompt injection, prompt copying, and filler. Blank answers score 0 server-side without calling AI or loading images. Oversized feedback is truncated while preserving Unicode; out-of-range, extra, or missing question scores are still rejected. Response body uses the same timeout as the request. Added offline benchmark tool. | Needs teacher-graded samples, keys/quota, and a team-chosen test environment to measure accuracy, latency, and consistency. Real Azure/Gemini calls not yet executed. |

## Verification of Teammate's File

**Conclusion: partially accurate; cannot confirm the whole document as "optimal ETS-aligned standard".**

| Claim or Proposal | Cross-Check | Decision |
| --- | --- | --- |
| Current formula sums raw scores, Part 1 contributes 15/28; 23/28 maps to 160 | Matches `WritingEvaluationProcessor` prior to changes; this is an internal conversion called an estimate in code and docs. | Keep the formula for FULL tests only, and do not label it as an ETS conversion table. |
| 20/35/45 or 25/35/40 weights and 90/110/120 caps are ETS rules | The document cites no source or calibration data for these numbers. The Handbook describes converting total ratings into a scaled score, without providing these proposed weights or caps. | Do not apply arbitrary score caps. |
| Part 1 with multiple sentences must cap at 1/3 | False: the rubric allows score 2 for multiple sentences if the corresponding criteria are met. | Prompt retains 2/3 eligibility; do not count punctuation to cap at 1. |
| Accept keyword variants, including irregular forms | True. | Delegate linguistic evaluation to the model instead of passing Contains/Regex flags as missing-keyword verdicts. |
| Partial/practice attempts save estimates like full tests | Matches old processor behavior. | New partial attempts save null estimate; per-question raw scores remain saved. API derives raw/max/% and masks legacy partial estimates. Do not auto-backfill shared data. |
| Score 0 is only for blanks; two raw totals both yielding 10 is a formula bug | Inaccurate: 0 also applies to ungradable responses; a 0-200 scale in steps of 10 has only 21 levels, so mapping multiple raw scores to the same scaled band is inevitable. | Do not add a floor of 10 or new penalties based on this argument alone. |
| Schema places summary/score before feedback | Matches old schema. Google supports property ordering; that does not prove the model is forced to reason properly or eliminates bias. | Put per-question feedback before scores/summary; needs A/B benchmarking. Do not ask to output hidden reasoning. |
| Splitting into 2-3 requests cuts latency by 35-45% and eliminates Halo Effect | Unmeasured hypothesis. Higher concurrency can hit quota limits; per-Part retries also need separate checkpoints to retain partial successes. | Do not change the request architecture yet. Measure p50/p95, error rates, and cost first. |
| Text over 1500 chars fails the whole evaluation | Matches old validator behavior. | Safely truncate text before validation instead of retrying solely due to length. |
| `Math.Clamp` out-of-range scores to avoid retries | Could turn malformed responses into plausible-looking scores. | Do not clamp scores; preserve per-Part range validation. |
| `netWordCount`/`uniqueWordRatio`, template detection | Useful for research, but n-grams and word ratios are not psychometric verdicts and can penalize legitimate prompt quotations. | Prompt instructs evaluating relevance and development; no score-locking heuristics added yet. |
| Lowering temperature guarantees consistent grading; QWK >=0.75 is a mandatory production gate | Consistency/QWK are useful metrics, but low temperature does not guarantee identical outputs and thresholds must be set by the team with real data. | Prepare measurement tooling without claiming thresholds are met. |

Rubric reference sources: [ETS Examinee Handbook, pages 26-31 and 37](https://www.kr.ets.org/pdfs/toeic/toeic-speaking-writing-examinee-handbook.pdf),
[ETS test design research](https://www.ets.org/Media/Research/pdf/TC-10-07.pdf).
Property ordering is simply an output feature [documented by Google](https://blog.google/innovation-and-ai/technology/developers-tools/gemini-api-structured-outputs/).

## Benchmarking Once Data Is Ready

Select 30-50 samples covering all Parts and score levels, including blank responses, off-topic answers, prompt copying, inflected keywords, emails missing requirements, and short/long essays.
Teachers grade each question on the exact `0-3`/`0-4`/`0-5` scale; Speaking uses `0-3` for Parts 1-4 and `0-5` for Part 5.
Separate prompt-tuning samples from the final evaluation set.
Run at least three trials per sample with model, rubric, and config held fixed.
Distinguish provider latency from total queue + grading latency so differing definitions are not compared.

Each measurement run is a JSON object:

```json
{
  "sampleId": "sample-001",
  "skill": "writing",
  "part": 2,
  "run": 1,
  "model": "MODEL_USED",
  "rubricVersion": "toeic-writing-v2",
  "maximum": 4,
  "score": 2,
  "referenceScore": 3,
  "latencyMs": 15000,
  "failed": false
}
```

The values above only illustrate the format.
Set `referenceScore: null` if teacher scores are not yet available; for failed runs set `failed: true, score: null`.
`part` also accepts `FULL` for full-test data.
Do not pool differing score scales.

```powershell
node scripts/benchmark-ai-evaluations.mjs runs.json --output benchmark.json
node --test scripts/benchmark-ai-evaluations.test.mjs
```

The tool outputs MAE/RMSE, exact match rate, QWK on the first run per sample (to avoid inflating sample size), variance across at least three runs, p50/p95, and failure counts, broken down by skill/Part/model/rubric/scale.
Without reference scores, accuracy metrics are null; QWK is also null when distributions lack variance.
The tool only analyzes exported files, sends no data to providers, and reads no credentials.

## Verification and Environment

- Backend locked-mode restore, formatting, and Release build ran. The Speaking/Writing test suite requiring no API host passed 175/175 tests. A full run prior to adding the final two boundary tests had 422 passed and 157 failed due to missing test PostgreSQL/API host; the full backend suite cannot yet be called green.
- FE passed format, lint, typecheck, and 158/158 targeted Speaking, Writing, and history tests. The full run had 597/599 passed; two 5s timeouts in L&R review and profile passed when re-run standalone alongside L&R room (16/16 total) without raising timeouts or changing tests.
- Playwright on available Chrome passed 2/2 scenarios: device check -> route transition -> 11 questions called `getUserMedia` once, uploaded all 11 non-empty recordings, and stopped tracks after submit; opening a submitted attempt did not invoke the microphone. Audio was mocked and API/BFF used fixtures: this verifies the browser flow, not real hardware or real backend. Test file: `toeicmaster-fe/tests/e2e/speaking-session.spec.ts`.
- Benchmark tool passed 5 tests with synthetic data; this is not a model benchmark result.
- API integration currently requires `TOEICMASTER_TEST_POSTGRES`; the Development API host always seeds L&R on startup. Local PostgreSQL was absent and Docker daemon was not running, so HTTP tests remain unverified. Neon/shared database was not used as a substitute.
- Local Node version is 24.14.1; the repo specifies >=24.21.0 on the Node 24 line. Re-run CI on the standard runtime before merging.
- Production build outside the sandbox compiled, typechecked, and generated 41 static pages before halting with `SecurityFatalError` due to an advisory on Next.js 16.4.2. `npm audit --omit=dev` confirmed a moderate advisory on `next`; [Next.js patched this in 16.4.5](https://nextjs.org/blog/november-2026-security-release/). The user chose to note this for the team to address separately, so package/lockfile were left untouched. An earlier sandbox build also had Google Fonts downloads blocked. Do not claim the production build succeeded.
- No migrations/database changes, pushes, or deploys were run during this review. Existing Writing sample output files present prior to the task were left untouched.

## Next Acceptance Steps

1. Run the full backend test suite with an isolated test PostgreSQL instance per CI, and audit legacy bank data before publishing new tests.
2. Use the [Speaking UAT](./speaking-uat-runbook.md) to test physical microphones, device disconnect, one-time permissions, reloads, and final-question recordings across browsers.
3. Run the [Writing UAT](./writing-uat-runbook.md) and L&R against real storage/backend; verify interactions, media, and partial score displays.
4. Prepare teacher-graded samples and benchmark the models; only then decide on weights/caps, split requests, temperature, or appropriate QWK gates.
