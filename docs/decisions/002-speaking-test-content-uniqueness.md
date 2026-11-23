# ADR-002: Content Uniqueness Across Questions in a Speaking Test

## Status

Step 1 implemented on 2026-11-21.
Step 2 (publish-check and similarity warnings) remains in the backlog.

## Date

2026-11-03

## Context

A Speaking test has 11 questions, assembled from 7 exercises in the bank: Part 1 and Part 2 each have 2 exercises (1 question each), Part 3 and Part 4 each have 1 exercise (3 questions each), and Part 5 has 1 exercise (1 question).

Update: the system also supports inline content entered directly on the test.
Duplicate-blocking rules run at publish time in both modes.

Currently, the system only guarantees that an `ExerciseId` is not repeated in a test.
This mechanism does not detect the following cases:

- 3 identical questions within the same Part 3 or Part 4 exercise (DUP-01).
- Two exercises with different IDs but the same passage or the same question (DUP-02).
- The same image uploaded twice, resulting in two different assets (DUP-03).

Moodle shares this exact limitation: a single question does not appear twice, yet [two copies of the same question still slip through](https://moodle.org/mod/forum/discuss.php?d=251833).
The assessment industry also has the concept of [enemy items](https://assess.com/enemy-items/): two questions that should not appear on the same test, even if their wording differs.

Details and evidence are in the [audit report](../runbooks/speaking-audit-report.md).

## Decision

### 1. Definition of "duplicate" by severity level

| Code | Level | Condition | Applied When |
| :--- | :--- | :--- | :--- |
| `DUPLICATE_EXERCISE` | Block | Same `ExerciseId` in two slots. | Existing, unchanged |
| `DUPLICATE_QUESTION_IN_EXERCISE` | Block | Two questions in a Part 3 or Part 4 exercise are identical after normalization. | Saving exercise; re-checked at publish |
| `DUPLICATE_PROMPT_TEXT` | Block | Two text units in a test are identical after normalization. | Publishing |
| `DUPLICATE_IMAGE` | Block | Two Part 2 exercises have the same `ImageAssetId` or the same `sha256`. | Publishing |
| `SIMILAR_CONTENT` | Warning | Two text units have a word-level Jaccard similarity >= 0.8. | Pre-publish |
| `SAME_TOPIC_IN_PART` | Warning | Two exercises in the same Part have the same `Topic` after normalization. | Pre-publish |

Warnings do not block publishing.
Admins retain final discretion because two announcements may share a template while remaining valid.

**Reusing an exercise across different tests is allowed.**
Existing attempts already keep their own snapshots, and the project's exercise bank is still small.
The UI only needs to display how many tests currently use the exercise.

### 2. Comparison units

| Part | Question Position | Unit |
| :--- | :--- | :--- |
| 1 | 1-2 | `passage` |
| 2 | 3-4 | Image, compared by asset id and `sha256` |
| 3 | 5-7 | Each item in `questions` |
| 4 | 8-10 | Each item in `questions` |
| 5 | 11 | `prompt` |

All text units are compared against one another, even across different Parts.
For example, a Part 3 question that duplicates a Part 4 question is still blocked.
A test has at most 11 units, so pairwise comparison is fast enough.

### 3. Normalization rules

Apply in exact order:

1. Unicode NFKC.
2. Lowercase using `ToLowerInvariant`.
3. Replace every non-alphanumeric character with a space.
4. Collapse consecutive spaces and trim both ends.

Thus `"What time is it?"` and `"what  time is it"` are treated as duplicates.

### 4. Where to place the logic

- Create `Application/Speaking/Admin/SpeakingTestContentRules.cs` containing pure, I/O-free functions: takes exercises and image data, returns a list of issues.
- `SpeakingExerciseInputValidator`: add rule requiring 3 distinct questions. Returns `400` with key `Questions`.
- `PublishSpeakingTestCommandHandler.Handle`: after `PublishRequirements`, fetch image hashes and call the function above. Blocking-level errors return `422` with the new key `Content`; `Exercises` and other keys remain unchanged. `PublishRequirements` remains synchronous because reading assets performs I/O.
- Fetch `sha256` via the existing `FindMediaAssetAsync`, at most 2 calls for Part 2. **No repository interface change required.**
- FE must fix UI-03 at the same time. `http.ts` currently keeps only the first error per key, so without this fix, Admins only see one duplicate pair per publish attempt.
- **No migration required.** The data needed for comparison already exists in `content_json` and `speaking_media_assets.sha256`.

### 5. Two-step rollout

**Step 1 (first):** blocking-level codes only, checked when saving an exercise and when publishing.
No new API.
Errors appear after clicking publish.
Disabling already-selected exercises in the dropdown can be handled directly by FE.

**Step 2 (when needed):** warning-level codes, plus endpoint `POST /api/v1/admin/speaking/tests/publish-check`.
The endpoint accepts the same body as `POST /tests`, performs no writes, and accepts incomplete tests.
Warnings are non-blocking, so they require this dedicated channel.
The contract is defined in [admin-api.md](../features/speaking/admin-api.md#pre-publish-check-proposed).

The server maintains a single source of logic, so FE does not need to duplicate normalization rules.

## Alternatives considered

### Unique index on content hash at the bank level

- Pros: strictly enforced by the database.
- Cons: blocks valid cases, requires a migration, and cannot handle duplicates across Parts.
- Rejected. This rule is a per-test constraint, not a bank-wide constraint.

### Client-side validation on FE only

- Pros: fast, no backend changes.
- Cons: direct API calls bypass it; data can change between check time and publish time.
- Rejected. The server must be the final authority, consistent with existing publish requirements.

### Block on near-similar content as well

- Pros: stricter test quality.
- Cons: thresholds are not yet calibrated so false positives are likely, and Admins would have no override.
- Rejected for this stage.

### Visual image similarity comparison (perceptual hash)

- Pros: detects resized or re-compressed images.
- Cons: requires adding an image-processing library, while the project avoids adding packages unless required.
- Deferred. `sha256` only catches byte-for-byte identical images; this is an accepted limitation.

### Manually curated enemy item list

- Pros: catches question pairs that give away answers to each other.
- Cons: high data-entry overhead, requires a new table.
- Deferred until the bank grows large enough.

## Consequences

- The prototype verified Step 1: 1 new file of 61 lines, blocking all duplicate scenarios as expected (see [report section 0](../runbooks/speaking-audit-report.md#0-re-verification-results-2026-11-03)).
- **Test fixtures must be updated at the same time.** `FullTestAsync` reuses one passage for two Part 1 exercises and one image for two Part 2 exercises, causing 22 red tests. Add a second passage and a byte-distinct PNG image to `SpeakingSamples`, and update `FullTestAsync`, leaving 2 tests (`Admin_and_learner_contract_matches_the_frontend`, `Published_images_are_served_only_through_a_published_snapshot`). Do not relax the rules just to make tests green.
- `SAMPLE-` exercises on Neon may share images or content.
Test `SAMPLE-FULL-01` is unaffected, though creating new tests from those exercises may be blocked.
- Already-published tests are unaffected because they use snapshots; no legacy data cleanup is needed.
- Legacy exercises with duplicate questions will receive `400` on their next edit, and tests using them cannot be published until fixed.
Existing attempts and Part practices are unaffected.
- Before rollout, run the read-only query in the [UAT runbook](../runbooks/speaking-uat-runbook.md#5-existing-data-audit) to see how many exercises need fixing.
- Remaining limitations: paraphrased content with the same meaning is not detected; edited images are not detected; a brief window exists between check time and save time where data could change, an accepted risk given the small number of Admins.
- A future phase may add a `content_fingerprint` column via a new migration to warn about bank-wide duplicates at exercise creation time.
