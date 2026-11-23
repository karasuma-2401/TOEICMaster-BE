# Listening & Reading Score Profiles

> This document describes the **existing score profile configuration and exam publish requirements**, not a learner scoring API.
> See [Admin API](./admin-api.md#2-score-profiles) and [Attempt API](./attempt-api.md).

## Score Profile Data

`ScoreProfile` is a conversion table created by Admin and bound to an `ExamRevision` by UUID.
Each `ScoreConversion` contains `skill` (`Listening` or `Reading`), `rawCorrectCount` (0-100), and `scaledScore` (5-495, multiple of 5).
A profile is considered complete when it has all 101 distinct entries for each skill, totaling 202 entries.

`POST /api/v1/admin/score-profiles` creates a profile; `GET /api/v1/admin/score-profiles` lists profiles with `isComplete` and `mappingCount`.
The backend does not provide a default conversion table, does not claim Admin-entered profiles are official ETS tables, and does not yet have APIs to edit/delete profiles.

## Binding Score Profiles to Exams

Creating or importing an exam requires `scoreProfileId` pointing to an existing profile.
A Draft can switch profiles via `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/score-profile`.
Publish rejects if the profile does not yet have 101 Listening entries and 101 Reading entries, returning `400 ValidationProblemDetails` with key `exam`.

A published revision retains its `scoreProfileId`; forking creates a new Draft from a published revision.
Currently, the backend does not yet have `L&R Attempt`, a step to count correct answers, lookup against the conversion table for an attempt, or an API returning Listening/Reading/total scores.
The formula and timing for returning results to learners are product requirements in the [PRD](../../specs/prd-mvp.md), not current API behavior.

## Current Display and Usage on FE

Admin FE calls `GET /api/admin/score-profiles` via the BFF and displays profiles in the selector when creating a Draft.
The FE response uses only the four summary fields `id`, `name`, `isComplete`, `mappingCount`; it does not receive the detailed conversion table from the list API.

Currently there is no FE form/BFF to call `POST /api/v1/admin/score-profiles`, no UI to change a revision's score profile even though BE has the `PUT .../score-profile` endpoint, and no learner UI displaying scaled scores.
Therefore, this document does not claim that FE already computes or displays TOEIC scores for examinees.
