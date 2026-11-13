# Feature Specification: TOEIC Listening & Reading Module

 > **Trạng thái:** Backend đã triển khai learner attempt lifecycle và FE đã có BFF, catalog, phòng thi, autosave, submit, result và review. Learner audio/image delivery và practice API vẫn còn thiếu.
> **Ánh xạ Figma:** Màn hình learner 12, 15–19; Admin 33–35, 40, 43, 44 (thiết kế UI, không đồng nghĩa API đã có).
> **Quyết định kiến trúc:** [decisions/001-admin-lr-exam-revisions.md](../../decisions/001-admin-lr-exam-revisions.md)
> **Tài liệu liên quan:** [Admin API](./admin-api.md) · [Attempt API](./attempt-api.md) · [Barem điểm](./scoring.md) · [Checklist](./tasks.md) · [API contract chung](../../contracts/api-contract.md#quản-trị-đề-toeic-listening--reading)

---

## 1. Mục tiêu & Cấu trúc Đề thi Chuẩn ETS

Mục tiêu sản phẩm là bài thi thử TOEIC Listening & Reading gồm **200 câu hỏi** trong **120 phút** (Listening: 45 phút, Reading: 75 phút), với điểm Listening/Reading 5–495 và tổng điểm 10–990. Backend hiện mới triển khai **khâu quản trị nội dung đề**; thời gian thi và tính điểm cho attempt chưa được thực thi. Barem do Admin cung cấp; hệ thống không có bảng ETS mặc định.

### Phạm vi hiện có

- Ngân hàng bài tập riêng theo Part 1–7: xem summary, danh sách, tạo item và import XLSX theo Part. Ngân hàng này chưa có chức năng tự lắp đề thi.
- Admin tạo đề Draft với cấu trúc cố định 103 block/200 slot, chỉnh câu hỏi và ngữ cảnh nhóm, chọn barem, import XLSX, publish và fork revision đã publish.
- Admin tạo `ScoreProfile` với mapping Listening/Reading; publish chỉ qua khi đủ 101 mốc cho mỗi kỹ năng.
- Hai loại media của đề: revision legacy dùng Master Audio; Draft mới và import chỉ có workbook dùng media workspace, upload audio theo block Listening và ảnh qua presigned PUT.

### Phần backend còn thiếu

- Chưa có API cấp presigned GET URL cho learner audio/image từ media storage private; attempt sheet hiện mới có object key Master Audio legacy và chưa trả audio asset URL theo block workspace.
- Chưa có API learner luyện tập theo Part từ exercise bank hoặc API lắp đề từ ngân hàng.
- Chưa có AI evaluation cho L&R; L&R chấm trắc nghiệm theo barem, không theo luồng AI của Speaking/Writing.

### Cấu trúc 103 Blocks & 200 Slots:
- **Part 1 (Photographs):** 6 blocks đơn (6 câu: 1–6, 4 lựa chọn A–D).
- **Part 2 (Question - Response):** 25 blocks đơn (25 câu: 7–31, **chỉ 3 lựa chọn A–C**).
- **Part 3 (Short Conversations):** 13 blocks nhóm (39 câu: 32–70, mỗi nhóm 3 câu).
- **Part 4 (Short Talks):** 10 blocks nhóm (30 câu: 71–100, mỗi nhóm 3 câu).
- **Part 5 (Incomplete Sentences):** 30 blocks đơn (30 câu: 101–130, 4 lựa chọn A–D).
- **Part 6 (Text Completion):** 4 blocks nhóm (16 câu: 131–146, mỗi đoạn văn 4 câu).
- **Part 7 (Reading Comprehension):** 15 blocks nhóm, tổng 54 câu: 147–200; mỗi block có 2–5 câu theo skeleton cố định.

---

## 2. Quy tắc Kỹ thuật Nghiêm ngặt (Strict Invariants)

1. **Tính bất biến của Đề thi (Revision Immutability):**
   - Đề thi (`Exam`) quản lý danh tính; Nội dung đề gắn với `ExamRevision`.
   - Một khi Revision đã `Published`, nó là **bất biến (read-only)** để giữ ổn định nội dung khi triển khai lượt làm học viên. Publish revision mới chuyển revision Published cũ thành `Superseded`.
   - Muốn sửa đề đã publish, Admin bắt buộc phải tạo bản nháp mới (`Draft revision`) bằng cơ chế Fork.
2. **Media Listening:**
   - Revision legacy cần Master Audio trước khi publish. Revision workspace cần asset audio ở từng block Part 1–4; `master-audio` legacy không thay thế điều kiện này.
   - Yêu cầu phòng thi không cho học viên pause/seek thuộc luồng learner chưa triển khai; chưa có enforcement backend tương ứng.
3. **Barem điểm (Score Profiles):**
   - Một đề thi chỉ được phép Publish khi đã liên kết với Score Profile có đủ **101 mốc quy đổi** cho Listening (0–100) và 101 mốc cho Reading (0–100).
4. **Import XLSX All-or-Nothing:**
   - File Excel gồm 4 sheet: `Manifest`, `Blocks`, `ContextBlocks`, `Questions`. Workbook sai hoặc `masterAudio` tùy chọn không hợp lệ thì không tạo Draft. Nếu lưu DB lỗi sau khi upload Master Audio, backend cố gắng xóa object vừa tải lên.

5. **Media workspace cho đề mới:**
   - Draft mới hoặc import chỉ với workbook dùng object storage private, upload bằng presigned PUT theo từng block audio Listening và ảnh Reading.
   - Publish kiểm tra audio Part 1–4 và mọi ảnh có `ImageRequired=true`. URL ký sẵn chỉ tồn tại trong phản hồi upload, không lưu vào database.
   - Revision cũ giữ `usesMediaWorkspace=false`, tiếp tục dùng Master Audio. `GET revision` trả cờ này để client chọn luồng chỉnh sửa phù hợp.

   [ADR-001](../../decisions/001-admin-lr-exam-revisions.md) ghi quyết định Master Audio ban đầu. Media workspace trong working tree bổ sung một luồng mới cho Draft; đoạn Master Audio của ADR áp dụng cho revision legacy.

6. **Điều kiện publish:**
   - Mọi câu phải có code, stem, lựa chọn và đáp án hợp lệ; Part 2 chỉ có A–C, các Part khác A–D. Code câu không được trùng trong revision.
   - Block nhóm phải có ít nhất một context; mọi ảnh `imageRequired=true` phải có asset. Với revision legacy, câu Part 1 bắt buộc có ảnh. Với workspace, ảnh Part 1 chỉ được kiểm tra khi đánh dấu `imageRequired=true` trong dữ liệu hiện tại.
   - Barem được gắn với revision phải đầy đủ 101 mốc cho Listening và 101 mốc cho Reading.

---

## 3. Tiêu chí nghiệm thu (Acceptance Criteria)

```gherkin
Scenario: Admin import đề thi bằng workbook
  Given workbook hợp lệ với 103 block, 200 câu và ScoreProfileId có thật
  When Admin gọi POST /api/v1/admin/exams/import
  Then hệ thống tạo ExamRevision Draft với 200 slot và usesMediaWorkspace=true

Scenario: Admin import theo luồng Master Audio legacy
  Given workbook hợp lệ và masterAudio MP3/WAV hợp lệ
  When Admin gọi POST /api/v1/admin/exams/import với cả hai field
  Then hệ thống tạo ExamRevision Draft với usesMediaWorkspace=false

Scenario: Admin công bố revision workspace chưa đủ media
  Given revision Draft workspace thiếu audio của một block Part 1–4 hoặc ảnh đã đánh dấu imageRequired
  When Admin gọi POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish
  Then API trả 400 ValidationProblemDetails với key exam và giữ revision là Draft

Scenario: Admin công bố revision hợp lệ
  Given mọi câu và context hợp lệ, barem đầy đủ, media bắt buộc đã gắn
  When Admin gọi POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish
  Then revision chuyển thành Published và revision Published cũ thành Superseded

Scenario: Admin import ngân hàng bài tập theo Part
  Given file XLSX đúng mẫu Part và mã item không trùng
  When Admin gọi POST /api/v1/admin/lr-exercise-bank/parts/{part}/imports
  Then API trả số item và số câu đã tạo cùng summary mới
```

### Tiêu chí learner đã được triển khai ở Backend

```gherkin
Scenario: Thí sinh làm bài thi L&R
  Given thí sinh bắt đầu lượt thi hợp lệ trên một revision đã Published
  When hết 120 phút hoặc thí sinh bấm nộp bài
  Then hệ thống chấm điểm theo barem gắn với revision
  And trả về Listening 5–495, Reading 5–495, tổng điểm 10–990
```

Các endpoint và payload được mô tả tại [Attempt API](./attempt-api.md).

## 4. Luồng Frontend & BFF hiện có

### Route và ranh giới truy cập

| Màn hình FE | Vai trò | Backend được gọi |
| --- | --- | --- |
| `/admin/exams` | Data Bank Center: xem/tạo/import đề, xem barem để chọn khi tạo đề, và quản lý ngân hàng bài tập Part 1–7. | Qua các route BFF `/api/admin/exams`, `/api/admin/score-profiles`, `/api/admin/lr-exercise-bank/*`. |
| `/admin/exams/reading/{examId}/{revisionId}` | Workspace chỉnh sửa một revision: question, context, media và publish. | Qua các route BFF `/api/admin/exams/{examId}/revisions/{revisionId}/*`. |

Các route `/api/admin/*` của Next.js không phải public API mới: chúng kiểm tra same-origin cho mutation, lấy session cookie, yêu cầu role `ADMIN`, chuyển access token sang `/api/v1/admin/*` của BE và trả `Cache-Control: no-store`. Vì vậy browser không gửi JWT trực tiếp đến BE.

### Luồng quản trị đề mới hoặc import workbook

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant FE as Next.js UI/BFF
    participant API as BE Admin API
    participant Store as S3-compatible storage

    Admin->>FE: Tạo Draft hoặc chọn workbook XLSX
    FE->>API: POST /api/v1/admin/exams hoặc /import
    API-->>FE: ExamId, RevisionId, Draft 200 slot
    Admin->>FE: Mở workspace revision
    FE->>API: GET revision; PUT question/context/score profile
    loop Mỗi audio hoặc ảnh workspace
        FE->>API: POST media-upload-intents
        API-->>FE: presigned PUT URL, headers, expiresAtUtc
        FE->>Store: PUT file trực tiếp với headers đã ký
        FE->>API: POST intent/complete
        API-->>FE: asset id đã gắn vào block/câu/context
    end
    Admin->>FE: Xuất bản
    FE->>API: POST publish
    API-->>FE: Published hoặc ValidationProblemDetails
```

`DataBankCenter` import đề từ workbook; UI không đính kèm `masterAudio` ở luồng import hiện tại. Khi tạo/import thành công, Admin chuyển đến workspace để tải audio Listening và ảnh bắt buộc. Tham số `masterAudio` vẫn tồn tại trong BE API và client helper nhằm tương thích revision legacy, không phải luồng tạo media mới trên UI.

### Chỉnh sửa và media theo loại revision

- Workspace lấy `usesMediaWorkspace` từ response revision để chọn UI. Revision `false` hiện panel Master Audio legacy và upload ảnh theo endpoint legacy; revision `true` hiện danh sách audio Part 1–4 cùng nút upload ảnh tại question/context Reading.
- Với workspace, FE thực hiện đúng ba bước `intent → PUT trực tiếp → complete`. Hàm PUT dùng `credentials: "omit"`; URL ký và credentials storage không được lưu trong FE hoặc database.
- FE chỉ khóa nút publish khi nhận biết thiếu media bắt buộc. BE luôn là nơi kiểm tra cuối cùng vì trạng thái có thể thay đổi hoặc request có thể bỏ qua UI.
- Sau lỗi `401`, client thử refresh session rồi gửi lại request; các lỗi HTTP khác được hiển thị từ `ProblemDetails`/`ValidationProblemDetails` của BFF/BE.

### Ngân hàng bài tập trên FE

`DataBankCenter` cho phép xem summary, lọc/phân trang theo Part, tạo thủ công một item và import XLSX. Các trường `audioUrl`/`imageUrl` của item ngân hàng là URL text trong dữ liệu item; chúng không đi qua media workspace của revision. UI hiện không dùng item ngân hàng để lắp tự động vào đề.

### Phần FE learner đã có và còn thiếu

FE đã có BFF `/api/lr/*`, catalog đề Published, trang hướng dẫn, attempt room, autosave, chuyển Reading, submit, result và review. Những phần còn thiếu là audio player dùng URL an toàn, hiển thị ảnh từ asset ID, attempt history trên library và practice Part 1–7. Xem checklist cập nhật tại [tasks.md](./tasks.md).
