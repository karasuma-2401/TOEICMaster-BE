# Barem điểm Listening & Reading

> Tài liệu này mô tả **cấu hình barem và điều kiện công bố đề hiện có**, không mô tả một API chấm bài học viên. Xem [Admin API](./admin-api.md#2-barem-điểm) và [Attempt API](./attempt-api.md).

## Dữ liệu barem

`ScoreProfile` là barem Admin tạo và gắn với một `ExamRevision` bằng UUID. Mỗi `ScoreConversion` chứa `skill` (`Listening` hoặc `Reading`), `rawCorrectCount` (0–100) và `scaledScore` (5–495, bội số của 5). Profile được coi là đầy đủ khi có đúng 101 mốc khác nhau cho mỗi kỹ năng, tức tổng cộng 202 mốc.

`POST /api/v1/admin/score-profiles` tạo profile; `GET /api/v1/admin/score-profiles` liệt kê profile với `isComplete` và `mappingCount`. Backend không cung cấp bảng quy đổi mặc định, không khẳng định profile do Admin nhập là barem ETS chính thức, và chưa có API sửa/xóa profile.

## Gắn barem với đề

Tạo hoặc import đề cần `scoreProfileId` trỏ tới profile có thật. Draft có thể đổi profile qua `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/score-profile`. Publish từ chối nếu profile chưa đủ 101 mốc Listening và 101 mốc Reading, trả `400 ValidationProblemDetails` key `exam`.

Revision đã công bố giữ `scoreProfileId`; fork tạo Draft mới từ revision đã công bố. Hiện backend chưa có `L&R Attempt`, bước đếm số câu đúng, tra bảng quy đổi cho lượt làm hay API trả điểm Listening/Reading/tổng điểm. Công thức và thời điểm trả kết quả cho học viên là yêu cầu sản phẩm trong [PRD](../../specs/prd-mvp.md), chưa phải hành vi API hiện tại.

## Hiển thị và sử dụng trên FE hiện tại

FE Admin gọi `GET /api/admin/score-profiles` qua BFF và hiển thị profile trong lựa chọn khi tạo Draft. Response FE dùng đúng bốn trường tóm tắt `id`, `name`, `isComplete`, `mappingCount`; không nhận bảng conversion chi tiết từ API list.

Hiện chưa có form/BFF FE để gọi `POST /api/v1/admin/score-profiles`, chưa có UI đổi barem của revision mặc dù BE có endpoint `PUT .../score-profile`, và chưa có bất kỳ UI learner nào hiển thị điểm quy đổi. Vì vậy tài liệu này không khẳng định FE đã tính hay hiển thị điểm TOEIC cho thí sinh.
