# Feature Specification: TOEIC Speaking Module

> **Trạng thái:** Backend đang phát triển (`feature/speaking-evaluation`)  
> **Ánh xạ Figma:** Màn hình 09, 13, 19, 20, 21, 22, 36, 41  
> **Tài liệu nháp kỹ thuật:** [notespeaking.md](../../notespeaking.md)

---

## 1. Cấu trúc Đề thi TOEIC Speaking (11 câu - ~20 phút)

| Part | Dạng bài | Số câu | Thời gian Chuẩn bị | Thời gian Trả lời |
| :--- | :--- | :--- | :--- | :--- |
| **Part 1** | Read a text aloud | Câu 1, 2 | 45 giây | 45 giây |
| **Part 2** | Describe a picture | Câu 3, 4 | 45 giây | 30 giây |
| **Part 3** | Respond to questions | Câu 5, 6, 7 | 3 giây | 15s (câu 5, 6) / 30s (câu 7) |
| **Part 4** | Respond using info provided | Câu 8, 9, 10 | 45s đọc bảng + 3s/câu | 15s (câu 8, 9) / 30s (câu 10) |
| **Part 5** | Express an opinion | Câu 11 | 45 giây | 60 giây |

---

## 2. Quy tắc Kỹ thuật Đặc thù của Speaking

1. **Kiểm tra Thiết bị Bắt buộc (Device Check):**
   - Trước khi vào phòng thi Speaking, hệ thống bắt buộc học viên kiểm tra Micro (ghi âm 5s và nghe lại). Nếu micro không đạt tín hiệu, không cho phép bắt đầu thi.
2. **Cơ chế Chuyển Pha Tự động 2 Giai đoạn (2-Phase Auto Transition):**
   - **Pha 1 (Preparation):** Đồng hồ đếm ngược thời gian chuẩn bị. Học viên không thể bấm bỏ qua hoặc nói trước.
   - **Tín hiệu chuyển pha:** Hệ thống phát tiếng "Beep" chuẩn và tự động kích hoạt Audio Recorder ở trình duyệt.
   - **Pha 2 (Response):** Đồng hồ đếm ngược ghi âm. Khi hết giờ, tự động dừng ghi âm và gửi blob audio lên server.
3. **Mô hình Dữ liệu (Database Invariants):**
   - Bảng `speaking_attempts`: Check constraint `ck_speaking_attempts_source` đảm bảo mỗi lượt làm chỉ thuộc về một bài thi đầy đủ (`test_id`) hoặc luyện tập theo Part (`exercise_id`).
   - Bảng `speaking_evaluations`: Mỗi lượt làm có tối đa 1 job chấm điểm AI.
4. **Chấm điểm bằng Google Gemini:**
   - Audio được lưu tạm trong MinIO bucket `speaking-recordings`.
   - AI đánh giá các tiêu chí rubric ETS: Phát âm (Pronunciation), Ngữ điệu & Trọng âm (Intonation & Stress), Độ lưu loát (Fluency), Ngữ pháp & Từ vựng.
   - Điểm ước tính: Thang điểm 0–200 ETS (bước nhảy 10 điểm).

---

## 3. Tiêu chí nghiệm thu (Acceptance Criteria)

```gherkin
Scenario: Học viên hoàn thành câu hỏi Speaking Part 1
  Given học viên đã qua bước kiểm tra micro và bắt đầu câu hỏi 1
  When hết 45 giây chuẩn bị
  Then hệ thống phát tiếng beep và tự động ghi âm
  And khi hết 45 giây ghi âm, hệ thống tự động khóa mic và nộp file âm thanh lên server
```
