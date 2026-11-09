# AI evaluation cho TOEIC Speaking

Speaking chấm theo hàng đợi bền vững. Bài nộp và bản ghi luôn được lưu trước, nên lỗi nhà cung cấp, hết quota hay timeout không làm mất bài. Điểm luôn được gọi là **điểm ước tính bởi AI (0–200, bước 10)**, không phải điểm ETS.

## Luồng sử dụng

1. Học viên nộp bài. Chưa có job chấm nào; `evaluation` là `null`.
2. FE gọi `GET /api/v1/speaking/evaluation-availability`, trả `{ available }`. Nếu chưa cấu hình, UI báo chưa bật.
3. Học viên đọc thông báo dữ liệu và đồng ý. FE gọi `POST /attempts/{id}/evaluation-consent` với `{ "accepted": true }`. Job chuyển sang `QUEUED`.
4. Worker xử lý từng câu có bản ghi:
   - Tải bản ghi từ MinIO, dùng `ffmpeg` chuyển sang WAV PCM 16 kHz mono và cắt đoạn tối đa 25 giây, tại điểm yên tĩnh nhất trong 5 giây cuối của đoạn.
   - Gửi từng đoạn đến Azure Speech REST kèm pronunciation assessment.
   - Gộp điểm các đoạn theo thời lượng và lưu kết quả câu ngay. Khi thử lại sẽ không gọi Azure lần nữa.
5. Worker gọi Gemini một lần cho cả bài, gửi kèm ảnh của các câu Part 2, lấy `grammar`, `vocabulary`, `relevance` (0–100), nhận xét và câu gợi ý. Transcript được coi là dữ liệu không tin cậy.
6. Server tự tính điểm. FE gọi `GET /attempts/{id}` mỗi 5 giây tới khi `COMPLETED` hoặc `FAILED`.

Phải cắt đoạn vì REST của Azure [giới hạn pronunciation assessment ở 30 giây](https://docs.azure.cn/en-us/ai-services/speech-service/rest-speech-to-text-short), trong khi Part 1 trả lời 45 giây và Part 5 trả lời 60 giây. Phải chuyển định dạng vì trình duyệt ghi WebM hoặc MP4.

## Điều kiện đồng ý

`POST .../evaluation-consent`:

| Mã | Khi nào |
| --- | --- |
| `400` | `accepted` khác `true` |
| `404` | Không phải chủ lượt làm |
| `409` | Chưa nộp bài |
| `409` | Còn trong 15 giây ân hạn tải bản ghi |
| `409` | Lượt làm không có bản ghi nào |
| `409` | Backend chưa cấu hình |
| `409` | Phiên ẩn danh đã dùng lượt chấm trong 24 giờ qua |
| `409` | Xếp lại job `FAILED` của khách khi bản ghi của câu chưa nhận diện đã hết hạn lưu trữ |
| `429` | Vượt giới hạn: 10 lần/phút cho mỗi tài khoản; khách dùng chung 30 lần/giờ theo địa chỉ kết nối |

Gọi lại, kể cả hai request đồng thời, khi đã có job thì trả job hiện tại. Nếu job đang `FAILED` thì xếp hàng lại.

## Công thức điểm

Nguồn: `SpeakingEvaluationScoring`. P là `PronScore` của Azure, đã gồm accuracy, fluency và prosody.

| Part | Chất lượng 0–100 | Điểm tối đa |
| --- | --- | --- |
| 1 | min(0,7·P + 0,3·completeness; completeness) | 3 |
| 2 | 0,4·P + 0,2·grammar + 0,2·vocabulary + 0,2·relevance | 3 |
| 3, 4 | 0,3·P + 0,2·grammar + 0,2·vocabulary + 0,3·relevance | 3 |
| 5 | 0,3·P + 0,2·grammar + 0,2·vocabulary + 0,3·relevance | 5 |

```text
điểm câu = round(chất lượng / 100 × điểm tối đa)
điểm ước tính = round(tổng điểm câu × 20 / tổng điểm tối đa) × 10
```

- `completeness`: phần trăm từ của đoạn văn được đọc đúng thứ tự.
- `relevance < 40`: câu đó tối đa 1 điểm.
- Transcript dưới 15 từ ở Part 2 hoặc dưới 30 từ ở Part 5: câu đó tối đa 1 điểm. Part 3, 4 không áp dụng vì câu trả lời ngắn vẫn có thể đủ ý.
- Câu không có bản ghi, không nhận ra giọng nói hoặc file không giải mã được: 0 điểm, không gửi sang Gemini.

Đây là quy đổi luyện tập do nhóm đề xuất, chưa được hiệu chỉnh và không phải bảng quy đổi của ETS.

## Trạng thái, thử lại, lưu trữ

`status`: `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, `FAILED`.

- Lỗi tạm thời (quota, mạng, 5xx, LLM trả sai cấu trúc, `ffmpeg` lỗi vì lý do khác file hỏng): thử lại tối đa 3 lần, chờ 1, 3, 9 phút. Nếu `Retry-After` lớn hơn thì theo giá trị đó.
- Lỗi cấu hình (sai key, thiếu `ffmpeg`): chuyển thẳng `FAILED`. Thiếu `ffmpeg` có mã `AUDIO_CONVERTER_UNAVAILABLE`.
- Worker có lease 5 phút. Hết lease thì worker khác tiếp quản, mỗi lần tiếp quản tính là một lần thử lại; vượt quá 3 lần thì `FAILED` với mã `LEASE_RETRIES_EXHAUSTED`. `version` chống hoàn thành hai lần.
- API trả `failureMessage` an toàn, không trả lỗi gốc của nhà cung cấp hay key.

## Dữ liệu trả về

`evaluation` trong `SpeakingAttemptDto`: `status`, `estimatedScore`, `summary`, `strengths`, `improvements`, `items`, `speechProvider`, `languageProvider`, `languageModel`, `rubricVersion`, `consentAcceptedAtUtc`, `queuedAtUtc`, `completedAtUtc`, `retryCount`, `failureMessage`.

Mỗi phần tử `items` (chỉ có khi `COMPLETED`): `position`, `part`, `recognitionStatus`, `transcript`, `accuracyScore`, `fluencyScore`, `completenessScore`, `prosodyScore`, `pronunciationScore`, `grammarScore`, `vocabularyScore`, `relevanceScore`, `rawScore`, `maxRawScore`, `feedback`, `suggestedResponse`, `wordIssues`.

`recognitionStatus`: `RECOGNIZED`, `NO_SPEECH`, `NO_RECORDING`. Rubric hiện tại: `toeic-speaking-v2`.

## Cấu hình

Không commit key. Đặt trong `.env`, user-secrets hoặc secret của môi trường triển khai.

| Biến | Mặc định | Ghi chú |
| --- | --- | --- |
| `AzureSpeech__Enabled` | `false` | |
| `AzureSpeech__Key`, `AzureSpeech__Region` | | Bắt buộc khi bật |
| `AzureSpeech__Locale` | `en-US` | |
| `AzureSpeech__Endpoint` | | Tùy chọn |
| `AzureSpeech__FfmpegPath` | `ffmpeg` | |
| `AzureSpeech__ChunkSeconds` | `25` | |
| `AzureSpeech__RequestTimeoutSeconds` | `30` | |
| `Gemini__Enabled`, `Gemini__ApiKey` | | Dùng chung với Writing |

Image chạy cần có `ffmpeg`. Hiện `ffmpeg` và các biến trên chỉ được khai báo trong `docker-compose.override.yml`, file không nằm trong git. `Dockerfile` và `docker-compose.yml` chưa có, nên image dựng từ repo sẽ không chấm được (báo cáo OPS-01).

Khi chạy `dotnet run`, thêm vào `.env` đúng tên `AzureSpeech__Enabled`, `AzureSpeech__Key`, `AzureSpeech__Region`, và cài `ffmpeg` (`brew install ffmpeg`). Tên `AZURE_SPEECH_*` chỉ có tác dụng qua `docker-compose.override.yml`; `DotEnvConfiguration` chưa ánh xạ các tên này.

Mỗi bài đầy đủ gọi Azure khoảng 11–16 request và Gemini 1 lần.

## Giới hạn đã biết

- `notespeaking.md` ghi chưa gọi thử Azure và Gemini bằng key thật. Cần xác nhận lại trước khi demo.
- Tài liệu Azure chưa rõ `ReferenceText` có bắt buộc với chế độ unscripted hay không. Code thử unscripted trước; nếu nhận `400` thì dùng chính transcript làm văn bản tham chiếu.
- Trọng số chưa được hiệu chỉnh. Trước khi dùng thật, nên có bộ mẫu đã được giáo viên chấm, tương tự mục Calibration của [Writing](../writing/ai-evaluation.md#calibration-trước-demo-chính-thức), rồi mới nâng phiên bản rubric.
