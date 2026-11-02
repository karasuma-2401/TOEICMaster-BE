# Feature Specification: TOEIC Listening & Reading Module

> **Trạng thái:** Backend Admin đã triển khai / Learner Engine đang hoàn thiện  
> **Ánh xạ Figma:** Màn hình 08, 12, 15, 16, 17, 18, 40, 43, 44  
> **Quyết định kiến trúc:** [decisions/001-admin-lr-exam-revisions.md](../../decisions/001-admin-lr-exam-revisions.md)  
> **Giao ước API:** [contracts/api-contract.md](../../contracts/api-contract.md#quản-trị-đề-toeic-listening--reading)

---

## 1. Mục tiêu & Cấu trúc Đề thi Chuẩn ETS

Cung cấp bài thi mô phỏng chuẩn quốc tế TOEIC Listening & Reading gồm **200 câu hỏi** trong **120 phút** (Listening: 45 phút, Reading: 75 phút) và thang điểm chuẩn ETS **10–990**.

### Cấu trúc 103 Blocks & 200 Slots:
- **Part 1 (Photographs):** 6 blocks đơn (6 câu: 1–6, 4 lựa chọn A–D).
- **Part 2 (Question - Response):** 25 blocks đơn (25 câu: 7–31, **chỉ 3 lựa chọn A–C**).
- **Part 3 (Short Conversations):** 13 blocks nhóm (39 câu: 32–70, mỗi nhóm 3 câu).
- **Part 4 (Short Talks):** 10 blocks nhóm (30 câu: 71–100, mỗi nhóm 3 câu).
- **Part 5 (Incomplete Sentences):** 30 blocks đơn (30 câu: 101–130, 4 lựa chọn A–D).
- **Part 6 (Text Completion):** 4 blocks nhóm (16 câu: 131–146, mỗi đoạn văn 4 câu).
- **Part 7 (Reading Comprehension):** Single & Multi-passage (54 câu: 147–200).

---

## 2. Quy tắc Kỹ thuật Nghiêm ngặt (Strict Invariants)

1. **Tính bất biến của Đề thi (Revision Immutability):**
   - Đề thi (`Exam`) quản lý danh tính; Nội dung đề gắn với `ExamRevision`.
   - Một khi Revision đã `Published`, nó là **bất biến (read-only)** để bảo toàn tính toàn vẹn của các bài làm học viên trong quá khứ.
   - Muốn sửa đề đã publish, Admin bắt buộc phải tạo bản nháp mới (`Draft revision`) bằng cơ chế Fork.
2. **Quy tắc Master Audio:**
   - Trong phòng thi thực tế, file Master Audio chạy một mạch liên tục từ câu 1 đến câu 100, **tuyệt đối không cho phép học viên Pause hoặc Seek**.
3. **Barem điểm (Score Profiles):**
   - Một đề thi chỉ được phép Publish khi đã liên kết với Score Profile có đủ **101 mốc quy đổi** cho Listening (0–100) và 101 mốc cho Reading (0–100).
4. **Import XLSX All-or-Nothing:**
   - File Excel gồm 4 sheet: `Manifest`, `Blocks`, `ContextBlocks`, `Questions`. Quá trình kiểm tra tính hợp lệ diễn ra trước; nếu lỗi ở bất kỳ dòng nào hoặc file media hỏng, toàn bộ transaction sẽ bị rollback.

---

## 3. Tiêu chí nghiệm thu (Acceptance Criteria)

```gherkin
Scenario: Admin import đề thi thành công từ file Excel và Master Audio
  Given file Excel đúng chuẩn 103 blocks, 200 câu và file Master Audio hợp lệ
  When Admin gọi POST /api/v1/admin/exams/import
  Then hệ thống tạo mới ExamRevision ở trạng thái Draft với đầy đủ 200 slots

Scenario: Thí sinh làm bài thi L&R
  Given thí sinh bắt đầu lượt thi hợp lệ
  When hết 120 phút hoặc thí sinh bấm nộp bài
  Then hệ thống chấm điểm dựa theo Barem chuẩn liên kết
  And trả về kết quả quy đổi (Listening 5-495, Reading 5-495, Tổng điểm 10-990)
```
