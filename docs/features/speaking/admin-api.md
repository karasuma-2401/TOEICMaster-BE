# Speaking Administration API

Các endpoint dùng prefix `/api/v1/admin/speaking`, yêu cầu JWT và policy `SpeakingAdministration`. Mọi response có `Cache-Control: no-store`.

## Tổng quan

- `GET /summary`: trả `{ exercises: { total, published, draft, archived }, tests: { total, published, draft } }`.

## Ngân hàng bài tập

`GET /exercises?part=1&query=walk&difficulty=EASY&status=PUBLISHED&pageNumber=1&pageSize=20`

Trả danh sách phân trang theo `updatedAtUtc DESC`, sau đó `id`. `pageSize` từ 1 đến 100. `query` tìm trong `code` và `topic`, tối đa 100 ký tự. Enum: `EASY`, `MEDIUM`, `HARD` và `DRAFT`, `PUBLISHED`, `ARCHIVED`.

- `POST /exercises`: tạo bài, trả `201`.
- `GET /exercises/{exerciseId}`: lấy chi tiết.
- `PUT /exercises/{exerciseId}`: body `{ "version": 1, "exercise": { ... } }`. Không đổi được Part.
- `DELETE /exercises/{exerciseId}?version=1`: chuyển sang `ARCHIVED`, không xóa dữ liệu.

Phần chung của body:

| Trường | Ràng buộc |
| --- | --- |
| `part` | 1–5 |
| `code` | 1–32 ký tự chữ, số, dấu gạch ngang; duy nhất, không phân biệt hoa thường |
| `topic` | 1–100 ký tự |
| `difficulty`, `status` | Enum ở trên |
| `rubricNotes` | Tối đa 2000 ký tự, tùy chọn |
| `modelAnswer` | 1–4000 ký tự |

Trường riêng theo Part:

| Part | Trường bắt buộc |
| --- | --- |
| 1 | `passage` (1–1500) |
| 2 | `imageAssetId`, `imageAlt` (1–300) |
| 3 | `context` (1–600), `questions` đúng 3 phần tử (mỗi phần tử 1–300) |
| 4 | `documentTitle` (1–200), `documentRows` 1–20 dòng `{ label (1–40), value (1–200) }`, `questions` như Part 3; `callerIntro` tùy chọn (tối đa 400) |
| 5 | `prompt` (1–600) |

Server chỉ lưu các trường thuộc đúng Part, trong `content_json`. Lỗi `400` dùng tên trường không có tiền tố, ví dụ `Passage`, để form hiện lỗi đúng chỗ.

`questions` của Part 3–4 phải khác nhau sau chuẩn hóa. Đoạn văn, từng câu hỏi, đề bài Part 5 và ảnh (theo `sha256`) không được trùng với bất kỳ bài nào khác trong ngân hàng, kể cả bài đã lưu trữ. Vi phạm trả `400` theo đúng trường, ví dụ `Passage` hoặc `Questions[1]`. Xem [ADR-002](../../decisions/002-speaking-test-content-uniqueness.md).

## Ảnh

- `POST /media/images`: `multipart/form-data`, field `file`. Chỉ JPEG hoặc PNG, kiểm tra bằng magic bytes, tối đa 5 MiB. Trả `201` với `{ id, contentType, sizeBytes }`.
- `GET /media/{assetId}`: lấy ảnh cho Admin, kể cả ảnh chưa công bố.

Asset bất biến. Thay ảnh nghĩa là tải asset mới.

## Đề Speaking

- `GET /tests?query=&status=&pageNumber=1&pageSize=20`: mỗi phần tử có `partCounts` và `questionCount`.
- `GET /tests/{testId}`: đề đã công bố đọc từ snapshot; đề nháp đọc bài hiện tại.
- `POST /tests`: tạo đề nháp, trả `201`.
- `PUT /tests/{testId}`: chỉ với đề nháp; body `{ "version": 1, "test": { ... } }`.
- `POST /tests/{testId}/publish`: body `{ "version": 1 }`.

Body đề:

```json
{
  "code": "SP-2026-01",
  "title": "Đề Speaking 01",
  "description": "",
  "exercises": [
    { "exerciseId": "uuid", "part": 1, "sequenceNumber": 1 }
  ]
}
```

Khi lưu: đề nháp được phép thiếu bài. Mỗi bài phải tồn tại và đúng Part; `sequenceNumber` từ 1 đến số bài của Part; không trùng `exerciseId` và không trùng vị trí. Một bài chỉ thuộc một đề: bài đã nằm trong đề khác (nháp hoặc đã công bố) trả `400 errors.Exercises`.

**Điều kiện công bố** (lỗi `422`, trả tất cả lỗi cùng lúc):

| Khóa lỗi | Điều kiện |
| --- | --- |
| `Code`, `Title` | Không rỗng |
| `Part1` … `Part5` | Đúng 2/2/1/1/1 bài, đánh số liên tục từ 1 |
| `Exercises` | Không lặp bài; mọi bài tồn tại, đúng Part và `PUBLISHED`; không bài nào thuộc đề khác |
| `Content` | Không trùng văn bản hoặc ảnh giữa các câu trong đề, kể cả khác Part |

Khi công bố, server ghi `snapshot_json` cho từng vị trí, tăng `version` và khóa đề. Sửa hoặc lưu trữ bài sau đó không ảnh hưởng đề đã công bố. `snapshot_json` có chứa rubric và bài mẫu, chỉ dùng cho Admin và attempt snapshot.

### Import đề từ Excel

`POST /tests/import`, `multipart/form-data`, field `file` (`.xlsx`, tối đa 16 MiB). Tạo 7 bài mới và 1 đề `DRAFT`, trả `201` với chi tiết đề. Nếu có lỗi thì không tạo gì.

Sheet `Test`: cột A là tên trường, cột B là giá trị, gồm `Code`, `Title`, `Description`.

Sheet `Exercises`: dòng 1 là tiêu đề, không phân biệt hoa thường và khoảng trắng. Cần đúng 7 dòng, 2/2/1/1/1 bài cho Part 1–5.

| Cột | Áp dụng | Ghi chú |
| --- | --- | --- |
| `Part`, `Sequence`, `Code`, `Topic`, `Difficulty`, `Model Answer` | Mọi bài | Bắt buộc |
| `Status` | Mọi bài | Bỏ trống là `DRAFT`. Muốn công bố đề, các bài phải là `PUBLISHED` |
| `Rubric Notes` | Mọi bài | Tùy chọn |
| `Passage` | Part 1 | |
| `Image Alt` và ảnh | Part 2 | Chèn bằng Insert > Pictures > **Place over Cells**, góc trên trái nằm trong dòng của bài. Ảnh "Place in Cell" bị từ chối. JPEG hoặc PNG, tối đa 5 MiB |
| `Context` | Part 3 | |
| `Question 1`, `Question 2`, `Question 3` | Part 3, 4 | |
| `Document Title`, `Document Rows`, `Caller Intro` | Part 4 | `Document Rows`: mỗi dòng trong ô là `nhãn \| nội dung` |
| `Prompt` | Part 5 | |

Lỗi `400` có khóa theo số dòng Excel, ví dụ `Rows[5].Passage`, `Rows[4].Image`. Khóa khác gồm `Test.Code`, `Exercises`, `Content`. File không đọc được trả `400`, file quá cỡ trả `413`, trùng mã đề trả `409`.

## Lỗi

| Mã | Khi nào |
| --- | --- |
| `400` | Dữ liệu không hợp lệ |
| `404` | Không tìm thấy |
| `409` | Trùng mã, sai `version`, đổi Part, sửa đề đã công bố |
| `413` | Ảnh quá 5 MiB |
| `422` | Chưa đủ điều kiện công bố |

## Đề xuất API mới (chưa triển khai)

Tất cả chỉ **thêm mới**, không đổi các response hiện có.

### Kiểm tra trước khi công bố (đề xuất)

`POST /tests/publish-check`. Body giống `POST /tests`. Không ghi dữ liệu. Chấp nhận đề chưa đủ bài.

```json
{
  "canPublish": false,
  "issues": [
    {
      "code": "DUPLICATE_PROMPT_TEXT",
      "severity": "BLOCKING",
      "message": "Câu 1 và Câu 2 có cùng nội dung.",
      "positions": [1, 2],
      "exerciseIds": ["uuid", "uuid"]
    },
    {
      "code": "SIMILAR_CONTENT",
      "severity": "WARNING",
      "message": "Câu 6 và Câu 8 gần giống nhau.",
      "positions": [6, 8],
      "exerciseIds": ["uuid", "uuid"]
    }
  ]
}
```

- `severity`: `BLOCKING` hoặc `WARNING`.
- `code`: `MISSING_INFO`, `INCOMPLETE_PART`, `EXERCISE_NOT_FOUND`, `EXERCISE_WRONG_PART`, `EXERCISE_NOT_PUBLISHED`, `DUPLICATE_EXERCISE`, `DUPLICATE_QUESTION_IN_EXERCISE`, `DUPLICATE_PROMPT_TEXT`, `DUPLICATE_IMAGE`, `SIMILAR_CONTENT`, `SAME_TOPIC_IN_PART`.
- `positions`: vị trí câu 1–11 trong đề đầy đủ, để UI cuộn tới đúng chỗ.
- Trả `400` khi body sai cấu trúc. Thiếu bài không phải lỗi `400`.

`POST /tests/{testId}/publish` dùng cùng logic. Mức `BLOCKING` trả `422`. Mức `WARNING` không chặn.

### Nhân bản đề (đề xuất)

`POST /tests/{testId}/clone`, body `{ "code": "SP-2026-01-V2", "title": "..." }`. Tạo đề nháp mới, trả `201`, dùng cùng các bài theo `ExerciseId`. Bài đã bị lưu trữ vẫn giữ trong bản nháp và sẽ báo lỗi ở bước kiểm tra. Đề gốc không đổi.

### Xóa đề nháp

`DELETE /tests/{testId}?version=1`. Chỉ với đề nháp. Trả `204`; xóa đề và các vị trí trong đề, không xóa bài tập. Thiếu `version` hoặc `version` nhỏ hơn 1 trả `400`; không tìm thấy trả `404`; đề đã công bố hoặc sai phiên bản trả `409`.

### Số đề đang dùng bài (đề xuất)

Thêm `usedInTestCount` vào phần tử danh sách của `GET /exercises`. Đây là trường mới, không đổi trường cũ.

### Mã lỗi cho `409` (đề xuất)

Thêm extension `code` vào ProblemDetails: `DUPLICATE_CODE`, `VERSION_CONFLICT`, `TEST_PUBLISHED`, để FE xử lý theo từng trường hợp.

### Gỡ đề (chờ D4)

`POST /tests/{testId}/withdraw`. Cần migration mới để thêm trạng thái vào `ck_speaking_tests_status`. Lượt làm cũ vẫn giữ nguyên.
