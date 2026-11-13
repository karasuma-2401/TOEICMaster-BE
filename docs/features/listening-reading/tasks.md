# Checklist: TOEIC Listening & Reading

> Đối chiếu với [spec.md](./spec.md), [Admin API](./admin-api.md), [Attempt API](./attempt-api.md) và [barem điểm](./scoring.md). Dấu `[x]` nghĩa là có mã nguồn tương ứng trong working tree, không khẳng định đã deploy hoặc UAT.

## Phần có trong backend

### Domain và persistence

- [x] `Exam`/`ExamRevision` với skeleton 103 block, 200 câu, trạng thái `Draft`/`Published`/`Superseded` và fork.
- [x] `ScoreProfile`/`ScoreConversion` với 101 mốc cho mỗi kỹ năng để xác định `isComplete`.
- [x] `LrExerciseBankItem`/`LrExerciseBankQuestion` độc lập với đề, ràng buộc số câu theo Part.
- [x] EF Core mapping/migration cho quản trị đề và ngân hàng bài tập; mã nguồn media workspace cùng migration mới hiện có trong working tree.

### Application và API Admin

- [x] Tạo/list/preview đề; sửa câu hỏi, context, score profile; import workbook; publish; fork.
- [x] Tải template đề và template ngân hàng bài tập theo Part.
- [x] Tạo/list barem; kiểm tra barem đầy đủ khi publish.
- [x] Xem summary/list, tạo và import ngân hàng bài tập Part 1–7.
- [x] Upload ảnh và Master Audio theo luồng legacy; media-upload-intent/complete cho Draft workspace.
- [x] Policy Admin `AccountAdministration` trên các controller L&R.

## Chưa có trong backend

- [ ] API learner liệt kê/xem đề Published mà không lộ đáp án.
- [ ] Attempt L&R: tạo, resume, lưu câu trả lời, nộp bài, hết giờ, lịch sử và review.
- [ ] Phát media cho học viên với revision legacy/workspace và kiểm soát Listening 45 phút, Reading 75 phút.
- [ ] Chấm số câu đúng, tra `ScoreProfile`, trả điểm Listening/Reading/tổng điểm.
- [ ] API luyện tập lấy bài từ ngân hàng L&R; chức năng chọn câu từ ngân hàng để lắp đề.
- [ ] API sửa/archive/xóa item ngân hàng bài tập và preview import riêng.

## Điểm cần xác nhận trước khi làm learner flow

Các quyết định chưa có contract được liệt kê tại [attempt-api.md](./attempt-api.md#điều-kiện-cần-chốt-trước-khi-viết-contract-learner). Chỉ chuyển checklist learner sang `[x]` khi có endpoint, persistence và kiểm thử tương ứng; màn hình Figma/PRD không phải bằng chứng triển khai.

## Phần có trong Frontend/BFF

- [x] Route Admin `/admin/exams`: Data Bank Center cho danh sách/tạo/import đề và quản lý ngân hàng bài tập L&R.
- [x] Route Admin `/admin/exams/reading/{examId}/{revisionId}`: workspace xem/sửa question, context và publish Draft.
- [x] BFF kiểm tra same-origin cho mutation, session/role `ADMIN`, forward access token đến BE và đặt `Cache-Control: no-store`.
- [x] UI/BFF cho summary, list/filter, tạo tay, import và tải template ngân hàng bài tập Part 1–7.
- [x] UI/BFF tạo/import workbook đề, đọc score profile, cập nhật question/context, upload Master Audio legacy và publish.
- [x] Media workspace FE: tạo intent qua BFF, PUT trực tiếp lên URL ký không kèm credentials, complete qua BFF; UI hiển thị audio Part 1–4 và ảnh bắt buộc Reading.
- [x] Client kiểm tra schema response quản trị và thử refresh phiên một lần sau `401`.

## Chưa có trong Frontend/BFF

- [ ] Route/API/UI learner L&R: danh sách đề Published, phòng thi, player, timer, autosave đáp án, submit, kết quả, lịch sử và review.
- [ ] BFF/UI tạo ScoreProfile. FE hiện chỉ đọc profile để chọn khi tạo đề.
- [ ] BFF/UI đổi ScoreProfile trên Draft và fork revision, dù BE đã có endpoint.
- [ ] Luồng FE cho media của học viên; object storage vẫn chỉ phục vụ upload quản trị trong phạm vi hiện tại.
