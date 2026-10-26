# ADR-001: Versioned administration for TOEIC L&R exams

## Status

Accepted

## Date

2026-10-26

## Context

An Admin creates and corrects TOEIC Listening & Reading exams with a fixed ETS structure of 200 questions. A published exam must not be altered in place because future learner attempts need to remain associated with the content, answer key, Master Audio and score configuration that were selected for that exam version.

Bulk authoring also needs to be fast without accepting incomplete or partially imported exams. The agreed import unit is one XLSX workbook for the entire L&R exam plus one separately uploaded Master Audio file.

## Decision

- `Exam` is the stable exam identity. `ExamRevision` contains the versioned 103-block, 200-slot exam snapshot.
- A revision begins as `Draft`. Publishing it marks the prior published revision as `Superseded`. Editing a published revision requires forking a new Draft revision.
- Import is atomic. The API validates workbook schema, 103 fixed blocks, 200 questions, question option rules, group contexts, referenced images and Master Audio before persisting a Draft. A failed database write deletes the newly uploaded audio object best-effort.
- The XLSX has `Manifest`, `Blocks`, `ContextBlocks` and `Questions` sheets. Mapping uses `BlockKey` and `QuestionNumber`, not spreadsheet row order.
- Master Audio is one separate `.mp3` or `.wav` upload, maximum 100 MiB. It is not embedded in the workbook and individual Part 3/4 audio is not represented in the exam model.
- Score profiles are reusable Admin-created configurations. They are selected by UUID in a Draft. Publishing requires complete mappings for raw scores 0–100 for both Listening and Reading. No ETS conversion table is seeded without an approved source.

## Alternatives considered

### Editable published exam

- Pros: fewer records and a simple edit flow.
- Cons: existing attempts could be interpreted against changed questions, answer keys or score tables.
- Rejected because it breaks version consistency.

### Separate Reading and Listening imports

- Pros: smaller individual files.
- Cons: it permits mismatched halves and separate upload lifecycles for one exam.
- Rejected because one L&R exam has one fixed 200-slot structure and one Master Audio.

### Incremental import that saves partial Draft data

- Pros: Admin can resume a broken import.
- Cons: incomplete artifacts are difficult to distinguish from intentionally authored Drafts and a re-import can leave conflicting state.
- Rejected because an all-or-nothing import is safer and clearer.

### Seed an ETS score table

- Pros: quicker publish setup.
- Cons: the project has no approved official source and the conversion may vary by form.
- Rejected. Admin supplies approved mappings through score profiles.

## Consequences

- Consumers must use the revision-specific endpoints when editing or publishing.
- The application needs a migration for revisions, blocks, questions, context blocks, score profiles, conversions and image assets.
- The current scope deliberately excludes learner attempts, submission, score calculation and attempt snapshots. Those features must reference an immutable published revision when added.
