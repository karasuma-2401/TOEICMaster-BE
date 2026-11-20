# Listening & Reading learner API

> **Trạng thái:** Backend đã có learner attempt API trong `ExamAttemptsController`; các endpoint dưới đây yêu cầu Bearer token và chỉ truy cập lượt thi của người dùng hiện tại. FE learner tích hợp qua Next.js BFF `/api/lr/*`.

## Endpoint đã triển khai

| Method | Endpoint | Chức năng |
| --- | --- | --- |
| GET | `/api/v1/exams/lr` | Danh sách revision Published mới nhất theo đề, không trả đáp án đúng |
| GET | `/api/v1/exams/{examId}/lr-overview` | Thông tin đề và activeAttemptId |
| POST | `/api/v1/exams/{examId}/attempts` | Tạo lượt thi hoặc resume lượt đang làm |
| GET | `/api/v1/exam-attempts/{attemptId}` | Tải snapshot đề, đáp án đã lưu, trạng thái phase và thời gian còn lại |
| POST | `/api/v1/exam-attempts/{attemptId}/start-reading` | Chuyển sang Reading và khóa Listening |
| PUT | `/api/v1/exam-attempts/{attemptId}/answers` | Autosave tối đa 200 câu |
| PUT | `/api/v1/exam-attempts/{attemptId}/questions/{questionId}/flag` | Bật/tắt cờ một câu |
| POST | `/api/v1/exam-attempts/{attemptId}/submit` | Nộp và chấm điểm |
| GET | `/api/v1/exam-attempts/{attemptId}/result` | Xem kết quả sau khi nộp |
| GET | `/api/v1/exam-attempts/{attemptId}/review?filter=All` | Xem đáp án/giải thích sau khi nộp (`audioAssetId`, `audioDurationMs` cho Part 1-4) |
| GET | `/api/v1/exam-attempts/{attemptId}/listening-audio-urls` | Danh sách presigned GET URL cho toàn bộ block Listening Part 1-4 theo thứ tự (`items`) |
| GET | `/api/v1/exam-attempts/{attemptId}/audio/{assetId}` | Presigned GET URL cho một block audio của lượt làm (`409` khi Listening đã khóa) |
| GET | `/api/v1/exam-attempts/{attemptId}/images/{assetId}` | Presigned GET URL cho ảnh của lượt làm |
| GET | `/api/v1/exam-attempts/history` | Lịch sử lượt thi |
| POST | `/api/v1/exam-attempts/{attemptId}/abandon` | Hủy lượt đang làm |

## Quy tắc bảo mật và nghiệp vụ

- User ID luôn lấy từ claim đã xác thực; client không gửi UserId.
- Response attempt đang làm không bao giờ chứa `correctKey`, explanation hoặc translation.
- Attempt gắn với revision và score profile tại thời điểm bắt đầu; kết quả được chấm bằng barem đã gắn.
- Server là nguồn thời gian duy nhất, giới hạn mặc định 120 phút. Client chỉ hiển thị đồng hồ dựa trên RemainingSeconds.
- Sau khi bắt đầu Reading, Listening không thể sửa hoặc gắn cờ.
- Câu chưa trả lời tính là skipped; chấm điểm chỉ xảy ra khi submit hoặc hết hạn.
- Review/result chỉ đọc được khi attempt đã Completed.
- Đáp án phải hợp lệ theo lựa chọn của câu; Part 2 chỉ chấp nhận A–C.
- Autosave chấp nhận grace period 5 giây sau deadline để xử lý độ trễ mạng.

## Luyện tập Listening & Reading

Learner có thể dùng các endpoint `/api/v1/lr-practice` để luyện từng item Published mà không cần mở một exam attempt.

`GET /api/v1/lr-practice/parts/{part}/items` và `GET /api/v1/lr-practice/items/{id}` là anonymous và chỉ trả nội dung không có đáp án.
`POST /api/v1/lr-practice/items/{id}/submit` và `GET /api/v1/lr-practice/parts/{part}/statistics` yêu cầu Bearer token.
Submit lưu snapshot đáp án và statistics chỉ gồm dữ liệu của user trong JWT.
Payload sai, câu hỏi không thuộc item, key trùng hoặc key không tồn tại trả `400 application/problem+json`; item không tồn tại hoặc chưa Published trả `404`.

## Media

Sheet DTO (`ExamAttemptSheetDto`) không còn `MasterAudioObjectKey`; mỗi `ExamBlockSheetDto` trả `audioAssetId` và `audioDurationMs` (ms) cho Part 1-4.
Review DTO (`ExamAttemptReviewBlockDto`) trả `audioAssetId` và `audioDurationMs` cho Part 1-4 (`null` cho Part 5-7).
Trong giai đoạn Listening (`InProgress` và `IsListeningLocked == false`), learner gọi `GET /api/v1/exam-attempts/{attemptId}/listening-audio-urls` để nhận danh sách presigned GET URL (hạn 3 giờ) cho toàn bộ block Listening Part 1-4 trong một lần gọi.
Sau khi chuyển sang Reading (`IsListeningLocked == true`) hoặc khi attempt không ở trạng thái `InProgress`, endpoint playlist và endpoint `GET .../audio/{assetId}` trong lúc làm bài đều trả `409 Conflict`.
Khi attempt đã `Completed`, learner vẫn gọi được `GET .../audio/{assetId}` trên màn Review.
Endpoint legacy `GET /api/v1/exam-attempts/{attemptId}/audio` đã được loại bỏ.

## Frontend

FE learner sử dụng BFF `/api/lr/[...segments]`, kiểm tra same-origin với mutation, lấy access token từ HttpOnly session cookie, forward đến `/api/v1/*` và đặt `Cache-Control: no-store`. Không gọi Admin preview API từ learner client.
