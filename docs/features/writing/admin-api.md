# Writing Administration API

Các endpoint dưới đây dùng prefix `/api/v1/admin/writing` và yêu cầu JWT có trusted role claim `Admin`.
Chúng không lấy role từ request header hoặc body. Luồng đăng nhập hiện tại chưa tự cấp role claim, vì vậy môi trường
thật cần module phân quyền cấp quyền Admin trước khi FE gọi API này; `ADMIN_PREVIEW=true` ở frontend chỉ là preview UI.

## Ngân hàng bài tập

`GET /exercises?part=1&query=walk&difficulty=EASY&status=PUBLISHED&pageNumber=1&pageSize=20`

Trả danh sách phân trang theo `updatedAtUtc DESC`, sau đó `id`. Các giá trị enum là `EASY`, `MEDIUM`, `HARD` và
`DRAFT`, `PUBLISHED`, `ARCHIVED`.

- `POST /exercises`: tạo bài tập.
- `GET /exercises/{exerciseId}`: lấy chi tiết.
- `PUT /exercises/{exerciseId}`: body `{ "version": 1, "exercise": { ... } }`; `version` là optimistic concurrency.
- `DELETE /exercises/{exerciseId}?version=1`: archive bài tập, không xóa dữ liệu mà đề đã tham chiếu.

Phần chung của body bài tập gồm `part`, `code`, `topic`, `difficulty`, `status`, `rubricNotes`, `modelAnswer`.

| Part | Trường bắt buộc riêng |
| --- | --- |
| 1 | `imageAssetId`, `imageAlt`, `keywordOne`, `keywordTwo` |
| 2 | `sender`, `recipient`, `subject`, `emailBody`, `directions` |
| 3 | `prompt`, `minimumWordCount` (1–5000); `outline` là tùy chọn |

`modelAnswer` bắt buộc ở cả ba Part, để dùng làm dữ liệu chuẩn cho bước chấm AI về sau.

## Ảnh và kiểm tra Excel

`POST /media/images` nhận `multipart/form-data` field `file`: chỉ JPEG/PNG hợp lệ, tối đa 5 MiB. Response chứa
`id`; dùng id đó làm `imageAssetId` cho Writing Part 1. Asset bất biến, nên ảnh của đề đã publish không bị thay khi
admin dùng ảnh mới cho câu hỏi khác.

`POST /exercises/imports/validate?part={1|2|3}` nhận `multipart/form-data` field `file`, chỉ `.xlsx`, tối đa 10 MiB.
Endpoint **chỉ preview/validate, không ghi dữ liệu**; UI có thể hiện từng dòng hợp lệ/lỗi trước khi bổ sung thao tác
commit ở checkpoint nối API. Sheet đầu tiên cần đúng tên cột sau:

- Part 1: `Code`, `Topic`, `Difficulty`, `Status`, `ImageAssetId`, `ImageAlt`, `KeywordOne`, `KeywordTwo`, `ModelAnswer`.
- Part 2: `Code`, `Topic`, `Difficulty`, `Status`, `Sender`, `Recipient`, `Subject`, `EmailBody`, `Directions`, `ModelAnswer`.
- Part 3: `Code`, `Topic`, `Difficulty`, `Status`, `Prompt`, `MinimumWordCount`, `ModelAnswer`.

`ImageAssetId` là UUID được trả về từ bước upload ảnh; không dùng đường dẫn file từ máy admin. Preview giới hạn 5.000
dòng dữ liệu và trả `rows`, `validRowCount`, `invalidRowCount`.

## Đề Writing

- `GET /tests?query=&status=&pageNumber=1&pageSize=20`
- `POST /tests`: tạo draft. Body gồm `code`, `title`, `description`, `questions`.
- `GET /tests/{testId}`
- `PUT /tests/{testId}`: chỉ draft; body `{ "version": 1, "test": { ... } }`.
- `POST /tests/{testId}/publish`: body `{ "version": 1 }`.

Mỗi item trong `questions` có `exerciseId`, `part`, `sequenceNumber`. Chỉ bài tập `PUBLISHED` đúng Part mới được chọn.
Publish yêu cầu đúng 5 câu Part 1, 2 câu Part 2, 1 câu Part 3 và số thứ tự liên tục từ 1. Khi publish, backend ghi JSON
snapshot của từng câu vào `writing_test_questions.snapshot_json`; sau đó không cho chỉnh sửa đề, và thay đổi ngân hàng
bài tập không tác động đề đã công bố.

Migration `AddWritingAdministration` chỉ được sinh trong source. Nó tạo `writing_media_assets`, `writing_exercises`,
`writing_tests`, `writing_test_questions`; checkpoint này không chạy `database update` lên Neon.
