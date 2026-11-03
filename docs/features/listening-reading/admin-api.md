# Listening & Reading Administration API

> Phạm vi: các endpoint Admin hiện có trong backend và lớp BFF FE đang gọi chúng. Xem [đặc tả](./spec.md), [barem điểm](./scoring.md) và [API contract chung](../../contracts/api-contract.md#quản-trị-đề-toeic-listening--reading).

Các nhóm `/api/v1/admin/exams`, `/api/v1/admin/exam-media`, `/api/v1/admin/score-profiles` và `/api/v1/admin/lr-exercise-bank` đều yêu cầu JWT thỏa policy `AccountAdministration` (role `Admin`). Dữ liệu câu hỏi và đáp án ở đây là dữ liệu quản trị; không dùng các response này làm API đề thi cho học viên.

## 1. Ngân hàng bài tập L&R

Ngân hàng bài tập là `LrExerciseBankItem` độc lập với `Exam` và `ExamRevision`. Chưa có thao tác lấy câu từ ngân hàng để tự lắp đề thi. Prefix: `/api/v1/admin/lr-exercise-bank`.

| Endpoint | Chức năng hiện có |
| --- | --- |
| `GET /summary` | Tổng số câu, thời điểm cập nhật và thống kê theo Part 1–7. |
| `GET /parts/{part}/items` | Danh sách phân trang; query: `query`, `difficulty`, `status`, `pageNumber`, `pageSize`. `pageSize` từ 1–100. Response gồm cả đáp án đúng. |
| `POST /parts/{part}/items` | Tạo một item cùng các câu con; trả `201` và item vừa tạo. |
| `GET /templates/{part}` | Tải mẫu `.xlsx` cho Part 1–7. |
| `POST /parts/{part}/imports` | Nhận `multipart/form-data` field `file`, `.xlsx` tối đa 10 MiB; import các item hợp lệ và trả số item/câu đã tạo cùng summary. |

Một item có `code`, `topic`, `difficulty` (`EASY`, `MEDIUM`, `HARD`), `status` (`DRAFT`, `PUBLISHED`, `ARCHIVED`), `context`, `audioUrl`, `imageUrl` và `questions`. Mỗi câu có `code`, `prompt`, `optionA`–`optionD`, `correctKey`, `translation`, `explanation`. `context` là bắt buộc cho item nhóm. `audioUrl` và `imageUrl` là chuỗi URL trong ngân hàng bài tập, khác với asset UUID của đề thi.

| Part | Loại item | Số câu con trong một item | Lựa chọn |
| --- | --- | ---: | --- |
| 1, 5 | `SINGLE` | 1 | A–D |
| 2 | `SINGLE` | 1 | A–C |
| 3, 4 | `GROUP` | 3 | A–D |
| 6 | `GROUP` | 4 | A–D |
| 7 | `GROUP` | 2–5 | A–D |

Mẫu import dùng một dòng cho mỗi item đơn, một dòng cho mỗi câu con của item nhóm. File nhóm dùng `GroupCode`, `Context`, `QuestionCode`; file đơn dùng `Code`. Import kiểm tra trùng mã trong file và với dữ liệu đã lưu trước khi thêm item. Hiện chưa có endpoint lấy chi tiết theo ID, sửa, archive/xóa, kiểm tra import riêng hoặc API luyện tập cho học viên.

## 2. Barem điểm

Prefix: `/api/v1/admin/score-profiles`.

| Endpoint | Request/response |
| --- | --- |
| `GET /` | Trả danh sách `{ id, name, isComplete, mappingCount }`. |
| `POST /` | Body `{ "name": "...", "conversions": [{ "skill": "Listening", "rawCorrectCount": 0, "scaledScore": 5 }] }`; trả `201` với cùng các trường tóm tắt. |

Mỗi `skill` là `Listening` hoặc `Reading`; `rawCorrectCount` là 0–100, `scaledScore` là 5–495 và chia hết cho 5. Một cặp `(skill, rawCorrectCount)` chỉ được khai báo một lần. Profile chỉ `isComplete=true` khi đủ 101 mốc cho **mỗi** kỹ năng. Backend không seed bảng ETS và chưa có endpoint sửa/xóa profile. Quy tắc publish nằm trong [scoring.md](./scoring.md).

## 3. Đề thi và revision

Prefix: `/api/v1/admin/exams`.

| Endpoint | Chức năng hiện có |
| --- | --- |
| `GET /?status=Draft&pageNumber=1&pageSize=20` | Danh sách theo trạng thái revision mới nhất; `pageSize` 1–100. |
| `POST /` | Body `{ "code": "LR-2026-001", "title": "Practice test", "scoreProfileId": "<uuid>" }`; trả `201` với Draft version 1 và 200 slot. `code` tối đa 64 ký tự, gồm chữ/số/`_`/`-` và bắt đầu bằng chữ hoặc số. |
| `GET /{examId}/revisions/{revisionId}` | Preview Admin toàn bộ 103 block, 200 câu, context, đáp án, `usesMediaWorkspace`, `hasMasterAudio`, `audioAssetId`, `imageRequired`. |
| `PUT /{examId}/revisions/{revisionId}/questions/{questionNumber}` | Ghi nội dung một câu trong Draft; trả `204`. |
| `PUT /{examId}/revisions/{revisionId}/blocks/{blockKey}/contexts` | Thay toàn bộ context của block nhóm trong Draft; body `{ "contexts": [...] }`, `ordinal` liên tiếp từ 1; trả `204`. |
| `PUT /{examId}/revisions/{revisionId}/score-profile` | Chọn profile khác cho Draft; body `{ "scoreProfileId": "<uuid>" }`; trả `204`. |
| `POST /{examId}/revisions/{revisionId}/publish` | Kiểm tra điều kiện publish, trả `200` với trạng thái `Published`. Revision published trước đó chuyển thành `Superseded`. |
| `POST /{examId}/revisions` | Body `{ "sourceRevisionId": "<published-revision-uuid>" }`; fork một published revision thành Draft version mới, trả `201`. |

`UpdateQuestion` nhận `code`, `stem`, `optionA`, `optionB`, `optionC`, `optionD`, `correctKey`, `translation`, `explanation`, `imageAssetId`, `imageRequired`. Part 2 chỉ có A–C; các Part khác cần A–D. `ReplaceContexts` nhận context kiểu `Text` hoặc `Image` với `ordinal`, `type`, `text`, `imageAssetId`, `altText`, `imageRequired`. `imageRequired=true` trì hoãn yêu cầu gắn ảnh tới lúc publish; asset UUID được gắn trực tiếp phải tồn tại.

Revision `Published` không chỉnh nội dung tại chỗ. Revision fork sao chép nội dung, barem và trạng thái media của revision nguồn. Xem [đặc tả](./spec.md) để biết điều kiện publish theo từng loại media.

## 4. Import đề hoàn chỉnh

- `GET /import-template`: tải `.xlsx` mẫu với 4 sheet `Manifest`, `Blocks`, `ContextBlocks`, `Questions`.
- `POST /import`: `multipart/form-data`; `workbook` bắt buộc, tối đa 10 MiB; `masterAudio` tùy chọn, `.mp3`/`.wav`, tối đa 100 MiB. Trả `201` với Draft nếu hợp lệ.

`Manifest` cần `SchemaVersion=1`, `TestCode`, `Title`, `Scope=LISTENING_READING`, `ScoreProfileId`. Import kiểm tra đúng 103 block và 200 cặp `BlockKey`/`QuestionNumber`, không dựa vào thứ tự dòng. `ImageRequired` là cột tùy chọn trong `ContextBlocks` và `Questions`. Có `masterAudio` thì tạo revision theo luồng master audio cũ; chỉ có `workbook` thì tạo media workspace. File mẫu chứa placeholder, cần điền nội dung và barem thực trước khi import. Không có endpoint preview/validate import riêng.

## 5. Media đề thi

`POST /api/v1/admin/exam-media/images` nhận `multipart/form-data` field `file`, ảnh PNG/JPEG/WebP tối đa 5 MiB, kiểm tra chữ ký file và trả `{ id, contentType, kind, byteSize }`. UUID `id` có thể gắn vào câu/context theo các endpoint chỉnh Draft.

Draft mới và import không kèm `masterAudio` có `usesMediaWorkspace=true`. Upload theo ba bước:

1. `POST /{examId}/revisions/{revisionId}/media-upload-intents` với `{ ownerKind, ownerKey, contentType, byteSize }`; trả `201` với `intentId`, `uploadUrl`, `expiresAtUtc`, `requiredHeaders`.
2. Client `PUT` bytes tới `uploadUrl`, gửi đúng `requiredHeaders`.
3. `POST /{examId}/revisions/{revisionId}/media-upload-intents/{intentId}/complete`; backend kiểm tra object, content type và kích thước rồi gắn asset vào owner; trả `201` với `{ id, contentType, kind, byteSize }`.

`ownerKind` nhận `AudioGroup`, `QuestionImage`, `ContextImage`. `ownerKey` tương ứng là `blockKey` của Listening Part 1–4, số câu Reading Part 5–7 dưới dạng chuỗi, hoặc `<blockKey>:<ordinal>` của context ảnh Reading Part 5–7. MIME hỗ trợ `audio/mpeg`, `audio/wav` (100 MiB) và `image/png`, `image/jpeg`, `image/webp` (5 MiB). Intent hết hạn sau 15 phút; intent hết hạn/đã hoàn tất hoặc revision không còn là Draft workspace trả `409`.

Intent ảnh hiện chỉ gắn vào câu/context Reading. Ảnh Part 1 vẫn có thể upload qua `/api/v1/admin/exam-media/images` rồi gắn `imageAssetId` khi sửa câu; trong code hiện tại, publish workspace chỉ bắt buộc ảnh Part 1 nếu câu có `imageRequired=true`.

`PUT /{examId}/revisions/{revisionId}/master-audio` vẫn có trong API legacy, nhận `multipart/form-data` field `file`, tối đa 100 MiB, trả `204`. Với revision workspace, điều kiện publish vẫn kiểm tra audio từng block; riêng việc gọi endpoint master audio không thỏa điều kiện này.

## 6. Lỗi và giới hạn

- Workbook sai: `400 ValidationProblemDetails`, key `workbook`; thiếu điều kiện publish: `400 ValidationProblemDetails`, key `exam`.
- Media sai: `400 ProblemDetails`; mã đề trùng: `409`; không tìm thấy exam/revision/profile: `404`.
- Ngân hàng bài tập trả `400 ProblemDetails` khi file/item không hợp lệ và `409 ProblemDetails` khi trùng mã.
- Chưa có endpoint xóa đề/revision, lấy media để học viên phát, hoặc API đề thi công khai cho học viên.

## 7. Frontend BFF contract

FE gọi cùng-origin `/api/admin/*`, sau đó BFF chuyển tiếp tới prefix BE `/api/v1/admin/*`. Bảng sau mô tả các route BFF thực sự tồn tại; `{...}` là path parameter giữ nguyên.

| BFF route | Method | Upstream BE | UI dùng |
| --- | --- | --- | --- |
| `/api/admin/exams` | `GET`, `POST` | `/api/v1/admin/exams` | Danh sách và tạo Draft. |
| `/api/admin/exams/import`, `/import-template` | `POST`, `GET` | `/api/v1/admin/exams/import`, `/import-template` | Import/tải mẫu đề. |
| `/api/admin/exams/{examId}/revisions/{revisionId}` | `GET` | Cùng path BE | Mở workspace. |
| `.../questions/{questionNumber}`, `.../blocks/{blockKey}/contexts` | `PUT` | Cùng path BE | Lưu question và context Draft. |
| `.../master-audio`, `.../publish` | `PUT`, `POST` | Cùng path BE | Media legacy và xuất bản. |
| `.../media-upload-intents`, `.../media-upload-intents/{intentId}/complete` | `POST` | Cùng path BE | Bước 1 và 3 của media workspace. |
| `/api/admin/exam-media/images` | `POST` | `/api/v1/admin/exam-media/images` | Upload ảnh legacy. |
| `/api/admin/score-profiles` | `GET` | `/api/v1/admin/score-profiles` | Chỉ đọc barem để chọn khi tạo đề. |
| `/api/admin/lr-exercise-bank/summary`, `parts/{part}/items`, `parts/{part}/imports`, `templates/{part}` | `GET`/`POST` | Cùng hậu tố trên `/api/v1/admin/lr-exercise-bank` | Summary, list/create, import và tải mẫu ngân hàng. |

Mọi mutation BFF kiểm tra same-origin trước khi forward. BFF kiểm tra session còn hạn và role `ADMIN`, giữ body JSON/multipart, giới hạn kích thước multipart ở lớp FE, chuyển tiếp response/status/content type từ BE và thêm `Cache-Control: no-store`.

### Khoảng trống FE được xác nhận

- BE có `POST /api/v1/admin/score-profiles`, nhưng FE chỉ có BFF `GET` và không có form tạo profile. Admin cần dùng client API được ủy quyền khác để tạo barem trước khi UI có profile để chọn.
- BE có `POST /api/v1/admin/exams/{examId}/revisions` để fork revision, nhưng FE chưa có BFF route hay thao tác UI tương ứng.
- Không có BFF/FE learner L&R. Không được dùng BFF Admin làm API learner vì response revision và item ngân hàng chứa `correctKey`.
