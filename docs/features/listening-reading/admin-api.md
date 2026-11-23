# Listening & Reading Administration API

> Scope: existing Admin endpoints in the backend and the FE BFF layer calling them.
> See the [specification](./spec.md), [score profiles](./scoring.md), and [shared API contract](../../contracts/api-contract.md).

The `/api/v1/admin/exams`, `/api/v1/admin/exam-media`, `/api/v1/admin/score-profiles`, and `/api/v1/admin/lr-exercise-bank` groups all require a JWT satisfying the `AccountAdministration` policy (`Admin` role).
Question and answer data here is administrative data; do not use these responses as the exam API for learners.

## 1. L&R Exercise Bank

The exercise bank is `LrExerciseBankItem`, which is independent of `Exam` and `ExamRevision`.
There is not yet an operation to pull items from the bank to assemble an exam.
Prefix: `/api/v1/admin/lr-exercise-bank`.

| Endpoint | Current functionality |
| --- | --- |
| `GET /summary` | Total question count, last updated timestamp, and statistics for Parts 1-7. |
| `GET /parts/{part}/items` | Paginated list; query: `query`, `difficulty`, `status`, `pageNumber`, `pageSize`. `pageSize` from 1-100. Response includes correct answers. |
| `POST /parts/{part}/items` | Create an item along with its sub-questions; returns `201` and the created item. |
| `GET /templates/{part}` | Download `.xlsx` template for Parts 1-7. |
| `POST /parts/{part}/imports` | Accepts `multipart/form-data` field `file`, `.xlsx` max 10 MiB; imports valid items and returns the number of created items/questions along with a summary. |

An item has `code`, `topic`, `difficulty` (`EASY`, `MEDIUM`, `HARD`), `status` (`DRAFT`, `PUBLISHED`, `ARCHIVED`), `context`, `audioUrl`, `imageUrl`, and `questions`.
Each question has `code`, `prompt`, `optionA`-`optionD`, `correctKey`, `translation`, `explanation`.
`context` is required for group items.
`audioUrl` and `imageUrl` are URL strings in the exercise bank, different from exam asset UUIDs.

| Part | Item type | Sub-questions per item | Options |
| --- | --- | ---: | --- |
| 1, 5 | `SINGLE` | 1 | A-D |
| 2 | `SINGLE` | 1 | A-C |
| 3, 4 | `GROUP` | 3 | A-D |
| 6 | `GROUP` | 4 | A-D |
| 7 | `GROUP` | 2-5 | A-D |

The import template uses one row per single item, and one row per sub-question of a group item.
Group files use `GroupCode`, `Context`, `QuestionCode`; single files use `Code`.
Import checks for duplicate codes within the file and against persisted data before adding items.
Currently there are no endpoints to fetch details by ID, edit, archive/delete, run separate import validation, or provide learner practice APIs.

## 2. Score Profiles

Prefix: `/api/v1/admin/score-profiles`.

| Endpoint | Request/response |
| --- | --- |
| `GET /` | Returns a list of `{ id, name, isComplete, mappingCount }`. |
| `POST /` | Body `{ "name": "...", "conversions": [{ "skill": "Listening", "rawCorrectCount": 0, "scaledScore": 5 }] }`; returns `201` with the same summary fields. |

Each `skill` is `Listening` or `Reading`; `rawCorrectCount` is 0-100, `scaledScore` is 5-495 and divisible by 5.
Each `(skill, rawCorrectCount)` pair can only be declared once.
A profile only has `isComplete=true` when it has all 101 entries for **each** skill.
The backend does not seed an ETS table and does not yet have endpoints to edit/delete profiles.
Publish rules are in [scoring.md](./scoring.md).

## 3. Exams and Revisions

Prefix: `/api/v1/admin/exams`.

| Endpoint | Current functionality |
| --- | --- |
| `GET /?status=Draft&pageNumber=1&pageSize=20` | List by latest revision status; `pageSize` 1-100. |
| `POST /` | Body `{ "code": "LR-2026-001", "title": "Practice test", "scoreProfileId": "<uuid>" }`; returns `201` with Draft version 1 and 200 slots. `code` is max 64 characters, consisting of letters/digits/`_`/`-` and starting with a letter or digit. |
| `GET /{examId}/revisions/{revisionId}` | Admin preview of all 103 blocks, 200 questions, contexts, answers, `audioAssetId`, `audioDurationMs`, `imageRequired`. |
| `PUT /{examId}/revisions/{revisionId}/questions/{questionNumber}` | Write the content of a question in Draft; returns `204`. |
| `PUT /{examId}/revisions/{revisionId}/blocks/{blockKey}/contexts` | Replace all contexts of a group block in Draft; body `{ "contexts": [...] }`, `ordinal` consecutive starting from 1; returns `204`. |
| `PUT /{examId}/revisions/{revisionId}/score-profile` | Select a different profile for Draft; body `{ "scoreProfileId": "<uuid>" }`; returns `204`. |
| `POST /{examId}/revisions/{revisionId}/publish` | Validate publish conditions, return `200` with `Published` status. Any previously published revision transitions to `Superseded`. |
| `POST /{examId}/revisions` | Body `{ "sourceRevisionId": "<published-revision-uuid>" }`; fork a published revision into a new Draft version, returns `201`. |

`UpdateQuestion` accepts `code`, `stem`, `optionA`, `optionB`, `optionC`, `optionD`, `correctKey`, `translation`, `explanation`, `imageAssetId`, `imageRequired`.
Part 2 only has A-C; other Parts require A-D.
`ReplaceContexts` accepts `Text` or `Image` contexts with `ordinal`, `type`, `text`, `imageAssetId`, `altText`, `imageRequired`.
`imageRequired=true` defers the image attachment requirement until publish; directly attached asset UUIDs must exist.

`Published` revisions cannot have their content edited in place.
A forked revision copies the content, score profile, and media state from the source revision.
See the [specification](./spec.md) for publish conditions.

## 4. Full Exam Import

- `GET /import-template`: download the `.xlsx` template with 4 sheets `Manifest`, `Blocks`, `ContextBlocks`, `Questions`.
- `POST /import`: `multipart/form-data`; `workbook` required, max 10 MiB.
  Returns `201` with a Draft if valid.

`Manifest` requires `SchemaVersion=1`, `TestCode`, `Title`, `Scope=LISTENING_READING`, `ScoreProfileId`.
Import validates exactly 103 blocks and 200 `BlockKey`/`QuestionNumber` pairs without relying on row order.
`ImageRequired` is an optional column in `ContextBlocks` and `Questions`.
The template file contains placeholders, so real content and score profile must be filled in before importing.
There is no separate import preview/validate endpoint.

## 5. Exam Media

`POST /api/v1/admin/exam-media/images` accepts `multipart/form-data` field `file`, PNG/JPEG/WebP image max 5 MiB, verifies file signature, and returns `{ id, contentType, kind, byteSize, durationMs }`.
The UUID `id` can be attached to questions/contexts via the Draft editing endpoints.

Media upload for any Draft follows three steps via presigned URLs:

1. `POST /{examId}/revisions/{revisionId}/media-upload-intents` with `{ ownerKind, ownerKey, contentType, byteSize, durationMs? }`; returns `201` with `intentId`, `uploadUrl`, `expiresAtUtc`, `requiredHeaders`.
2. Client `PUT`s bytes to `uploadUrl`, sending the exact `requiredHeaders`.
3. `POST /{examId}/revisions/{revisionId}/media-upload-intents/{intentId}/complete`; the backend verifies the object, content type, and size, then creates `ExamMediaAsset` (with `durationMs` from `expectedDurationMs`) and attaches it to the owner; returns `201` with `{ id, contentType, kind, byteSize, durationMs }`.

`ownerKind` accepts `AudioGroup`, `QuestionImage`, `ContextImage`.
`ownerKey` is correspondingly the `blockKey` for Listening Parts 1-4, the question number for Parts 1-7 as a string, or `<blockKey>:<ordinal>` for image contexts in Reading Parts 5-7.
Supported MIME types are `audio/mpeg` (alias `audio/mp3`), `audio/wav` (alias `audio/x-wav`) max 100 MiB and `image/png`, `image/jpeg`, `image/webp` max 5 MiB.
`durationMs` is optional (`1..3_600_000` ms) for `AudioGroup` and must be `null` for image intents.
Intents expire after 15 minutes; expired/completed intents or revisions no longer in Draft return `409`.

Publish requires audio for all Part 1-4 blocks and all questions/contexts with `imageRequired=true`.
The legacy endpoint `PUT /{examId}/revisions/{revisionId}/master-audio` has been removed.

## 6. Errors and Limits

- Invalid workbook: `400 ValidationProblemDetails`, key `workbook`; missing publish conditions: `400 ValidationProblemDetails`, key `exam`.
- Invalid media: `400 ProblemDetails`; duplicate exam code: `409`; exam/revision/profile not found: `404`.
- The exercise bank returns `400 ProblemDetails` when the file/item is invalid and `409 ProblemDetails` on duplicate codes.
- There are not yet endpoints to delete an exam/revision, fetch media for learner playback, or serve a public exam API for learners.

## 7. Frontend BFF Contract

FE calls same-origin `/api/admin/*`, and the BFF then forwards to the BE prefix `/api/v1/admin/*`.
The table below describes the BFF routes that actually exist; `{...}` denotes path parameters preserved as-is.

| BFF route | Method | Upstream BE | Used by UI |
| --- | --- | --- | --- |
| `/api/admin/exams` | `GET`, `POST` | `/api/v1/admin/exams` | List exams and create Drafts. |
| `/api/admin/exams/import`, `/import-template` | `POST`, `GET` | `/api/v1/admin/exams/import`, `/import-template` | Import exams / download template. |
| `/api/admin/exams/{examId}/revisions/{revisionId}` | `GET` | Same BE path | Open workspace. |
| `.../questions/{questionNumber}`, `.../blocks/{blockKey}/contexts` | `PUT` | Same BE path | Save Draft question and context. |
| `.../publish` | `POST` | Same BE path | Publish revision. |
| `.../media-upload-intents`, `.../media-upload-intents/{intentId}/complete` | `POST` | Same BE path | Steps 1 and 3 of presigned URL media upload. |
| `/api/admin/exam-media/images` | `POST` | `/api/v1/admin/exam-media/images` | Legacy image upload. |
| `/api/admin/score-profiles` | `GET` | `/api/v1/admin/score-profiles` | Read-only score profiles for selection when creating an exam. |
| `/api/admin/lr-exercise-bank/summary`, `parts/{part}/items`, `parts/{part}/imports`, `templates/{part}` | `GET`/`POST` | Same suffix under `/api/v1/admin/lr-exercise-bank` | Exercise bank summary, list/create, import, and template download. |

All BFF mutations verify same-origin before forwarding.
The BFF checks for a valid session and `ADMIN` role, preserves JSON/multipart bodies, enforces multipart size limits at the FE layer, forwards response/status/content type from BE, and adds `Cache-Control: no-store`.

### Confirmed FE Gaps

- BE has `POST /api/v1/admin/score-profiles`, but FE only has a `GET` BFF route and no profile creation form.
  Admin must use another authorized API client to create a score profile before the UI has a profile to select.
- BE has `POST /api/v1/admin/exams/{examId}/revisions` to fork a revision, but FE does not yet have a corresponding BFF route or UI action.
- There is no learner L&R BFF/FE.
  Do not use the Admin BFF as a learner API because revision and exercise bank item responses contain `correctKey`.
