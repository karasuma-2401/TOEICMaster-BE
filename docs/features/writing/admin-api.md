# Writing Administration API

Các endpoint dùng prefix `/api/v1/admin/writing` và policy `WritingAdministration` (role `Admin`).

## Ngân hàng bài tập lẻ

- `GET /exercises?part=1&query=&difficulty=&status=&pageNumber=1&pageSize=20`: danh sách phân trang.
- `POST /exercises`, `GET /exercises/{id}`, `PUT /exercises/{id}` với body `{ "version": 1, "exercise": { ... } }`, `DELETE /exercises/{id}?version=1` để lưu trữ.
- Bài tập gồm `part`, `code`, `topic`, `difficulty`, `status`, `rubricNotes` và `modelAnswer`. `modelAnswer` là tài liệu tham khảo nội bộ tùy chọn; không trả cho học viên.
- Part 1 cần `imageAssetId`, `imageAlt`, `keywordOne`, `keywordTwo`; Part 2 cần `sender`, `recipient`, `subject`, `emailBody`, `directions`; Part 3 cần `prompt`, `minimumWordCount`, có thể có `outline`.
- `POST /exercises/imports/validate?part=1` preview `.xlsx`; `POST /exercises/import?part=1` kiểm tra lại và lưu toàn bộ file hợp lệ trong một lần lưu. Hai endpoint nhận `multipart/form-data`, field `file`, tối đa 10 MiB; mã bài phải duy nhất, ảnh phải tồn tại.
- `POST /media/images` upload JPEG/PNG/WebP tối đa 5 MiB, trả `id` dùng làm `imageAssetId`.

## Đề Writing độc lập

- `GET /tests`, `GET /tests/{id}` lấy danh sách/chi tiết. Trạng thái là `DRAFT`, `PUBLISHED` hoặc `ARCHIVED`; có thể lọc theo `status`.
- `POST /tests` tạo nháp; `PUT /tests/{id}` cập nhật nháp với `{ "version": 1, "test": { ... } }`. Mỗi phần tử `questions` chứa `part`, `sequenceNumber` (bắt đầu từ 1 trong mỗi Part), `exerciseId: null` và `content` có cấu trúc bài tập phía trên. Có thể lưu nháp chưa đủ câu; nội dung mỗi câu đã nhập phải hợp lệ. API tiếp tục đọc đề cũ tham chiếu `exerciseId`.
- `POST /tests/{id}/publish` với `{ "version": 1 }` yêu cầu đúng 5/2/1 câu có thứ tự liên tục; đề xuất bản không thể chỉnh sửa.
- `DELETE /tests/{id}?version=1` xóa vĩnh viễn đề nháp cùng các câu hỏi thuộc đề. Đề đã công bố hoặc lưu trữ không thể xóa qua endpoint này.
- `POST /tests/{id}/archive` với `{ "version": 2 }` lưu trữ đề đã công bố. Đề không còn xuất hiện cho học viên và không thể bắt đầu lượt làm mới; các lượt làm đã có, kết quả và nội dung snapshot vẫn được giữ.
- `POST /tests/{id}/restore` với `{ "version": 3 }` khôi phục đề đã lưu trữ về trạng thái công bố với nội dung cũ. Mọi thao tác trên kiểm tra `version`; dữ liệu cũ nhận lỗi 409.
- `GET /tests/import-template` tải mẫu Excel; `POST /tests/imports/validate` preview; `POST /tests/import` xác thực lại và tạo một đề `DRAFT` trong một lần lưu. Các endpoint import nhận `.xlsx` tối đa 10 MiB.
- Workbook gồm `Manifest` (`TestCode`, `Title`, `Description`), `Part1`, `Part2`, `Part3`. `SequenceNumber` trong Excel lần lượt là 1–5, 6–7 và 8; ảnh phải upload trước và điền `ImageAssetId`. Tên các cột theo file mẫu; `ModelAnswer` và `RubricNotes` tùy chọn.

Không sửa migration đã áp dụng. Migration `AddWritingIndependentTestsAndPractice` cần được nhóm áp dụng trên môi trường phù hợp trước khi dùng chức năng mới. Không chạy migration lên database dùng chung khi chưa được nhóm duyệt.
