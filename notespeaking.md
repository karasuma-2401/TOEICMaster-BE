# Ghi chú: chấm AI cho Speaking

Ngày: 30/10/2026 · Nhánh BE: `feature/speaking-evaluation`, tạo từ `dev` @ `171f307`. Nhánh FE: `feature/speaking-part-practice`, tạo từ `dev` @ `4416ec9`. Cả hai chưa commit, chưa push.

Nội dung gồm hai phần: chấm AI cho Speaking (mục 2) và luyện theo Part bằng từng bài tập lẻ (mục 7).

## 1. Migration mới

Trước khi làm, nhánh đã được cập nhật bằng migration mới nhất trên `dev`. Migration gần nhất trước đó là `20261028180220_AddLrExerciseBank`.

| Migration | Nội dung |
| --- | --- |
| `20261029192259_AddSpeakingPracticeAndAiEvaluation` | Tạo 2 bảng `speaking_evaluations` và `speaking_evaluation_items`, và sửa bảng `speaking_attempts` cho luyện theo Part. Không đụng bảng của module khác. |

Migration này thay cho `20261029180213_AddSpeakingAiEvaluation` ở bản trước. Bản đó chưa từng được áp dụng lên Neon, nên mình gỡ và gộp chung để nhóm chỉ phải chạy một migration.

**Thay đổi trên `speaking_attempts`:**

- `test_id` chuyển thành cho phép null.
- Thêm cột `exercise_id` (uuid, null được), FK → `speaking_exercises` với `RESTRICT`, có chỉ mục.
- Thêm CHECK `ck_speaking_attempts_source`: mỗi lượt làm có đúng một nguồn, hoặc `test_id` hoặc `exercise_id`.
- Khi rollback (`Down`), các lượt luyện lẻ (`test_id IS NULL`) bị xóa trước, rồi `test_id` mới được đặt lại thành bắt buộc.

**`speaking_evaluations`**: mỗi lượt làm có tối đa 1 job chấm. Các cột:

- Định danh và trạng thái: `id`, `attempt_id` (UNIQUE, FK → `speaking_attempts`, xóa theo cascade), `status`.
- Nhà cung cấp và rubric: `speech_provider`, `speech_locale`, `language_provider`, `language_model`, `rubric_version`.
- Thời điểm của job: `consent_accepted_at_utc`, `queued_at_utc`, `started_at_utc`, `lease_expires_at_utc`, `next_attempt_at_utc`, `completed_at_utc`.
- Kết quả: `retry_count`, `estimated_score`, `summary`, `strengths_json` và `improvements_json` (jsonb).
- Lỗi: `failure_code`, `failure_message`.
- Hệ thống: `created_at_utc`, `updated_at_utc`, `version` (concurrency token).

`status` chỉ nhận các giá trị `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, `FAILED`.

**`speaking_evaluation_items`**: khóa chính là `(evaluation_id, position)`, mỗi câu hỏi một dòng. Các cột:

- Thông tin câu: `part`, `recognition_status` (`RECOGNIZED`, `NO_SPEECH`, `NO_RECORDING`).
- Kết quả từ Azure: `transcript`, `audio_seconds`, `accuracy_score`, `fluency_score`, `completeness_score`, `prosody_score`, `pronunciation_score`, và `word_issues_json` (jsonb, danh sách từ phát âm sai).
- Kết quả từ LLM: `grammar_score`, `vocabulary_score`, `relevance_score`, `feedback`, `suggested_response`.
- Điểm câu: `raw_score`, `max_raw_score`.

Có CHECK constraint cho mọi enum và khoảng điểm, cùng chỉ mục `(status, next_attempt_at_utc)` và `lease_expires_at_utc` để worker tìm job.

Migration đã chạy Up → Down → Up thành công trên PostgreSQL 16 tạm, kể cả khi đã có lượt luyện lẻ.

**Đã áp dụng lên Neon** (30/10/2026) theo đồng ý của bạn. Hãy báo nhóm: ai pull nhánh này về **không cần** chạy lại `database update`. Backend Docker cũng đã được rebuild từ code hiện tại.

## 2. Cách chấm

Luồng chấm làm đúng theo hướng dẫn: STT của Azure Speech lấy transcript, bật pronunciation assessment để lấy các tiêu chí phát âm, rồi LLM chấm vocabulary và grammar.

1. **Learner đồng ý.** Learner gọi `POST /api/v1/speaking/attempts/{id}/evaluation-consent` với body `{ "accepted": true }`. Bước này bắt buộc vì giọng nói được gửi ra dịch vụ bên ngoài, giống quy tắc của Writing. Request tạo một job `QUEUED`.
2. **Worker nền xử lý từng câu có bản ghi:**
   - Tải bản ghi từ MinIO.
   - Dùng `ffmpeg` chuyển sang WAV PCM 16 kHz mono và cắt thành các đoạn ≤ 25 giây.
   - Gửi từng đoạn tới Azure Speech REST (short audio) với header `Pronunciation-Assessment`: `Dimension=Comprehensive`, `Granularity=Word`, bật prosody khi dùng en-US.
   - Nhận về transcript, AccuracyScore, FluencyScore, ProsodyScore, PronScore và danh sách từ phát âm sai. Điểm các đoạn được gộp theo thời lượng.
   - Câu Part 1 có thêm độ hoàn chỉnh (completeness): tỷ lệ từ của đoạn văn được đọc đúng thứ tự.
   - Kết quả từng câu được lưu ngay, nên khi phải thử lại thì không gọi Azure lần nữa.
3. **Gọi LLM (Gemini) một lần cho cả bài.** Đầu vào gồm transcript, đề, rubric riêng và điểm phát âm. LLM trả về grammar, vocabulary và relevance (0–100), nhận xét tiếng Việt, câu trả lời gợi ý, cùng tóm tắt, điểm mạnh và điểm cần cải thiện.
4. **Server tự tính điểm.** LLM không quyết định điểm cuối cùng.

**Vì sao phải cắt audio và dùng `ffmpeg`:**

- REST API của Azure chỉ chấm pronunciation cho audio **≤ 30 giây**, trong khi Part 1 cho trả lời 45 giây và Part 5 cho 60 giây.
- REST chỉ nhận WAV/OGG, trong khi Chrome ghi WebM và Safari ghi MP4.

**Công thức điểm** (`SpeakingEvaluationScoring`), với P = PronScore của Azure (đã gồm accuracy, fluency và prosody):

| Part | Chất lượng 0–100 | Điểm tối đa |
| --- | --- | --- |
| 1 (đọc to) | 0.7·P + 0.3·completeness | 3 |
| 2 (tả ảnh) | 0.4·P + 0.2·grammar + 0.2·vocabulary + 0.2·relevance | 3 |
| 3, 4 | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 3 |
| 5 (nêu ý kiến) | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 5 |

- **Điểm câu** = làm tròn(chất lượng / 100 × điểm tối đa).
- **Câu lạc đề:** nếu relevance < 40, câu được tối đa 1 điểm, dù nói trôi chảy đến đâu.
- **Câu không có bản ghi hoặc không nhận diện được giọng nói:** 0 điểm, và không gửi sang LLM.
- **Điểm ước tính** = tổng điểm câu / tổng điểm tối đa × 200, làm tròn tới bội số của 10.

Đây là điểm luyện tập, không phải điểm ETS chính thức. Trọng số trên là đề xuất của mình, nhóm có thể điều chỉnh.

**Thử lại khi lỗi** (giống Writing):

- Lỗi tạm thời (quota, mạng, 5xx, kết quả LLM không hợp lệ) được thử lại tối đa 3 lần, chờ 1, 3 rồi 9 phút.
- Lỗi cấu hình (sai key, thiếu `ffmpeg`) chuyển thẳng sang `FAILED`. Learner gọi lại endpoint consent để xếp hàng lại.
- Khách ẩn danh được chấm 1 lần mỗi 24 giờ.

## 3. API cho frontend

- **`POST /api/v1/speaking/attempts/{id}/evaluation-consent`**, body `{ "accepted": true }` → trả về `SpeakingAttemptDto`.
  - `400`: `accepted` khác `true`.
  - `404`: không phải chủ lượt làm.
  - `409` khi một trong các điều kiện sau xảy ra:
    - chưa nộp bài;
    - vẫn trong 15 giây ân hạn để tải bản ghi cuối;
    - lượt làm không có bản ghi nào;
    - backend chưa cấu hình chấm AI;
    - khách ẩn danh đã dùng lượt chấm trong 24 giờ qua.
  - Gọi lại khi job đã có thì trả về job hiện tại. Nếu job đang `FAILED` thì xếp hàng lại.
- **`GET /api/v1/speaking/attempts/{id}`**: có thêm trường `evaluation`, giá trị `null` cho tới khi learner đồng ý. FE polling endpoint này tới khi `status` là `COMPLETED` hoặc `FAILED`.
  - `evaluation` gồm `status`, `estimatedScore`, `summary`, `strengths`, `improvements`, `items`, `speechProvider`, `languageProvider`, `languageModel`, `rubricVersion`, `consentAcceptedAtUtc`, `queuedAtUtc`, `completedAtUtc`, `retryCount`, `failureMessage`.
  - Mỗi phần tử của `items` (chỉ có khi `COMPLETED`) gồm `position`, `part`, `recognitionStatus`, `transcript`, `accuracyScore`, `fluencyScore`, `completenessScore`, `prosodyScore`, `pronunciationScore`, `grammarScore`, `vocabularyScore`, `relevanceScore`, `rawScore`, `maxRawScore`, `feedback`, `suggestedResponse`, và `wordIssues` dạng `[{ word, accuracyScore }]`.
- **`GET /api/v1/speaking/attempts`**: mỗi phần tử có thêm `evaluationStatus` và `estimatedScore`.

Response cũ chỉ được thêm trường, không đổi trường nào. FE hiện chưa có màn hình cho phần này; mình không sửa repo FE.

## 4. Việc bạn cần làm

1. **Tạo Azure Speech resource** (Azure Portal → Speech service). Region nên chọn `southeastasia`, gói F0 miễn phí đủ để thử. Lấy **Key** và **Region**.
2. **Bật Gemini.** Chấm Speaking dùng chung key `Gemini` của Writing, nên cần `Gemini__Enabled=true` và `Gemini__ApiKey`. Nếu thiếu Azure hoặc Gemini, endpoint consent trả `409` "chưa được cấu hình".
3. **Cấu hình biến môi trường.** Mình **không sửa** các file dùng chung `docker-compose.yml`, `.env.example` và `appsettings.json`, nên cần thêm thủ công.
   - Trong `docker-compose.yml`, mục `backend.environment`:
     ```yaml
     - AzureSpeech__Enabled=${AZURE_SPEECH_ENABLED:-false}
     - AzureSpeech__Key=${AZURE_SPEECH_KEY:-}
     - AzureSpeech__Region=${AZURE_SPEECH_REGION:-southeastasia}
     - Gemini__Enabled=${GEMINI_ENABLED:-false}
     - Gemini__ApiKey=${GEMINI_API_KEY:-}
     ```
     Lưu ý: compose hiện cũng chưa truyền biến `GEMINI_*`, nên chấm AI của Writing trong Docker cũng chưa bật được.
   - Trong `.env.example`, thêm `AZURE_SPEECH_ENABLED=false`, `AZURE_SPEECH_KEY=`, `AZURE_SPEECH_REGION=southeastasia`.
   - Khi chạy bằng `dotnet run`, có thể dùng `dotnet user-secrets` hoặc biến môi trường cùng tên.
   - Các tùy chọn khác đều có giá trị mặc định: `AzureSpeech__Locale` (en-US), `AzureSpeech__Endpoint`, `AzureSpeech__FfmpegPath` (ffmpeg), `AzureSpeech__ChunkSeconds` (25), `AzureSpeech__RequestTimeoutSeconds` (30).
4. **Cài `ffmpeg` vào image runtime.** Đây là `Dockerfile` dùng chung nên mình chưa sửa. Thêm vào stage `runtime`, trước dòng `USER $APP_UID`:
   ```dockerfile
   USER root
   RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
   ```
   Mình đã thử trên chính base `mcr.microsoft.com/dotnet/aspnet:10.0` (Ubuntu 24.04, ffmpeg 6.1.1): cài được và chuyển đúng audio WebM, Ogg, MP4. Image sẽ lớn thêm khoảng vài chục MB. Nếu chạy `dotnet run` trên máy dev thì cài bằng `brew install ffmpeg`. Thiếu `ffmpeg` thì job chuyển `FAILED` với lỗi `AUDIO_CONVERTER_UNAVAILABLE`.
5. ~~Áp dụng migration lên Neon~~: đã làm, xem mục 1.
6. **Thử với key thật.** Tài liệu Azure không thống nhất việc `ReferenceText` có bắt buộc hay không khi chấm unscripted. Code gửi chế độ unscripted trước. Nếu Azure trả `400`, code tự nhận diện lời nói rồi chấm lại với chính transcript làm văn bản tham chiếu. Cần thử với một bản ghi thật để xác nhận điểm trả về hợp lý.
7. **Chi phí.** Mỗi bài Full gọi Azure khoảng 11–16 request (do cắt đoạn) và gọi Gemini 1 lần. Pronunciation assessment được tính phí như speech-to-text.

## 5. Phạm vi thay đổi

- **File mới, tất cả là Speaking:**
  - Domain: `SpeakingEvaluation.cs`.
  - Application: thư mục `Application/Speaking/Evaluations/`.
  - Infrastructure: thư mục `Infrastructure/Speaking/Evaluations/`, file `SpeakingEvaluationConfiguration.cs`, file `EfSpeakingEvaluationRepository.cs`, và migration.
  - Test: các file test mới.
- **Sửa file Speaking có sẵn:** `SpeakingAttemptsController`, `SpeakingAttemptContracts`, `SpeakingAttemptHandlers`, `SpeakingShared`, `SpeakingConfiguration`, các test Speaking.
- **Điểm chạm file dùng chung, ở mức tối thiểu:**
  - `DependencyInjection.cs`: 2 dòng (`using` và `services.AddSpeakingEvaluation(configuration)`).
  - `MediatorPipelineTests.cs`: 3 dòng đăng ký fake cho Speaking.
  - `ToeicDbContextModelSnapshot.cs`: do EF tự sinh, chỉ thêm 2 bảng.
- Đọc nhưng **không sửa** `GeminiOptions` của Writing. Không đụng repo FE.

## 6. Kiểm thử

- `dotnet test`: 280/280 pass (trước khi làm có 236 test). Có 44 test mới:
  - công thức điểm và độ hoàn chỉnh khi đọc;
  - consent: điều kiện, giới hạn khách ẩn danh, owner;
  - xử lý job Full 11 câu với câu không có bản ghi và câu im lặng;
  - chặn câu lạc đề;
  - retry không gọi lại Azure;
  - lỗi vĩnh viễn rồi xếp hàng lại;
  - kết quả LLM thiếu câu;
  - định dạng request Azure (URL, header, base64 JSON), gộp đoạn theo thời lượng, đọc cả hai dạng response, đường dự phòng khi gặp `400`, phân loại lỗi;
  - request Gemini (key nằm trong header, không nằm trong URL);
  - luồng HTTP đầy đủ: consent → worker nền → polling.
- **Trên PostgreSQL 16 thật** (test tạm, đã xóa sau khi chạy):
  - Hai worker giành cùng một job thì chỉ một bên lấy được.
  - Tạo job trùng bị chỉ mục UNIQUE chặn và trả `409`.
  - Hết lease thì worker khác tiếp quản được.
  - Kết quả từng câu được lưu trước khi LLM lỗi.
  - Khi thử lại thì hoàn tất mà không gọi Azure thêm lần nào.
  - Xóa lượt làm thì job và các câu bị xóa theo cascade.
- **`ffmpeg` thật** (image tạm từ base runtime): WebM 45 giây → 25 + 20 giây, MP4 60 giây → 25 + 25 + 10 giây, Ogg 10 giây → 1 đoạn. Mọi đoạn là WAV 16 kHz mono; file hỏng trả về rỗng.
- Đã kiểm tra format toàn solution, build Release với 0 warning, và `has-pending-model-changes` không còn thay đổi nào.
- **Chưa kiểm tra được:** gọi Azure Speech và Gemini thật, vì chưa có key.

## 7. Luyện theo Part

Trước đây, thẻ "Luyện theo Part" chỉ mở lại đề Full ở chế độ một Part, và không có thẻ nào bấm được khi chưa có đề công bố. Giờ mỗi bài tập đã công bố trong ngân hàng đều luyện riêng được, không cần đề.

**API mới (BE):**

- `GET /api/v1/speaking/exercises?part=1..5&pageNumber&pageSize`: danh sách bài tập `PUBLISHED`, không chứa rubric hay bài mẫu. Mỗi phần tử gồm `{ id, code, part, topic, difficulty, questionCount, durationSeconds, updatedAtUtc }`.
- `GET /api/v1/speaking/exercises/{id}`: trả về 1 bài như trên, `404` nếu bài không ở trạng thái công bố.
- `POST /api/v1/speaking/attempts` nhận thêm dạng body `{ "exerciseId": "..." }`. Lượt làm có `mode = PART_n` theo Part của bài.
  - Phải gửi đúng một trong `testId` hoặc `exerciseId`, nếu không trả `400`.
  - Nội dung bài được snapshot lúc bắt đầu, nên admin sửa bài sau đó không làm đổi lượt đang làm.
- `SpeakingAttemptDto` và phần tử danh sách lượt làm có thêm `exerciseId`, còn `testId` có thể `null`. Với lượt luyện lẻ, `testCode` là mã bài và `testTitle` có dạng `Luyện Part n · <chủ đề>`.
- Ảnh Part 2 của bài đã công bố được phục vụ qua `GET /api/v1/speaking/media/{id}`. Bài bị lưu trữ thì ảnh không còn được phục vụ, trừ khi ảnh nằm trong một đề đã công bố.

**FE:**

- Trang mới `/speaking/parts/[1-5]`: danh sách bài của Part, gồm mã, chủ đề, độ khó, số câu và thời lượng. Part không hợp lệ trả `404`.
- Trang mới `/speaking/exercises/[id]/practice`: kiểm tra micro rồi bắt đầu lượt luyện, sau đó dùng lại màn hình làm bài và xem lại hiện có.
- Thẻ Part ở `/speaking` luôn trỏ tới `/speaking/parts/n`.
- Màn hình xem lại: "Luyện lại" mở lại đúng bài; "Chọn bài khác" quay về danh sách của Part.
- BFF `/api/speaking/*` cho phép thêm 2 route `exercises`.

**Dữ liệu mẫu trên Neon** (tạo qua API admin, mọi mã có tiền tố `SAMPLE-`):

- 11 bài tập `PUBLISHED`: `SAMPLE-P1-001..003`, `SAMPLE-P2-001..002`, `SAMPLE-P3-001..002`, `SAMPLE-P4-001..002`, `SAMPLE-P5-001..002`.
- Đề `SAMPLE-FULL-01` "Đề mẫu TOEIC Speaking 01", đủ 11 câu, đã công bố.
- **Ảnh Part 2 nằm trong MinIO trên máy của Jin**, còn bản ghi trong DB thì dùng chung. Thành viên khác mở bài Part 2 sẽ không tải được ảnh. Muốn dùng chung thì mỗi người upload lại ảnh qua trang admin, hoặc nhóm chuyển sang dùng một kho MinIO chung.
- Muốn dọn: lưu trữ các bài `SAMPLE-` trong trang admin. Đề đã công bố thì hiện chưa có API gỡ.

**Kiểm thử thêm:**

- BE: 286/286 test pass, gồm 6 test mới cho luyện theo Part.
- Trên PostgreSQL 16 tạm: luồng luyện theo Part chạy đúng, CHECK `ck_speaking_attempts_source` chặn lượt làm không có nguồn, và rollback chạy được khi đã có lượt luyện lẻ.
- FE: format, lint, typecheck sạch; 221/221 test pass.
- Chạy thật với backend mới và DB Neon, qua BFF của FE:
  - danh sách đề và bài tập của từng Part;
  - luyện lẻ Part 2 (ảnh JPEG trả `200`) và Part 4 (câu hỏi bị che đúng);
  - làm Full đủ 11 câu;
  - lịch sử lượt làm.
- Các trang `/speaking`, `/speaking/parts/3`, `/speaking/exercises/{id}/practice` render đúng nội dung. `/admin` chuyển hướng tới trang đăng nhập khi chưa đăng nhập admin.

## 8. Luồng chấm AI trên giao diện

Trước bản này, FE chưa có giao diện nào cho chấm AI Speaking: màn hình xem lại vẫn ghi "sẽ được bổ sung ở giai đoạn sau", và BFF chưa cho phép route `evaluation-consent`. Giờ luồng như sau:

1. Learner làm xong và nộp bài. Màn hình **xem lại** (`/speaking/attempts/{id}`) có thêm khung **"Chấm điểm bằng AI"**, nằm dưới phần thống kê.
2. Khung tự gọi `GET /api/speaking/evaluation-availability` (endpoint BE mới, trả về `{ available }`).
   - **Chưa cấu hình** Azure Speech hoặc Gemini: hiện "Chấm AI cho Speaking chưa được bật trên máy chủ", và không có nút nào.
   - **Đã cấu hình:** hiện giải thích 3 bước, gồm Azure Speech chuyển giọng nói thành văn bản và chấm phát âm, Gemini chấm ngữ pháp, từ vựng, mức đúng trọng tâm, rồi hệ thống tổng hợp điểm /200. Kèm theo là ô đồng ý và nút **"Đồng ý và chấm bài"**. Nút bị khóa khi chưa tick ô, khi bản ghi còn đang tải lên, hoặc khi lượt làm không có bản ghi nào.
3. Bấm nút sẽ gọi `POST .../evaluation-consent`. Khung chuyển sang **"Đã xếp hàng chờ chấm"** hoặc **"AI đang nghe và chấm bài nói"**, và tự polling `GET` lượt làm mỗi 5 giây. Learner có thể rời trang; mở lại lượt làm sẽ thấy đúng trạng thái.
4. Khi job **hoàn tất**, báo cáo hiện ra:
   - Điểm ước tính /200, nhận xét tổng quan, điểm mạnh và điểm cần cải thiện.
   - Từng câu: điểm `x/3` hoặc `x/5`, đoạn "AI nghe được" (transcript), và các điểm 0–100 gồm Phát âm tổng, Độ chính xác, Trôi chảy, Ngữ điệu, Đọc đủ đoạn (Part 1), Ngữ pháp, Từ vựng, Đúng trọng tâm. Kèm theo là các từ cần luyện phát âm, nhận xét và câu trả lời gợi ý.
5. Khi job **thất bại**, hiện lý do và nút **"Thử chấm lại"**. Khi bị từ chối (`409`), hiện đúng thông báo từ BE. Ví dụ, nếu bấm trong vòng 15 giây sau khi nộp bài, khung báo "Bản ghi cuối vẫn đang được tải lên".

**Để thấy luồng chạy thật trên UI**, cần làm các bước ở mục 4: Azure Speech key và region, `Gemini__Enabled=true` cùng `Gemini__ApiKey` được truyền vào container, và `ffmpeg` trong image. Thiếu bất kỳ bước nào thì UI dừng ở thông báo "chưa được bật" hoặc job báo `FAILED` kèm lý do.

**Kiểm thử:** FE có 230/230 test pass. Phần mới gồm parser kết quả chấm, các trạng thái của khung chấm (chưa bật, đồng ý rồi xếp hàng, bị từ chối, báo cáo hoàn tất, thử lại khi lỗi) và 2 route BFF mới. BE có thêm assert cho `evaluation-availability` trong test HTTP.
