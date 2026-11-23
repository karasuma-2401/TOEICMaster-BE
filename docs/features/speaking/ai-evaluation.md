# AI Evaluation for TOEIC Speaking

Speaking is graded via a durable queue.
Submissions and recordings are always saved first, so provider errors, quota exhaustion, or timeouts do not cause lost work.
Scores are always labeled as an **AI-estimated score (0–200, step 10)**, not an ETS score.

## Usage Flow

1. The learner submits the attempt. There is no evaluation job yet; `evaluation` is `null`.
2. FE calls `GET /api/v1/speaking/evaluation-availability`, which returns `{ available }`. If unconfigured, the UI reports that it is not enabled.
3. The learner reads the data notice and consents. FE calls `POST /attempts/{id}/evaluation-consent` with `{ "accepted": true }`. The job transitions to `QUEUED`.
4. The worker processes each question that has a recording:
   - Downloads the recording from MinIO, uses `ffmpeg` to convert it to 16 kHz mono PCM WAV, and splits it into chunks of at most 25 seconds at the quietest point within the last 5 seconds of each chunk.
   - Sends each chunk to Azure Speech REST with pronunciation assessment.
   - Aggregates chunk scores weighted by duration and saves the question result immediately. Retries will not call Azure again.
5. The worker calls Gemini once for the entire attempt, attaching images for Part 2 questions, and retrieves `grammar`, `vocabulary`, `relevance` (0–100), feedback, and a suggested response. Transcripts are treated as untrusted data.
6. The server computes the score itself. FE polls `GET /attempts/{id}` every 5 seconds until `COMPLETED` or `FAILED`.

Audio splitting is required because Azure's REST API [limits pronunciation assessment to 30 seconds](https://docs.azure.cn/en-us/ai-services/speech-service/rest-speech-to-text-short), whereas Part 1 responses are 45 seconds and Part 5 responses are 60 seconds.
Transcoding is required because browsers record in WebM or MP4.

## Consent Conditions

`POST .../evaluation-consent`:

| Code | When |
| --- | --- |
| `400` | `accepted` is not `true` |
| `404` | Caller is not the attempt owner |
| `409` | Attempt has not been submitted |
| `409` | Still within the 15-second recording upload grace period |
| `409` | Attempt has no recordings |
| `409` | Backend is not configured |
| `409` | Anonymous session has already used an evaluation in the past 24 hours |
| `409` | Re-queuing a guest's `FAILED` job when the recording for an unrecognized question has expired from storage |
| `429` | Rate limit exceeded: 10 requests/minute per account; guests share 30 requests/hour per connection address |

Calling again, including two concurrent requests, returns the existing job if one already exists.
If the job is `FAILED`, it is re-queued.

## Scoring Formula

Source: `SpeakingEvaluationScoring`.
P is Azure's `PronScore`, which already includes accuracy, fluency, and prosody.

| Part | Quality 0–100 | Max Score |
| --- | --- | --- |
| 1 | min(0.7·P + 0.3·completeness; completeness) | 3 |
| 2 | 0.4·P + 0.2·grammar + 0.2·vocabulary + 0.2·relevance | 3 |
| 3, 4 | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 3 |
| 5 | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 5 |

```text
question score = round(quality / 100 × max score)
estimated score = round(total question score × 20 / total max score) × 10
```

- `completeness`: percentage of words in the passage read in the correct order.
- `relevance < 40`: that question is capped at 1 point.
- Transcript under 15 words in Part 2 or under 30 words in Part 5: that question is capped at 1 point. Parts 3 and 4 are exempt because short answers can still be complete.
- Question with no recording, no speech recognized, or an undecodable file: 0 points, not sent to Gemini.

This is a practice conversion proposed by the team, is not calibrated, and is not an ETS conversion table.

## Status, Retries, and Storage

`status`: `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, `FAILED`.

- Transient errors (quota, network, 5xx, malformed LLM structured output, `ffmpeg` failure for reasons other than a corrupted file): retry up to 3 times, waiting 1, 3, and 9 minutes. If `Retry-After` is larger, follow that value.
- Configuration errors (invalid key, missing `ffmpeg`): transition straight to `FAILED`. Missing `ffmpeg` uses code `AUDIO_CONVERTER_UNAVAILABLE`.
- Workers hold a 5-minute lease. When a lease expires, another worker takes over; each takeover counts as a retry. Exceeding 3 retries sets `FAILED` with code `LEASE_RETRIES_EXHAUSTED`. `version` prevents double completion.
- The API returns a safe `failureMessage` and does not expose raw provider errors or keys.

## Response Data

`evaluation` in `SpeakingAttemptDto`: `status`, `estimatedScore`, `summary`, `strengths`, `improvements`, `items`, `speechProvider`, `languageProvider`, `languageModel`, `rubricVersion`, `consentAcceptedAtUtc`, `queuedAtUtc`, `completedAtUtc`, `retryCount`, `failureMessage`.

Each entry in `items` (present only when `COMPLETED`): `position`, `part`, `recognitionStatus`, `transcript`, `accuracyScore`, `fluencyScore`, `completenessScore`, `prosodyScore`, `pronunciationScore`, `grammarScore`, `vocabularyScore`, `relevanceScore`, `rawScore`, `maxRawScore`, `feedback`, `suggestedResponse`, `wordIssues`.

`recognitionStatus`: `RECOGNIZED`, `NO_SPEECH`, `NO_RECORDING`. Current rubric: `toeic-speaking-v2`.

## Configuration

Do not commit keys.
Place them in `.env`, user-secrets, or deployment environment secrets.

| Variable | Default | Notes |
| --- | --- | --- |
| `AzureSpeech__Enabled` | `false` | |
| `AzureSpeech__Key`, `AzureSpeech__Region` | | Required when enabled |
| `AzureSpeech__Locale` | `en-US` | |
| `AzureSpeech__Endpoint` | | Optional |
| `AzureSpeech__FfmpegPath` | `ffmpeg` | |
| `AzureSpeech__ChunkSeconds` | `25` | |
| `AzureSpeech__RequestTimeoutSeconds` | `30` | |
| `Gemini__Enabled`, `Gemini__ApiKey` | | Shared with Writing |

The runtime image needs `ffmpeg`; the runtime Dockerfile already installs this tool.
Compose and `.env.example` include the variables `AZURE_SPEECH_ENABLED`, `AZURE_SPEECH_KEY`, `AZURE_SPEECH_REGION`, and `AZURE_SPEECH_LOCALE`; when running directly, `AZURE_SPEECH_FFMPEG_PATH` selects the executable.
The image was not built on the review machine because the Docker daemon was not running.

Each full attempt makes roughly 11–16 Azure requests and 1 Gemini request.

## Known Limitations

- `notespeaking.md` notes that Azure and Gemini have not yet been tested with real keys. This must be verified before the demo.
- Azure documentation is unclear on whether `ReferenceText` is required for unscripted mode. The code tries unscripted first; if it receives `400`, it falls back to using the transcript itself as the reference text.
- Weights have not been calibrated yet. Before real usage, there should be a teacher-graded sample set, similar to the Calibration section of [Writing](../writing/ai-evaluation.md#calibration-before-official-demo), before bumping the rubric version.
