# Speaking Test Builder Redesign (Admin)

> **Status:** Implemented search, slot position preservation, full validation error display, non-closing draft save, and test cloning on 2026-11-21. `publish-check`, similarity warnings, and exercise usage counts remain in the backlog.
> **Screens:** `/admin/tests/speaking` (Figma 41), `/admin/content/speaking` (Figma 36)  
> **Root Issue:** section 4 of [audit report](../../runbooks/speaking-audit-report.md)  
> **Uniqueness Rules:** [ADR-002](../../decisions/002-speaking-test-content-uniqueness.md)

FE directories in this document are relative to `src/features/speaking/components/admin/`.

The builder currently supports both direct content entry and assembly from the exercise bank as required.
A new test code/title is entered when cloning; the clone is always an independent DIRECT test preserving the source snapshot content, even when the source came from the exercise bank.
The actual clone contract also includes `version`; see [Admin API](./admin-api.md).

---

## 1. Target Layout

Keep the existing 3-column layout.
Key changes: organize around the **11 candidate questions**, pick exercises via a searchable picker, and group errors in a single place with jump links.

```text
┌ TEST BUILDER · SP-2026-01 · [Draft] · Saved 10:32            [Close] [Save] [Publish] ┐
├ ⚠ 2 errors, 1 warning: Q1 & Q2 duplicate passage (Fix ->) · Q6 & Q8 duplicate (Fix ->)┤
├───────────────┬─────────────────────────────────────┬─────────────────────────────────┤
│ Test Info     │ Part 1 · Read aloud · 1/2 · Q1-2    │ User View                       │
│ Code, Title,  │ ┌ Question 1 ─────────────────────┐ │ [Selected Item] [Full 11 Qs]    │
│ Description   │ │ P1-003 · Library hours · Used 2x│ │                                 │
│               │ │ [Change] [View] [Clear]         │ │                                 │
│ 11-Q Map      │ ├ Question 2 ─────────────────────┤ │                                 │
│ 1 2 3 4 5 6 7 │ │ [+ Select exercise]             │ │                                 │
│ 8 9 10 11     │ └─────────────────────────────────┘ │                                 │
│(green/red/gry)│ Part 3 · Respond · 0/1 · Q5-7       │                                 │
│ Requirements  │                                     │                                 │
└───────────────┴─────────────────────────────────────┴─────────────────────────────────┘
```

The exercise picker opens as a panel when clicking "Select exercise":

```text
┌ Select exercise for Question 2 · Part 1 ──────────────┐
│ [Search code, topic...] [Difficulty v]                │
│ P1-001 · Museum tour · Easy · Used in 1 test [Select] │
│ P1-003 · Library hours · In use at Q1 (locked)        │
│ P1-004 · Bus notice · Easy                   [Select] │
│ < Page 1/5 >       No matching exercise? [Create ↗]   │
└───────────────────────────────────────────────────────┘
```

---

## 2. Solutions by Issue

| ID | Solution | Primary FE Files | Needs BE? |
| :--- | :--- | :--- | :--- |
| UI-01 | Stop loading 100 exercises on open. The picker calls `GET /admin/speaking/exercises?part=&query=&difficulty=&status=PUBLISHED&pageNumber=&pageSize=20` with a 300 ms debounce. Maintain an `id -> exercise` map from selected items and loaded results. | `speaking-test-workspace.tsx`, `test-slot-picker.tsx`, new picker `exercise-picker-panel.tsx` | No, API already supports it |
| UI-02 | Preserve exact slot positions. Remove `compact()` on submit; `sequenceNumber = slotIndex + 1`, skipping empty slots. Parser places items at `exerciseIds[part][sequenceNumber - 1]`. | `speaking-test-workspace.tsx`, `admin-api.ts`, `parsers.ts` | No, validator already allows missing slots in drafts |
| UI-03 | `fieldErrors` retains the full array of errors. Audit callers currently expecting `string`. | `http.ts`, call sites | No |
| UI-04 | Display the server's `title` verbatim. On version conflict, show a "Reload latest" button and **preserve** in-progress edits so Admin can compare. Needs `code` in ProblemDetails to distinguish conflict causes. | `speaking-test-workspace.tsx` | Yes, add `code` (in addition to `title`) |
| UI-05 | Diff the draft against the last saved state. Prompt for confirmation on close when dirty; add `beforeunload`. | `speaking-test-workspace.tsx` | No |
| UI-06 | Saving a draft does not close the builder: update `saved` and `version`, and show "Saved at ...". | `speaking-test-workspace.tsx`, `admin-speaking-tests-page.tsx` | No |
| UI-07 | Error heading matches the action: "Could not save" or "Cannot publish yet". | `publish-checklist-panel.tsx` | No |
| UI-08 | Badges and checklist read from a single source. Each slot has 3 states: valid, invalid with reason, or empty. Clicking an error scrolls to that question (per Figma 43). | `test-slot-picker.tsx`, `publish-checklist.ts` | Uses `publish-check` |
| UI-09 | "Clone as draft" for published tests; "Delete" for draft tests. "Withdraw" comes later (D4). | `speaking-tests-table.tsx`, `speaking-test-workspace.tsx` | Yes, see section 4 |
| UI-10 | Standardize labels: header `11 QUESTIONS · 7 EXERCISES`; each Part shows `1/2 exercises · Q1-2`. | `speaking-test-workspace.tsx`, `test-slot-picker.tsx` | No |
| UI-11 | Part 4 uses a row-based table input (label, value, add, remove, max 20 rows). Pre-validate server limits. Show errors beside each field. Load existing items via `DocumentRows` as before. | `speaking-exercise-fields.tsx`, `admin-draft.ts` | No |
| UI-12 | In the exercise form, show "Used in N tests". Moving off `PUBLISHED` while a draft test uses it prompts for confirmation. | `speaking-exercise-editor.tsx` | Yes, add usage count |
| UI-13 | Add a "Full 11 Qs" tab in the preview column; empty Parts show a "Create exercise ↗" link that opens a new tab and refreshes on return. | `test-preview-panel.tsx` | No |
| DUP-04 | Already-used exercises remain visible but locked, labeled "In use at Question n". Do not hide them, so Admin understands why they cannot be selected. | `exercise-picker-panel.tsx` | No |
| DUP-05 | Display "Used in N tests" in the picker and table. | `exercise-picker-panel.tsx` | Yes, add usage count |

---

## 3. Pre-publish Check Flow

1. Admin changes a selection or test metadata.
2. After 500 ms, the FE calls `POST /admin/speaking/tests/publish-check` with the current draft, even if unsaved. Previous requests are cancelled via `AbortController`.
3. The banner, 11-question map, and checklist all render from this response.
4. The "Publish test" button is disabled while any blocking error remains. If only warnings exist, a confirmation dialog lists each warning.
5. On publish click: save the draft, then call `POST /tests/{id}/publish`. The server re-validates everything.
6. If `publish-check` fails due to a network error: still allow saving the draft, clearly display "Unable to verify yet", and let the server decide on publish.

FE `publishChecklist` remains active for instant feedback (required exercise counts, duplicate IDs). Content rules come strictly from the server.

---

## 4. Required Backend Changes

The contract is defined in [admin-api.md](./admin-api.md#proposed-new-apis-not-yet-implemented).

| Change | Endpoint or Field | Migration? | Priority |
| :--- | :--- | :--- | :--- |
| Pre-publish check | `POST /tests/publish-check` | No | P3 (section 3 is the target design; before this, errors appear on publish click) |
| Clone test | `POST /tests/{id}/clone` | No | P2 |
| Delete draft | `DELETE /tests/{id}?version=` | No | P2 |
| Exercise usage count | `usedInTestCount` in exercise list items | No | P2 |
| Machine-readable error code | `code` in `409` ProblemDetails | No | P2 |
| Withdraw test | `POST /tests/{id}/withdraw` | **Yes**: `ck_speaking_tests_status` currently only allows `DRAFT`, `PUBLISHED` | P3, pending D4 |

---

## 5. Acceptance Criteria

```gherkin
Scenario: Already-selected exercise cannot be selected again
  Given exercise P1-003 is assigned to Question 1
  When Admin opens the picker for Question 2
  Then P1-003 shows "In use at Question 1" and cannot be clicked

Scenario: Older exercises remain selectable in a large bank
  Given Part 1 has more than 100 PUBLISHED exercises
  When Admin searches for the code of the oldest exercise
  Then that exercise appears and can be selected

Scenario: Saving does not shift slot positions
  Given Question 2 has a selected exercise and Question 1 is empty
  When Admin saves the draft and reopens it
  Then the exercise remains at Question 2 and Question 1 remains empty

Scenario: Saving a draft does not lose editing context
  When Admin clicks "Save draft"
  Then the builder stays open, displays the saved timestamp, and uses the new version for subsequent saves

Scenario: Unsaved changes are not lost on accidental close
  Given there are unsaved changes
  When Admin clicks "Close"
  Then the system asks for confirmation before closing

Scenario: All errors are displayed and link to exact positions
  Given Question 1 and Question 2 have duplicate passages, and Question 5 duplicates Question 8
  When the validation result returns
  Then the banner shows both errors
  And clicking an error scrolls to the corresponding question
  And the "Publish test" button is disabled

Scenario: Editing a published test via cloning
  Given test SP-01 is published
  When Admin selects "Clone as draft"
  Then a new draft test is created with a new code and the same exercises
  And SP-01 and its existing attempts remain unchanged
```
