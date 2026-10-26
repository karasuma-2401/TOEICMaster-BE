# Kế hoạch triển khai: Quản trị đề TOEIC Listening & Reading

## Phạm vi đã chốt

Chỉ triển khai luồng Admin. Module quản lý đề L&R hỗ trợ tạo Draft 200 slot theo
cấu trúc ETS, chỉnh sửa thủ công, import một workbook XLSX kèm Master Audio,
xem trước, publish và tạo bản nháp phiên bản kế tiếp từ đề đã publish. Không
triển khai luồng làm bài, chấm điểm hay hiển thị cho người học.

## Quyết định kiến trúc

- `Exam` là định danh ổn định của đề; `ExamRevision` là snapshot versioned.
  Bản publish bất biến, thao tác sửa tạo Draft revision mới.
- Mỗi revision khởi tạo đúng 103 block và 200 slot: Part 1 (6), Part 2 (25),
  Part 3 (13 x 3), Part 4 (10 x 3), Part 5 (30), Part 6 (4 x 4), Part 7
  (4 x 2, 3 x 3, 3 x 4, 5 x 5).
- Workbook có `Manifest`, `Blocks`, `ContextBlocks`, `Questions`; các row map
  bằng `BlockKey` và `QuestionNumber`, không phụ thuộc vị trí dòng.
- Import là all-or-nothing. Parser, định dạng tệp, cấu trúc ETS, đáp án, media
  reference và barem được kiểm tra trước. Upload object thất bại hoặc transaction
  thất bại không để lại revision dở dang.
- Master Audio chỉ `.mp3`/`.wav`, tối đa 100 MB; ảnh chỉ `.png`/`.jpg`/`.jpeg`/
  `.webp`, tối đa 5 MB. Kiểm tra chữ ký tệp, không tin Content-Type/tên tệp.
- Barem là resource versioned do Admin quản lý. Đề tham chiếu barem; publish
  yêu cầu đủ 101 mapping Listening và 101 mapping Reading.
- API quản trị áp dụng policy Admin hiện hữu. Module không thay đổi cơ chế cấp
  role/token đang được thực hiện ở task khác.

## API dự kiến

- `POST/GET /api/v1/admin/exams` — tạo Draft và danh sách có phân trang.
- `GET/PATCH /api/v1/admin/exams/{examId}/revisions/{revisionId}` — xem trước,
  sửa metadata Draft.
- `PUT /.../questions/{questionNumber}`, `PUT /.../blocks/{blockKey}/contexts`,
  `PUT /.../master-audio` — chỉnh nội dung Draft.
- `POST /api/v1/admin/exams/import` — import XLSX + Master Audio.
- `POST /api/v1/admin/exams/{examId}/revisions` — fork revision thành Draft.
- `POST /.../publish` — validate và publish Draft.
- `POST /api/v1/admin/exam-media/images` — upload image asset trước import.
- `/api/v1/admin/score-profiles` — quản lý barem tái sử dụng.

## Thứ tự thực hiện

1. Domain và test quy tắc 200 slot/revision immutability.
2. EF mapping, migration, repository và media storage.
3. Application command/query, XLSX parser và validation import.
4. API contracts/controller/error mapping; test phân quyền và multipart.
5. Workbook template, API documentation, format/build/test đầy đủ.

## Rủi ro và kiểm soát

| Rủi ro | Kiểm soát |
| --- | --- |
| ZIP/XLSX bom hoặc schema lạ | Giới hạn entry/số byte đọc, chỉ đọc XML cần thiết, validate header/sheet. |
| Upload giả loại tệp | Kiểm tra signature và giới hạn stream, tên object do server tạo. |
| Lỗi giữa object storage và DB | Validate trước, upload có cleanup khi transaction không commit. |
| Sửa đề đã publish làm sai lịch sử | Publish immutable; luôn fork revision mới. |
| Barem thiếu hoặc sai | Không seed mặc định; require 0–100 cho hai kỹ năng trước publish. |
