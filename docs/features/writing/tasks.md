# Checklist module TOEIC Writing

Tham chiếu: [đặc tả](spec.md), [Admin API](admin-api.md), [Attempt API](attempt-api.md) và [AI Evaluation](ai-evaluation.md).

## Phạm vi trong source

- Đề nhập trực tiếp độc lập hoặc import Excel; giữ khả năng đọc đề cũ tham chiếu ngân hàng.
- Ngân hàng bài tập lẻ CRUD, import Excel và luyện theo Part không giới hạn thời gian.
- `modelAnswer` tùy chọn, chỉ dùng nội bộ; AI chỉ nhận bài tham khảo khi cấu hình cho phép sau calibration.
- Chấm AI sau consent, ảnh Part 1 được gửi cùng đề, có giới hạn tổng dung lượng ảnh.
- Migration `AddWritingIndependentTestsAndPractice` được thêm trong source; chưa áp dụng lên môi trường dùng chung.

## Còn cần xác nhận trước khi sử dụng thực tế

- Chạy migration và UAT trên môi trường do nhóm cho phép theo [runbook](../../runbooks/writing-uat-runbook.md).
- Calibration AI với bài được giảng viên chấm, so sánh rubric với trường hợp có bài tham khảo; chỉ bật bài tham khảo sau khi đánh giá.
- Kiểm thử live đầy đủ MinIO, database, quyền Admin/User/guest và Gemini theo cấu hình môi trường.
