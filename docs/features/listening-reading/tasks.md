# Checklist: TOEIC Listening & Reading

> Dấu `[x]` xác nhận có mã nguồn tương ứng; không đồng nghĩa đã deploy hoặc UAT.

## Backend

- [x] Quản trị Exam/ExamRevision, publish/fork, 200 câu và media Admin.
- [x] ScoreProfile đủ 101 mốc cho mỗi kỹ năng.
- [x] Persistence cho ExamAttempt và ExamAttemptAnswer.
- [x] Learner API: danh sách Published, overview, create/resume, tải attempt.
- [x] Autosave đáp án, gắn cờ, chuyển Listening → Reading và khóa Listening ở server.
- [x] Server-side deadline, auto-grade khi hết giờ, submit, result, review, history.
- [x] Không trả correctKey/explanation/translation trước submit.
- [x] Kiểm thử application cho lifecycle, quyền sở hữu, validation, deadline và scoring.
- [ ] Presigned GET media learner cho legacy Master Audio và workspace audio theo block.
- [ ] Kiểm thử tích hợp HTTP/DB và UAT với MinIO/production storage.
- [ ] API luyện tập từ L&R exercise bank và lắp đề từ ngân hàng.

## Frontend/BFF

- [x] BFF learner `/api/lr/*`: same-origin mutation, session token forwarding, no-store.
- [x] Typed API client cho catalog, overview, attempt, autosave, start-reading, submit, result và review.
- [x] Danh sách đề Published dùng API Backend.
- [x] Trang hướng dẫn lấy dữ liệu đề thật và bắt đầu/resume attempt.
- [x] Trang attempt lấy snapshot thật, map question/options/context.
- [x] Autosave đáp án/cờ; submit và chuyển Reading gọi Backend.
- [x] Trang kết quả và review dùng dữ liệu Backend.
- [ ] Player audio thật, tự phát và khóa pause/seek; phụ thuộc endpoint presigned GET media.
- [ ] Render ảnh/context image từ media asset ID.
- [ ] Hiển thị attempt history và số lần thi/điểm tốt nhất trên library.
- [ ] Tích hợp practice Part 1–7 với learner exercise API (Backend chưa có API learner cho exercise bank).
- [ ] E2E test và UAT trên môi trường có dữ liệu đề Published thực tế.

## Rủi ro / giới hạn còn lại

- Không dùng Admin preview API ở learner flow vì response có đáp án đúng.
- Chưa có learner media URL; không hiển thị thông báo giả rằng audio đã phát hoặc đáp án đã lưu trước khi API xác nhận.
- Cần chạy test/build ở cả hai repository và kiểm tra deployment env `API_BASE_URL` trước khi merge.
