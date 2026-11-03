# Speaking attempt API

Các endpoint dùng prefix `/api/v1/speaking`, mở cho cả người dùng chưa đăng nhập. Mọi response có `Cache-Control: no-store`. Browser gọi qua BFF same-origin `/api/speaking/*`.

## Chủ sở hữu lượt làm

Request có JWT hợp lệ gắn lượt làm với `sub`. Nếu chưa đăng nhập, cần gửi UUID trong header `X-Speaking-Anonymous-Session`; backend chỉ lưu SHA-256 của giá trị này. Một lượt làm thuộc đúng một loại chủ sở hữu. Người khác nhận `404`.

## Đề và bài luyện

Các response này không chứa rubric hay bài mẫu.

- `GET /tests?query=&pageNumber=1&pageSize=20`: chỉ đề đã công bố.
- `GET /tests/{testId}`: gồm `parts: [{ part, questionCount, durationSeconds }]`.
- `GET /exercises?part=1&pageNumber=1&pageSize=20`: bài `PUBLISHED` để luyện lẻ.
- `GET /exercises/{exerciseId}`: `{ id, code, part, topic, difficulty, questionCount, durationSeconds, updatedAtUtc }`.
- `GET /media/{assetId}`: chỉ ảnh của bài hoặc đề đã công bố.

Giá trị `durationSeconds` chuẩn theo Part: P1 180, P2 150, P3 69, P4 114, P5 105.

## Bắt đầu lượt làm

`POST /attempts` nhận đúng một trong hai dạng:

```json
{ "testId": "uuid", "mode": "FULL" }
```

```json
{ "exerciseId": "uuid" }
```

`mode` là `FULL` hoặc `PART_1` đến `PART_5`. Với bài luyện lẻ, mode lấy theo Part của bài. Các câu được đánh số lại từ 1 và nội dung được snapshot lúc tạo.

> **Thời điểm gọi:** đồng hồ bắt đầu ngay khi request này thành công. Theo [ADR-003](../../decisions/003-speaking-microphone-session.md), FE phải có micro sẵn sàng **trước** khi gọi.

## Làm bài và đồng hồ

- `GET /attempts`: lịch sử của chủ sở hữu hiện tại; có `recordedCount`, `evaluationStatus`, `estimatedScore`.
- `GET /attempts/{attemptId}`: tải hoặc tiếp tục. Tự chuyển qua mọi pha đã quá hạn.
- `POST /attempts/{attemptId}/next`: kết thúc pha hiện tại. Ở pha trả lời của câu cuối, request này nộp bài.
- `POST /attempts/{attemptId}/submit`: nộp bài, idempotent.

Thứ tự pha của từng câu:

```text
READING (nếu có) → PREPARING (nếu có) → RESPONDING → khóa câu → câu kế tiếp
Sau câu cuối: status = SUBMITTED; currentQuestionPosition, currentPhase, phaseEndsAtUtc = null
```

FE đếm ngược từ `phaseEndsAtUtc`, có tính độ lệch theo `serverNowUtc`. Backend là nguồn thời gian. Hết hạn thì FE gọi `GET` để lấy trạng thái mới.

Với Part 3 và 4, phần tử chưa tới lượt trong `content.questions` là `null`. Part 4 cũng trả `null` cho câu hiện tại khi đang `READING`.

Các trường của response: `id`, `testId`, `exerciseId`, `testCode`, `testTitle`, `mode`, `status`, `currentQuestionPosition`, `currentPhase`, `phaseEndsAtUtc`, `serverNowUtc`, `createdAtUtc`, `submittedAtUtc`, `questions`, `reviewQuestions`, `evaluation`.

Với lượt luyện lẻ, `testId` là `null`, `testCode` là mã bài, và `testTitle` có dạng `Luyện Part n · <chủ đề>`.

**Đích (D2):** ẩn nút bỏ qua ở pha đọc và chuẩn bị của đề `FULL`. Việc này chỉ cần sửa FE; nếu muốn chặn ở server thì cần thêm kiểm tra mode trong `Next`.

## Bản ghi

`PUT /attempts/{attemptId}/recordings/{position}`: `multipart/form-data`, gồm `file` và `durationSeconds`.

- Định dạng: WebM, Ogg hoặc MP4, kiểm tra theo nội dung file.
- Dung lượng tối đa của mỗi câu: `min(4 MiB, responseSeconds × 32 KiB + 256 KiB)`.
- Chỉ nhận trong pha trả lời của câu đó, hoặc trong 15 giây sau khi câu bị khóa.
- Tải lại cho cùng câu trong thời gian này sẽ thay bản ghi cũ.
- Giới hạn 30 request mỗi phút theo phiên ẩn danh, token hoặc địa chỉ IP.

`GET /attempts/{attemptId}/recordings/{position}`: chỉ chủ lượt làm mới nghe được.

Bản ghi của khách bị xóa sau 30 ngày. Bản ghi được giữ trên server, không chỉ trong tab; phải thông báo đúng điều này trên UI (MIC-11).

## Sau khi nộp

`reviewQuestions` chứa rubric và bài mẫu theo câu. Xem [AI Evaluation](./ai-evaluation.md) cho phần chấm điểm.

Route FE `/speaking/attempts/{id}` dùng chung cho làm bài và xem lại. Khi `status = SUBMITTED`, FE không được xin quyền micro.

## Lỗi

| Mã | Khi nào |
| --- | --- |
| `400` | Body sai, bản ghi rỗng hoặc sai định dạng |
| `401` | Thiếu cả JWT lẫn UUID phiên ẩn danh hợp lệ |
| `404` | Không tìm thấy hoặc không phải chủ lượt làm |
| `409` | Đề chưa sẵn sàng; hết thời gian tải bản ghi |
| `413` | Bản ghi quá dung lượng |
| `429` | Tải bản ghi quá nhanh |
