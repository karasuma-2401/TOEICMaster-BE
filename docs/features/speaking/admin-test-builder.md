# Thiết kế lại Test Builder Speaking (Admin)

> **Trạng thái:** Đề xuất, chưa triển khai  
> **Màn hình:** `/admin/tests/speaking` (Figma 41), `/admin/content/speaking` (Figma 36)  
> **Vấn đề gốc:** mục 4 của [báo cáo rà soát](../../runbooks/speaking-audit-report.md)  
> **Luật không trùng:** [ADR-002](../../decisions/002-speaking-test-content-uniqueness.md)

Thư mục FE trong tài liệu này tính từ `src/features/speaking/components/admin/`.

---

## 1. Bố cục đích

Giữ layout 3 cột hiện có. Thay đổi chính: tổ chức theo **11 câu của thí sinh**, chọn bài bằng bộ chọn có tìm kiếm, và gom lỗi vào một chỗ có liên kết.

```text
┌ TEST BUILDER · SP-2026-01 · [Bản nháp] · Đã lưu 10:32     [Đóng] [Lưu] [Xuất bản đề] ┐
├ ⚠ 2 lỗi, 1 cảnh báo: Câu 1 và 2 trùng đoạn văn (Sửa →) · Câu 6 và 8 trùng (Sửa →)   ┤
├───────────────┬─────────────────────────────────────┬─────────────────────────────────┤
│ Thông tin đề  │ Part 1 · Đọc to · 1/2 bài · câu 1–2 │ Góc nhìn User                   │
│ Mã, Tên, Mô tả│ ┌ Câu 1 ──────────────────────────┐ │ [Bài đang chọn] [Toàn đề 11 câu]│
│               │ │ P1-003 · Library hours · Dùng ở 2│ │                                 │
│ Sơ đồ 11 câu  │ │ [Đổi bài] [Xem] [Bỏ chọn]        │ │                                 │
│ 1 2 3 4 5 6 7 │ ├ Câu 2 ──────────────────────────┤ │                                 │
│ 8 9 10 11     │ │ [+ Chọn bài]                     │ │                                 │
│ (xanh/đỏ/xám) │ └─────────────────────────────────┘ │                                 │
│ Điều kiện     │ Part 3 · Trả lời · 0/1 bài · câu 5–7│                                 │
└───────────────┴─────────────────────────────────────┴─────────────────────────────────┘
```

Bộ chọn bài mở dạng panel khi bấm "Chọn bài":

```text
┌ Chọn bài cho Câu 2 · Part 1 ──────────────────────┐
│ [Tìm mã, chủ đề…] [Độ khó ▾]                      │
│ P1-001 · Museum tour · Dễ · Dùng ở 1 đề   [Chọn]  │
│ P1-003 · Library hours · Đang dùng ở Câu 1 (khóa) │
│ P1-004 · Bus notice · Dễ                  [Chọn]  │
│ ‹ Trang 1/5 ›     Chưa có bài phù hợp? [Tạo bài ↗]│
└───────────────────────────────────────────────────┘
```

---

## 2. Phương án theo từng vấn đề

| ID | Phương án | File FE chính | Cần BE? |
| :--- | :--- | :--- | :--- |
| UI-01 | Bỏ việc tải 100 bài lúc mở. Bộ chọn gọi `GET /admin/speaking/exercises?part=&query=&difficulty=&status=PUBLISHED&pageNumber=&pageSize=20`, debounce 300 ms. Giữ bản đồ `id → bài` từ các bài đã chọn và kết quả đã tải. | `speaking-test-workspace.tsx`, `test-slot-picker.tsx`, bộ chọn mới `exercise-picker-panel.tsx` | Không, API đã hỗ trợ |
| UI-02 | Giữ đúng vị trí. Bỏ `compact()` khi gửi; `sequenceNumber = chỉ số ô + 1`, bỏ qua ô trống. Parser đặt bài vào `exerciseIds[part][sequenceNumber - 1]`. | `speaking-test-workspace.tsx`, `admin-api.ts`, `parsers.ts` | Không, validator đã cho phép đề nháp thiếu vị trí |
| UI-03 | `fieldErrors` giữ toàn bộ mảng lỗi. Lưu ý các nơi đang đọc kiểu `string`. | `http.ts`, nơi gọi | Không |
| UI-04 | Hiện đúng `title` của server. Xung đột version thì hiện nút "Tải lại bản mới" và **giữ** thay đổi đang sửa để Admin so sánh. Cần phân biệt bằng `code` trong ProblemDetails. | `speaking-test-workspace.tsx` | Có, thêm `code` (ngoài `title`) |
| UI-05 | So sánh bản nháp với bản đã lưu. Có thay đổi thì hỏi xác nhận khi đóng; thêm `beforeunload`. | `speaking-test-workspace.tsx` | Không |
| UI-06 | Lưu nháp không đóng builder: cập nhật `saved` và `version`, hiện "Đã lưu lúc …". | `speaking-test-workspace.tsx`, `admin-speaking-tests-page.tsx` | Không |
| UI-07 | Tiêu đề lỗi theo thao tác: "Chưa lưu được" hoặc "Chưa thể công bố". | `publish-checklist-panel.tsx` | Không |
| UI-08 | Huy hiệu và checklist cùng lấy từ một nguồn. Ô có 3 trạng thái: hợp lệ, có lỗi kèm lý do, trống. Bấm lỗi thì cuộn đến câu đó (theo Figma 43). | `test-slot-picker.tsx`, `publish-checklist.ts` | Dùng `publish-check` |
| UI-09 | "Nhân bản thành nháp" cho đề đã công bố; "Xóa" cho đề nháp. "Gỡ đề" làm sau (D4). | `speaking-tests-table.tsx`, `speaking-test-workspace.tsx` | Có, xem mục 4 |
| UI-10 | Thống nhất: tiêu đề `11 CÂU · 7 BÀI`; mỗi Part ghi `1/2 bài · câu 1–2`. | `speaking-test-workspace.tsx`, `test-slot-picker.tsx` | Không |
| UI-11 | Part 4 dùng bảng nhập theo dòng (nhãn, nội dung, thêm, xóa, tối đa 20 dòng). Kiểm tra trước các giới hạn của server. Hiện lỗi cạnh từng trường. Tải bài cũ qua `DocumentRows` như hiện nay. | `speaking-exercise-fields.tsx`, `admin-draft.ts` | Không |
| UI-12 | Trong form bài tập, hiện "Đang dùng ở N đề". Chuyển khỏi `PUBLISHED` khi có đề nháp đang dùng thì hỏi xác nhận. | `speaking-exercise-editor.tsx` | Có, thêm số đếm |
| UI-13 | Thêm tab "Toàn đề 11 câu" ở cột xem trước; Part trống có liên kết "Tạo bài ↗" mở tab mới và làm mới khi quay lại. | `test-preview-panel.tsx` | Không |
| DUP-04 | Bài đã dùng vẫn hiện nhưng bị khóa, kèm "Đang dùng ở Câu n". Không ẩn, để Admin biết vì sao không chọn được. | `exercise-picker-panel.tsx` | Không |
| DUP-05 | Hiện "Dùng ở N đề" trong bộ chọn và bảng. | `exercise-picker-panel.tsx` | Có, thêm số đếm |

---

## 3. Luồng kiểm tra trước khi công bố

1. Admin đổi lựa chọn hoặc thông tin đề.
2. Sau 500 ms, FE gọi `POST /admin/speaking/tests/publish-check` với bản nháp hiện tại, kể cả chưa lưu. Hủy request cũ bằng `AbortController`.
3. Banner, sơ đồ 11 câu và checklist cùng hiển thị từ kết quả này.
4. Nút "Xuất bản đề" bị khóa khi còn lỗi mức chặn. Nếu chỉ có cảnh báo, hộp xác nhận liệt kê từng cảnh báo.
5. Khi bấm xuất bản: lưu bản nháp, rồi `POST /tests/{id}/publish`. Server kiểm tra lại toàn bộ.
6. Nếu `publish-check` lỗi mạng: vẫn cho lưu nháp, ghi rõ "Chưa kiểm tra được", và để server quyết định khi công bố.

`publishChecklist` ở FE vẫn dùng cho phản hồi tức thì (đủ số bài, trùng ID). Luật về nội dung chỉ lấy từ server.

---

## 4. Thay đổi backend cần thiết

Hợp đồng nằm ở [admin-api.md](./admin-api.md#đề-xuất-api-mới-chưa-triển-khai).

| Thay đổi | Endpoint hoặc trường | Migration? | Ưu tiên |
| :--- | :--- | :--- | :--- |
| Kiểm tra trước | `POST /tests/publish-check` | Không | P3 (mục 3 là thiết kế đích; trước đó lỗi hiện sau khi bấm công bố) |
| Nhân bản | `POST /tests/{id}/clone` | Không | P2 |
| Xóa đề nháp | `DELETE /tests/{id}?version=` | Không | P2 |
| Số đề đang dùng bài | `usedInTestCount` trong danh sách bài | Không | P2 |
| Mã lỗi máy đọc được | `code` trong ProblemDetails của `409` | Không | P2 |
| Gỡ đề | `POST /tests/{id}/withdraw` | **Có**: `ck_speaking_tests_status` chỉ cho `DRAFT`, `PUBLISHED` | P3, chờ D4 |

---

## 5. Tiêu chí nghiệm thu

```gherkin
Scenario: Bài đã chọn không thể chọn lại
  Given bài P1-003 đang ở Câu 1
  When Admin mở bộ chọn của Câu 2
  Then P1-003 hiện "Đang dùng ở Câu 1" và không bấm chọn được

Scenario: Vẫn chọn được bài cũ trong ngân hàng lớn
  Given Part 1 có hơn 100 bài PUBLISHED
  When Admin tìm mã của bài cũ nhất
  Then bài đó xuất hiện và chọn được

Scenario: Lưu không làm đổi vị trí
  Given Câu 2 đã chọn bài và Câu 1 còn trống
  When Admin lưu nháp rồi mở lại
  Then bài vẫn ở Câu 2 và Câu 1 vẫn trống

Scenario: Lưu nháp không làm mất ngữ cảnh
  When Admin bấm "Lưu bản nháp"
  Then builder vẫn mở, hiện thời điểm đã lưu, và version mới được dùng cho lần lưu sau

Scenario: Không mất thay đổi khi đóng nhầm
  Given có thay đổi chưa lưu
  When Admin bấm "Đóng"
  Then hệ thống hỏi xác nhận trước khi đóng

Scenario: Hiện đủ lỗi và dẫn tới đúng vị trí
  Given Câu 1 và Câu 2 trùng đoạn văn, và Câu 5 trùng Câu 8
  When kết quả kiểm tra trả về
  Then banner hiện đủ 2 lỗi
  And bấm vào một lỗi thì cuộn đến câu tương ứng
  And nút "Xuất bản đề" bị khóa

Scenario: Sửa đề đã công bố bằng cách nhân bản
  Given đề SP-01 đã công bố
  When Admin chọn "Nhân bản thành nháp"
  Then có đề nháp mới với mã mới và cùng các bài
  And SP-01 và các lượt làm của nó không đổi
```
