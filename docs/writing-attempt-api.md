# Writing attempt API

Các endpoint dùng prefix `/api/v1/writing`. `GET /tests` và `GET /tests/{testId}` chỉ trả đề đã publish; không trả
model answer hay rubric của Admin. `GET /media/{assetId}` chỉ mở ảnh đang được một đề đã publish tham chiếu.

## Chủ sở hữu lượt làm

Request có JWT hợp lệ sẽ gắn lượt làm với `sub` của tài khoản. Với người chưa đăng nhập, client/BFF tạo UUID ngẫu nhiên
và gửi trong header `X-Writing-Anonymous-Session`; backend chỉ lưu SHA-256 của UUID. Không truyền định danh owner trong
body. Một attempt chỉ thuộc **một** trong hai loại owner và owner khác nhận `404` thay vì biết attempt có tồn tại.

## Đề và lịch sử

- `GET /tests?query=&pageNumber=1&pageSize=20`
- `GET /tests/{testId}`
- `GET /attempts`: tối đa 100 lượt gần nhất của owner hiện tại.

`POST /attempts` có body:

```json
{ "testId": "uuid", "mode": "FULL" }
```

`mode` là `FULL`, `PART_1`, `PART_2`, hoặc `PART_3`. Đề full giữ đúng thứ tự 5 câu Part 1, 2 câu Part 2, 1 câu Part 3;
mode theo Part chỉ lấy câu của Part đó.

## Làm bài và timer

- `GET /attempts/{attemptId}`: tải/resume attempt và tự chuyển trạng thái nếu deadline server đã qua.
- `PUT /attempts/{attemptId}/answers/{position}` body `{ "answer": "..." }`: autosave duy nhất câu hiện tại khi đang
  ở pha `ANSWERING`.
- `POST /attempts/{attemptId}/next`: khóa câu hiện tại và chuyển câu kế tiếp; chỉ dùng được ở `ANSWERING`.
- `POST /attempts/{attemptId}/submit`: idempotent, khóa câu hiện tại nếu còn đang làm và trả
  `SUBMITTED_AWAITING_EVALUATION`.

Response attempt luôn có `serverNowUtc`, `phaseEndsAtUtc`, `currentQuestionPosition`, `currentPhase`, và snapshot prompt.
Frontend đếm ngược từ `phaseEndsAtUtc`; backend là nguồn thời gian chuẩn. Deadline đã qua sẽ lần lượt chuyển
`PREPARING → ANSWERING → khóa câu → câu kế`, kể cả khi người dùng đóng tab lâu hơn tổng nhiều pha. Thời lượng cố định:
Part 1 là 30 giây chuẩn bị + 60 giây làm; Part 2 là 60 giây + 540 giây; Part 3 là 120 giây + 1680 giây.

Snapshot của `writing_attempt_questions` chỉ giữ prompt hiển thị cho User, không lưu `modelAnswer` hoặc `rubricNotes`.
Checkpoint này chưa gọi AI; bài đã nộp dừng tại `SUBMITTED_AWAITING_EVALUATION` để CP6 tạo job chấm bất đồng bộ.

Migration `AddWritingAttempts` tạo `writing_attempts` và `writing_attempt_questions`. Nó chỉ được thêm vào source; chưa
chạy `database update` trên Neon.
