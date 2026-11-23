# Speaking Administration API

Endpoints use the `/api/v1/admin/speaking` prefix and require a JWT with the `SpeakingAdministration` policy.
All responses include `Cache-Control: no-store`.

## Overview

- `GET /summary`: returns `{ exercises: { total, published, draft, archived }, tests: { total, published, draft } }`.

## Exercise Bank

`GET /exercises?part=1&query=walk&difficulty=EASY&status=PUBLISHED&pageNumber=1&pageSize=20`

Returns a paginated list ordered by `updatedAtUtc DESC`, then `id`.
`pageSize` ranges from 1 to 100.
`query` searches `code` and `topic`, up to 100 characters.
Enums: `EASY`, `MEDIUM`, `HARD` and `DRAFT`, `PUBLISHED`, `ARCHIVED`.

- `POST /exercises`: creates an exercise, returns `201`.
- `GET /exercises/{exerciseId}`: retrieves exercise details.
- `PUT /exercises/{exerciseId}`: body `{ "version": 1, "exercise": { ... } }`. Cannot change Part.
- `DELETE /exercises/{exerciseId}?version=1`: transitions to `ARCHIVED` without deleting data.

Common body fields:

| Field | Constraints |
| --- | --- |
| `part` | 1-5 |
| `code` | 1-32 alphanumeric or hyphen characters; unique, case-insensitive |
| `topic` | 1-100 characters |
| `difficulty`, `status` | Enums listed above |
| `rubricNotes` | Up to 2000 characters, optional |
| `modelAnswer` | 1-4000 characters |

Part-specific fields:

| Part | Required Fields |
| --- | --- |
| 1 | `passage` (1-1500) |
| 2 | `imageAssetId`, `imageAlt` (1-300) |
| 3 | `context` (1-600), `questions` with exactly 3 items (each 1-300) |
| 4 | `documentTitle` (1-200), `documentRows` with 1-20 rows `{ label (1-40), value (1-200) }`, `questions` same as Part 3; `callerIntro` optional (up to 400) |
| 5 | `prompt` (1-600) |

The server persists only fields belonging to the corresponding Part inside `content_json`.
`400` errors use unprefixed field names, such as `Passage`, so forms highlight errors in the right place.

**Target (ADR-002):** `questions` in Part 3-4 must be distinct after normalization.

## Images

- `POST /media/images`: `multipart/form-data`, field `file`. JPEG or PNG only, validated via magic bytes, max 5 MiB. Returns `201` with `{ id, contentType, sizeBytes }`.
- `GET /media/{assetId}`: retrieves an image for Admin, including unpublished images.

Assets are immutable. Replacing an image means uploading a new asset.

## Speaking Tests

- `GET /tests?query=&status=&pageNumber=1&pageSize=20`: each item includes `partCounts` and `questionCount`.
- `GET /tests/{testId}`: published tests read from the snapshot; draft tests read current exercises.
- `POST /tests`: creates a draft test, returns `201`.
- `PUT /tests/{testId}`: saves a Draft directly; published legacy tests only allow updating basic metadata (code, title, description) without modifying content.
- `POST /tests/{testId}/publish`: body `{ "version": 1 }`.

New test body (direct builder input):

```json
{
  "code": "SP-2026-01",
  "title": "Speaking Test 01",
  "description": "",
  "contentMode": "DIRECT",
  "exercises": [],
  "questions": [
    {
      "part": 1,
      "sequenceNumber": 1,
      "content": {
        "rubricNotes": "Clear pronunciation.",
        "modelAnswer": "Reference answer for evaluation.",
        "passage": "Please read this short announcement aloud."
      }
    }
  ]
}
```

`questions` represent content owned exclusively by the test: they do not create `speaking_exercises`, do not appear in the Exercises tab, and cannot be reused by other tests.
In Parts 3 and 4, each item contains a shared context along with exactly three sub-questions, so a test has 7 content groups corresponding to 11 candidate questions (2/2/3/3/1).
A Draft may omit groups; each submitted group must satisfy its Part's field rules.
Part 2 images are still uploaded via `POST /media/images`, then referenced via `imageAssetId` in `content`.

Legacy-compatible body:

```json
{
  "code": "SP-2026-01",
  "title": "Speaking Test 01",
  "description": "",
  "exercises": [
    { "exerciseId": "uuid", "part": 1, "sequenceNumber": 1 }
  ]
}
```

On save: draft tests may omit exercises.
Each exercise must exist and match the Part; `sequenceNumber` must range from 1 to the Part's exercise count; neither `exerciseId` nor position may be duplicated.

Legacy payloads without `contentMode` (or using `EXERCISE_BANK`) remain readable for viewing/taking tests.
The API does not migrate legacy data to the direct builder format.
For published legacy tests, a `PUT` without `exercises` updates metadata only; the snapshot content remains unchanged.

**Publish Conditions** (`422` error, returning all errors at once):

| Error Key | Condition |
| --- | --- |
| `Code`, `Title` | Non-empty |
| `Part1` ... `Part5` | Exactly 2/2/1/1/1 exercises, numbered sequentially starting from 1 |
| `Exercises` | No duplicate exercises; all exercises exist, match the Part, and are `PUBLISHED` |
| `Part1` ... `Part5` (direct) | Exactly 2/2/1/1/1 content groups in sequential order; Part 3/4 have exactly three sub-questions |
| `Questions` | Contains only direct content belonging to the test, with no bank references mixed in |
| `Content` | No duplicate text after NFKC/lowercase/diacritic and whitespace normalization; no duplicate images by asset or SHA-256, in both modes |

When publishing, the server writes `snapshot_json` for each slot, increments `version`, and locks the test.
Editing or archiving exercises afterward does not affect published tests.
`snapshot_json` includes the rubric and model answer and is intended only for Admin and attempt snapshots.

## Errors

| Code | When |
| --- | --- |
| `400` | Invalid input |
| `404` | Not found |
| `409` | Duplicate code, stale `version`, Part change, editing a published test |
| `413` | Image exceeds 5 MiB |
| `422` | Publish conditions not met |

## Proposed New APIs (Not Yet Implemented)

All additions are **purely additive**, leaving existing responses unchanged.

### Pre-publish Check (Proposed)

`POST /tests/publish-check`.
Same body as `POST /tests`.
Does not persist data.
Accepts tests with missing exercises.

```json
{
  "canPublish": false,
  "issues": [
    {
      "code": "DUPLICATE_PROMPT_TEXT",
      "severity": "BLOCKING",
      "message": "Question 1 and Question 2 have identical content.",
      "positions": [1, 2],
      "exerciseIds": ["uuid", "uuid"]
    },
    {
      "code": "SIMILAR_CONTENT",
      "severity": "WARNING",
      "message": "Question 6 and Question 8 are very similar.",
      "positions": [6, 8],
      "exerciseIds": ["uuid", "uuid"]
    }
  ]
}
```

- `severity`: `BLOCKING` or `WARNING`.
- `code`: `MISSING_INFO`, `INCOMPLETE_PART`, `EXERCISE_NOT_FOUND`, `EXERCISE_WRONG_PART`, `EXERCISE_NOT_PUBLISHED`, `DUPLICATE_EXERCISE`, `DUPLICATE_QUESTION_IN_EXERCISE`, `DUPLICATE_PROMPT_TEXT`, `DUPLICATE_IMAGE`, `SIMILAR_CONTENT`, `SAME_TOPIC_IN_PART`.
- `positions`: question positions 1-11 in a full test, allowing the UI to scroll directly to the issue.
- Returns `400` when the body structure is invalid. Missing exercises are not a `400` error.

`POST /tests/{testId}/publish` uses the same logic.
`BLOCKING` severity returns `422`.
`WARNING` severity does not block publishing.

### Clone Existing Test

`POST /tests/{testId}/clone`, body `{ "version": 2, "code": "SP-2026-01-V2", "title": "..." }`.
Creates an independent draft DIRECT test and returns `201`.
For a published source built from the exercise bank, it copies the frozen snapshot rather than reading current exercise bank content.
Leaves the source test and its attempts untouched; stale version or duplicate code returns `409`.

### Delete Draft Test

`DELETE /tests/{testId}?version=1`.
Draft tests only.
Returns `204`; deletes the test and its slot entries without deleting exercises.
Missing `version` or `version` less than 1 returns `400`; not found returns `404`; published test or stale version returns `409`.

### Number of Tests Using an Exercise (Proposed)

Add `usedInTestCount` to list items in `GET /exercises`.
This is a new field without altering existing fields.

### Implemented Error Codes for `409`

ProblemDetails includes the `code` extension: `DUPLICATE_CODE` for duplicate test codes, `VERSION_CONFLICT` for stale versions, and `CONFLICT` for other conflicts.
The frontend preserves the server's `title` and entire `errors` array.

### Withdraw Test (Pending D4)

`POST /tests/{testId}/withdraw`.
Requires a new migration to add the status to `ck_speaking_tests_status`.
Existing attempts remain intact.
