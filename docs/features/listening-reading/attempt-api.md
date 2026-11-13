# Listening & Reading attempt API

> **Trạng thái:** Chưa có API lượt làm bài L&R trong backend hiện tại. Tài liệu này ghi ranh giới đã kiểm chứng và các quyết định còn thiếu; không định nghĩa endpoint giả định.

## Phần hiện có

Backend có `Exam`/`ExamRevision` cho Admin tạo và công bố đề, 200 câu cùng đáp án, media và `ScoreProfile` cho hai kỹ năng. Các route hiện tại nằm dưới `/api/v1/admin/exams`, `/api/v1/admin/exam-media`, `/api/v1/admin/score-profiles` và `/api/v1/admin/lr-exercise-bank`. Chúng yêu cầu policy Admin và trả dữ liệu quản trị, gồm `correctKey` ở response revision/bank.

## Phần chưa có

Chưa có controller/handler L&R công khai để học viên lấy danh sách đề đã publish, bắt đầu hoặc resume attempt, lưu đáp án, nộp bài, chấm điểm, xem kết quả, xem lại bài hoặc xem lịch sử. Chưa có bảng attempt L&R hay snapshot đề cho lượt làm. Vì vậy chưa có route, request/response, trạng thái hoặc mã lỗi ổn định để BFF tích hợp cho các thao tác đó.

Các màn hình L&R trong [screen inventory](../../ui-ux/screens-inventory.md) và yêu cầu phòng thi 120 phút trong [PRD](../../specs/prd-mvp.md) là đầu vào thiết kế, chưa chứng minh backend đã thực thi đồng hồ, tự nộp, khóa đáp án Listening, phát audio không pause/seek hay trả điểm 10–990.

## Điều kiện cần chốt trước khi viết contract learner

- Cách xác định chủ sở hữu attempt cho người đăng nhập và khách, cùng chính sách lưu lịch sử.
- Nguồn thời gian chuẩn, cách xử lý hết giờ/mất kết nối, và cơ chế khóa đáp án Listening khi phát audio.
- Cách phát media của revision workspace (audio theo block) và revision legacy (master audio) cho học viên, bảo vệ object storage private.
- Snapshot immutable của câu hỏi, đáp án và barem khi bắt đầu attempt; thời điểm được phép trả `correctKey`, transcript, giải thích và ảnh/audio.
- Quy tắc chấm theo từng Part, cách tra profile Listening/Reading và response kết quả.

Chỉ sau khi các quyết định này được xác nhận mới thêm endpoint và ví dụ payload. Không gọi API preview Admin từ client học viên vì response chứa đáp án.

## Frontend hiện tại

Kiểm tra source FE cho thấy không có route trang L&R dành cho học viên, không có route handler BFF learner và không có module client gọi attempt L&R. Hai route L&R hiện có đều thuộc Admin:

| Route | Mục đích |
| --- | --- |
| `/admin/exams` | Quản trị đề và ngân hàng bài tập. |
| `/admin/exams/reading/{examId}/{revisionId}` | Chỉnh sửa revision, nhận cả `correctKey` và metadata quản trị. |

Do đó không có luồng FE→BFF→BE cho attempt để mô tả ở thời điểm này. Khi triển khai learner, contract phải được chốt trước và tách hẳn dữ liệu public khỏi các DTO Admin; không suy ra contract từ UI Figma hoặc từ payload quản trị.
