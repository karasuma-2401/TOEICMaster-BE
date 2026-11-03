# Writing Administration API

Các endpoint dưới đây dùng prefix `/api/v1/admin/writing` và yêu cầu JWT có trusted role claim `Admin` thông qua policy `AccountAdministration`.
Chúng không lấy role từ request header hoặc body.

---

## 1. Ngân hàng bài tập lẻ (Exercise Bank - Độc lập cho Luyện tập từng Part)

Phân hệ này quản lý các bài tập luyện tập đơn lẻ (`WritingExercise`), phục vụ riêng cho tính năng học viên Luyện tập theo từng Part (`/practice/writing/[partId]`). Phân hệ này **hoàn toàn độc lập** với phân hệ Đề thi Writing.

### Danh sách & Thao tác bài tập
- `GET /exercises?part=1&query=walk&difficulty=EASY&status=PUBLISHED&pageNumber=1&pageSize=20`: Trả danh sách phân trang theo `updatedAtUtc DESC`, sau đó `id`. Enum độ khó: `EASY`, `MEDIUM`, `HARD`; Enum trạng thái: `DRAFT`, `PUBLISHED`, `ARCHIVED`.
- `POST /exercises`: Tạo bài tập lẻ.
- `GET /exercises/{exerciseId}`: Lấy chi tiết bài tập.
- `PUT /exercises/{exerciseId}`: Cập nhật bài tập. Body: `{ "version": 1, "exercise": { ... } }` (optimistic concurrency qua `version`).
- `DELETE /exercises/{exerciseId}?version=1`: Archive bài tập lẻ (soft delete).

### Cấu trúc dữ liệu bài tập
Phần chung của body gồm: `part`, `code`, `topic`, `difficulty`, `status`, `rubricNotes`, `modelAnswer`.

| Part | Trường bắt buộc riêng |
| :--- | :--- |
| **1** | `imageAssetId`, `imageAlt`, `keywordOne`, `keywordTwo` |
| **2** | `sender`, `recipient`, `subject`, `emailBody`, `directions` |
| **3** | `prompt`, `minimumWordCount` (1–5000); `outline` là tùy chọn |

`modelAnswer` bắt buộc ở cả ba Part, đóng vai trò dữ liệu đối chuẩn khi AI chấm điểm.

### Import bài tập lẻ từ Excel
- `POST /exercises/imports/validate?part={1|2|3}`: Nhận `multipart/form-data` field `file` (.xlsx tối đa 10 MiB). Endpoint kiểm tra và trả preview danh sách câu hỏi hợp lệ/lỗi (`rows`, `validRowCount`, `invalidRowCount`).

---

## 2. Quản lý Ảnh (Media Assets)

- `POST /media/images`: Nhận `multipart/form-data` field `file`: chỉ chấp nhận JPEG/PNG/WebP hợp lệ, tối đa 5 MiB.
- Kiểm tra chữ ký file thực tế (magic bytes).
- Trả về `{ "id": "<uuid>", "url": "..." }`. Giá trị `id` này được sử dụng làm `imageAssetId` cho câu hỏi Part 1 (cả trong bài tập lẻ và đề thi). Asset mang tính bất biến.

---

## 3. Quản trị Đề thi Writing (Writing Tests - Tạo đề độc lập & Import Excel)

Đề thi Writing (`WritingTest`) được thiết kế **độc lập hoàn toàn**, không phụ thuộc vào Ngân hàng bài tập lẻ. Khi tạo đề thi, toàn bộ 8 câu hỏi chuẩn ETS (5 câu Part 1, 2 câu Part 2, 1 câu Part 3) được khai báo trực tiếp trong đề thi hoặc import trọn gói qua file Excel.

### 3.1. Danh sách & Tạo đề thi mới trực tiếp
- `GET /tests?query=&status=&pageNumber=1&pageSize=20`: Lấy danh sách đề thi phân trang theo trạng thái (`DRAFT`, `PUBLISHED`, `ARCHIVED`).
- `POST /tests`: Tạo mới một đề thi hoàn chỉnh ở trạng thái `DRAFT` với 8 câu hỏi trực tiếp:
  ```json
  {
    "code": "WT-2026-001",
    "title": "TOEIC Writing Test 1",
    "description": "Đề thi thử TOEIC Writing chuẩn cấu trúc ETS 8 câu hỏi.",
    "questions": [
      {
        "part": 1,
        "sequenceNumber": 1,
        "imageAssetId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "imageAlt": "Người phụ nữ đang làm việc",
        "keywordOne": "woman",
        "keywordTwo": "laptop",
        "rubricNotes": "Thì hiện tại tiếp diễn, đúng ngữ pháp",
        "modelAnswer": "The woman is working on her laptop in the office."
      },
      // ... Câu 2, 3, 4, 5 (Part 1)
      {
        "part": 2,
        "sequenceNumber": 6,
        "sender": "John Doe <john@company.com>",
        "recipient": "Jane Smith <jane@company.com>",
        "subject": "Office Supplies Inquiry",
        "emailBody": "Could you please inform me when the new equipment will arrive?",
        "directions": "Respond as if you are Jane Smith. Give 2 pieces of information and 1 request.",
        "rubricNotes": "Đầy đủ 2 thông tin và 1 câu hỏi/yêu cầu",
        "modelAnswer": "Dear Mr. Doe, The equipment is scheduled to arrive on Monday morning..."
      },
      // ... Câu 7 (Part 2)
      {
        "part": 3,
        "sequenceNumber": 8,
        "prompt": "Do you agree or disagree that working from home increases employee productivity? Give reasons and examples.",
        "minimumWordCount": 300,
        "outline": "Introduction: State stance. Body 1: Flexibility and time-saving. Body 2: Fewer workplace distractions. Conclusion: Restate stance.",
        "rubricNotes": "Tối thiểu 300 từ, luận điểm rõ ràng, liên kết mạch lạc",
        "modelAnswer": "In modern corporate environments, the debate regarding remote work..."
      }
    ]
  }
  ```

### 3.2. Import Đề thi Writing trọn gói từ Excel
Hỗ trợ import toàn bộ 8 câu hỏi kèm thông tin đề thi trong một lần thao tác:

- `GET /tests/import-template`: Tải về file mẫu Excel `.xlsx` gồm 4 sheets chuẩn:
  1. `Manifest`: `SchemaVersion=1`, `TestCode`, `Title`, `Description`.
  2. `Part1`: Đúng 5 dòng câu hỏi (SequenceNumber 1–5): `ImageAssetId`, `ImageAlt`, `KeywordOne`, `KeywordTwo`, `ModelAnswer`, `RubricNotes`.
  3. `Part2`: Đúng 2 dòng câu hỏi (SequenceNumber 6–7): `Sender`, `Recipient`, `Subject`, `EmailBody`, `Directions`, `ModelAnswer`, `RubricNotes`.
  4. `Part3`: Đúng 1 dòng câu hỏi (SequenceNumber 8): `Prompt`, `MinimumWordCount`, `Outline`, `ModelAnswer`, `RubricNotes`.
- `POST /tests/imports/validate`: Preview và kiểm tra hợp lệ file Excel đề thi (`multipart/form-data`, field `file`). Trả về cấu trúc 8 câu preview kèm danh sách cảnh báo/lỗi (ví dụ: thiếu ảnh, sai số lượng câu, thiếu model answer).
- `POST /tests/import`: Nhận `multipart/form-data` field `file` (.xlsx tối đa 10 MiB). Tạo ngay đề thi ở trạng thái `DRAFT` với đầy đủ 8 câu hỏi nếu toàn bộ dữ liệu hợp lệ. Cơ chế All-or-Nothing: có bất kỳ lỗi nào sẽ rollback toàn bộ transaction.

### 3.3. Xem chi tiết & Chỉnh sửa Đề thi Draft
- `GET /tests/{testId}`: Lấy chi tiết đề thi gồm metadata và toàn bộ 8 câu hỏi chi tiết.
- `PUT /tests/{testId}`: Cập nhật thông tin đề và nội dung các câu hỏi (chỉ cho phép khi đề ở trạng thái `DRAFT`). Body: `{ "version": 1, "test": { ... } }`.

### 3.4. Xuất bản Đề thi (Publish) & Tính Bất biến
- `POST /tests/{testId}/publish`: Body: `{ "version": 1 }`.
- **Điều kiện xuất bản:**
  - Đề thi phải có đúng **8 câu hỏi**: 5 câu Part 1 (`sequenceNumber` 1–5), 2 câu Part 2 (`sequenceNumber` 6–7), 1 câu Part 3 (`sequenceNumber` 8).
  - Tất cả các trường bắt buộc, `imageAssetId` (đã upload hợp lệ) và `modelAnswer` đều không được để trống.
- **Tính bất biến (Exam Immutability):**
  - Khi xuất bản thành công, trạng thái chuyển sang `PUBLISHED`.
  - Backend tự động sinh bản chụp JSON của từng câu hỏi và lưu vào `writing_test_questions.snapshot_json`.
  - Đề thi đã xuất bản **không thể chỉnh sửa hay xóa** để bảo vệ tính toàn vẹn của lịch sử làm bài và điểm số của học viên.
