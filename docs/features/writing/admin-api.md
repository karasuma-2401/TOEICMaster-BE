# Writing Administration API

All endpoints use the `/api/v1/admin/writing` prefix and the `WritingAdministration` policy (role `Admin`).

## Standalone Exercise Bank

- `GET /exercises?part=1&query=&difficulty=&status=&pageNumber=1&pageSize=20`: paginated list.
- `POST /exercises`, `GET /exercises/{id}`, `PUT /exercises/{id}` with body `{ "version": 1, "exercise": { ... } }`, `DELETE /exercises/{id}?version=1` to archive.
- An exercise includes `part`, `code`, `topic`, `difficulty`, `status`, `rubricNotes`, and `modelAnswer`. `modelAnswer` is an optional internal reference; it is not returned to learners.
- Part 1 requires `imageAssetId`, `imageAlt`, `keywordOne`, `keywordTwo`; Part 2 requires `sender`, `recipient`, `subject`, `emailBody`, `directions`; Part 3 requires `prompt`, `minimumWordCount`, and optionally `outline`.
- `POST /exercises/imports/validate?part=1` previews `.xlsx`; `POST /exercises/import?part=1` re-validates and saves the entire valid file in a single save operation. Both endpoints accept `multipart/form-data`, field `file`, max 10 MiB; exercise codes must be unique, and images must exist.
- `POST /media/images` uploads JPEG/PNG/WebP up to 5 MiB and returns `id` to use as `imageAssetId`.

## Standalone Writing Tests

- `GET /tests`, `GET /tests/{id}` retrieve the list/details. Statuses are `DRAFT`, `PUBLISHED`, or `ARCHIVED`; filtering by `status` is supported.
- `POST /tests` creates a draft; `PUT /tests/{id}` updates a draft with `{ "version": 1, "test": { ... } }`. Each item in `questions` contains `part`, `sequenceNumber` (starting from 1 within each Part), `exerciseId: null`, and `content` following the exercise structure above. A draft can be saved before all questions are filled; each entered question's content must be valid. The API continues to read legacy tests that reference `exerciseId`.
- `POST /tests/{id}/publish` with `{ "version": 1 }` requires exactly 5/2/1 questions in continuous order; a published test cannot be edited.
- `DELETE /tests/{id}?version=1` permanently deletes a draft test along with its questions. Published or archived tests cannot be deleted via this endpoint.
- `POST /tests/{id}/archive` with `{ "version": 2 }` archives a published test. The test is no longer shown to learners and new attempts cannot be started; existing attempts, results, and snapshot contents are preserved.
- `POST /tests/{id}/restore` with `{ "version": 3 }` restores an archived test back to published status with its previous content. All operations above check `version`; stale data receives a 409 error.
- `GET /tests/import-template` downloads the Excel template; `POST /tests/imports/validate` previews; `POST /tests/import` re-validates and creates a `DRAFT` test in a single save operation. The import endpoints accept `.xlsx` up to 10 MiB.
- The workbook consists of `Manifest` (`TestCode`, `Title`, `Description`), `Part1`, `Part2`, and `Part3`. `SequenceNumber` values in Excel are 1-5, 6-7, and 8 respectively; images must be uploaded beforehand and their `ImageAssetId` filled in. Column names follow the template file; `ModelAnswer` and `RubricNotes` are optional.

Do not modify already applied migrations. The `AddWritingIndependentTestsAndPractice` migration needs to be applied by the team on an appropriate environment before using the new functionality. Do not run migrations against the shared database without team approval.
