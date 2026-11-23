# TOEIC Writing Module Checklist

References: [specification](spec.md), [Admin API](admin-api.md), [Attempt API](attempt-api.md), and [AI Evaluation](ai-evaluation.md).

## Scope in Source

- Standalone directly entered tests or Excel import; preserves support for reading legacy tests referencing the bank.
- Standalone exercise bank CRUD, Excel import, and untimed Part-based practice.
- Optional `modelAnswer`, internal use only; AI only receives reference answers when configuration allows after calibration.
- AI grading after consent, Part 1 images sent along with prompts, with a total image size limit.
- The `AddWritingIndependentTestsAndPractice` migration is added in source; it has not been applied to shared environments yet.

## Still Needs Confirmation Before Real-World Usage

- Run migrations and UAT on a team-approved environment per the [runbook](../../runbooks/writing-uat-runbook.md).
- Calibrate AI against instructor-graded submissions, comparing the rubric against cases with reference answers; only enable reference answers after evaluation.
- Perform full live testing of MinIO, database, Admin/User/guest permissions, and Gemini according to environment configuration.
