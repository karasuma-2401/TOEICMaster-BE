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
| GET | `/api/v1/exam-attempts/{attemptId}/review?filter=All` | Xem đáp án/giải thích sau khi nộp |
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

## Media

Attempt DTO hiện trả `MasterAudioObjectKey` cho revision legacy nhưng chưa cung cấp URL phát media an toàn cho learner. Media workspace audio theo block cũng chưa được đưa vào sheet DTO learner. Do đó không được coi phần audio player là đã tích hợp hoàn chỉnh; cần bổ sung endpoint cấp presigned GET URL có kiểm tra quyền sở hữu attempt và phase trước khi bật phát audio production.

## Frontend

FE learner sử dụng BFF `/api/lr/[...segments]`, kiểm tra same-origin với mutation, lấy access token từ HttpOnly session cookie, forward đến `/api/v1/*` và đặt `Cache-Control: no-store`. Không gọi Admin preview API từ learner client.
