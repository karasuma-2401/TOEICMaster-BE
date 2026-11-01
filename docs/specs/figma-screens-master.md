# TÀI LIỆU ĐẶC TẢ & MÔ TẢ CHI TIẾT CÁC MÀN HÌNH THIẾT KẾ FIGMA
## HỆ THỐNG LUYỆN THI TOEIC TRỰC TUYẾN 4 KỸ NĂNG (TOEICMASTER)

---

| **Thông Tin Tài Liệu** | **Nội Dung Chi Tiết** |
| :--- | :--- |
| **Dự án** | **TOEICMaster** — Nền tảng luyện thi và đánh giá năng lực TOEIC 4 kỹ năng |
| **Vai trò người lập** | **Senior UX/UI Designer & Product Design Lead** |
| **Nguồn thiết kế Figma** | [Figma File: TOEICMaster (node-id: 0-1)](https://www.figma.com/design/maKDgFmFHPvMoqeHBMW4US/TOEICMaster?node-id=0-1&p=f&t=O8G3nhSqmbtUH4KL-0) |
| **Phiên bản thiết kế** | **2.1 (Full Ecosystem: 44 Màn hình + Design System Foundations)** |
| **Trạng thái cập nhật** | Tháng 11/2026 (Cập nhật đồng bộ toàn diện thiết kế mới nhất trên Figma) |
| **Đối tượng tiếp nhận** | Product Owner, Frontend Developer, Backend Developer, QA/QC Engineer |
| **Mục đích tài liệu** | Mô tả chi tiết từng màn hình (layout, component, text, interaction, URL route, edge case) phục vụ thiết kế giao diện và phát triển frontend/backend |

---

## MỤC LỤC TỔNG QUAN

1. [TỔNG QUAN HỆ THỐNG THIẾT KẾ & DESIGN TOKENS (PAGE: DESIGN SYSTEM & FOUNDATIONS)](#1-tổng-quan-hệ-thống-thiết-kế--design-tokens)
   - 1.1. Triết lý thiết kế & Bảng màu thương hiệu (01 · Color Tokens - ID: `11:82`)
   - 1.2. Màu phân định bài thi độc lập (Category Theming: L&R, Speaking, Writing)
   - 1.3. Trạng thái ma trận câu hỏi (Question Palette Exam Tokens)
   - 1.4. Hệ thống kiểu chữ (02 · Typography - ID: `11:184`)
   - 1.5. Thư viện Master Components & States (03 · Master Components - ID: `11:194`)
   - 1.6. Nền tảng 3 Bài thi độc lập (04 · v2.1 Independent Exam Foundations - ID: `24:2`)
2. [BẢNG TỔNG HỢP 44 MÀN HÌNH HIỆN CÓ TRÊN FIGMA (PAGE 1 - ID: `0:1`)](#2-bảng-tổng-hợp-44-màn-hình-hiện-có-trên-figma)
3. [MÔ TẢ CHI TIẾT TỪNG PHÂN HỆ & MÀN HÌNH (DETAILED SCREEN SPECIFICATIONS)](#3-mô-tả-chi-tiết-từng-màn-hình)
   - **PHÂN HỆ 1: XÁC THỰC NGƯỜI HỌC (LEARNER AUTH - 3 MÀN HÌNH)**
     - [Màn hình 01: [Learner] Login_Page - /login (ID: 1:2)](#màn-hình-01-learner-login_page---login-frame-id-12)
     - [Màn hình 02: [Learner] Register_Page - /register (ID: 1:144)](#màn-hình-02-learner-register_page---register-frame-id-1144)
     - [Màn hình 03: [Learner] Forgot_Password_Page - /forgot-password (ID: 1:294)](#màn-hình-03-learner-forgot_password_page---forgot-password-frame-id-1294)
   - **PHÂN HỆ 2: CỔNG NGƯỜI HỌC, HỒ SƠ & LỊCH SỬ TIẾN ĐỘ (LEARNER PORTAL & PROGRESSION - 3 MÀN HÌNH)**
     - [Màn hình 04: [Learner] Home_Landing_Page - / (ID: 13:67084)](#màn-hình-04-learner-home_landing_page-----frame-id-1367084)
     - [Màn hình 05: [Learner] Profile_Settings_Page - /profile (ID: 13:70116)](#màn-hình-05-learner-profile_settings_page---profile-frame-id-1370116)
     - [Màn hình 06: [Learner] History_Progression_Page - /history (ID: 13:70205)](#màn-hình-06-learner-history_progression_page---history-frame-id-1370205)
   - **PHÂN HỆ 3: TRUNG TÂM LUYỆN TẬP KỸ NĂNG NGƯỜI HỌC (LEARNER PRACTICE HUB - 4 MÀN HÌNH)**
     - [Màn hình 07: [Learner] Practice_Hub_Overview - /practice (ID: 13:67225)](#màn-hình-07-learner-practice_hub_overview---practice-frame-id-1367225)
     - [Màn hình 08: [Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId] (ID: 13:67472)](#màn-hình-08-learner-practice_lr_interactive_mode---practicelrpartid-frame-id-1367472)
     - [Màn hình 09: [Learner] Practice_Speaking_Mode - /practice/speaking/[partId] (ID: 13:67617)](#màn-hình-09-learner-practice_speaking_mode---practicespeakingpartid-frame-id-1367617)
     - [Màn hình 10: [Learner] Practice_Writing_Mode - /practice/writing/[partId] (ID: 13:67739)](#màn-hình-10-learner-practice_writing_mode---practicewritingpartid-frame-id-1367739)
   - **PHÂN HỆ 4: THƯ VIỆN ĐỀ THI THỬ ETS (LEARNER EXAMS LIBRARY HUB - 4 MÀN HÌNH)**
     - [Màn hình 11: [Learner] Exams_Library_Hub - /exams (ID: 13:67810)](#màn-hình-11-learner-exams_library_hub---exams-frame-id-1367810)
     - [Màn hình 12: [Learner] Exams_LR_List - /exams/lr (ID: 24:8730)](#màn-hình-12-learner-exams_lr_list---examslr-frame-id-248730)
     - [Màn hình 13: [Learner] Exams_Speaking_List - /exams/speaking (ID: 24:8867)](#màn-hình-13-learner-exams_speaking_list---examsspeaking-frame-id-248867)
     - [Màn hình 14: [Learner] Exams_Writing_List - /exams/writing (ID: 24:9318)](#màn-hình-14-learner-exams_writing_list---examswriting-frame-id-249318)
   - **PHÂN HỆ 5: LUỒNG THI THỬ & ĐÁNH GIÁ LISTENING & READING (LEARNER L&R EXAM FLOW - 5 MÀN HÌNH)**
     - [Màn hình 15: [Learner] Exam_LR_Instructions - /exams/lr/[id] (ID: 13:67968)](#màn-hình-15-learner-exam_lr_instructions---examslrid-frame-id-1367968)
     - [Màn hình 16: [Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt (ID: 13:68036)](#màn-hình-16-learner-exam_lr_virtual_room---examslridattempt-frame-id-1368036)
     - [Màn hình 17: [Learner] Modal_Exam_LR_Confirm_Submit - Overlay (ID: 13:68592)](#màn-hình-17-learner-modal_exam_lr_confirm_submit---overlay-frame-id-1368592)
     - [Màn hình 18: [Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId] (ID: 13:69428)](#màn-hình-18-learner-exam_lr_result_report---examslridresultattemptid-frame-id-1369428)
     - [Màn hình 19: [Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId] (ID: 13:69536)](#màn-hình-19-learner-exam_lr_review_mode---examslridreviewattemptid-frame-id-1369536)
   - **PHÂN HỆ 6: LUỒNG THI THỬ & ĐÁNH GIÁ SPEAKING (LEARNER SPEAKING EXAM FLOW - 4 MÀN HÌNH)**
     - [Màn hình 20: [Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check (ID: 13:69176)](#màn-hình-20-learner-exam_speaking_device_check---examsspeakingiddevice-check-frame-id-1369176)
     - [Màn hình 21: [Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt (ID: 13:69278)](#màn-hình-21-learner-exam_speaking_virtual_room---examsspeakingidattempt-frame-id-1369278)
     - [Màn hình 22: [Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId] (ID: 24:9004)](#màn-hình-22-learner-exam_speaking_result_report---examsspeakingidresultattemptid-frame-id-249004)
     - [Màn hình 23: [Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId] (ID: 24:9106)](#màn-hình-23-learner-exam_speaking_review_mode---examsspeakingidreviewattemptid-frame-id-249106)
   - **PHÂN HỆ 7: LUỒNG THI THỬ & ĐÁNH GIÁ WRITING (LEARNER WRITING EXAM FLOW - 4 MÀN HÌNH)**
     - [Màn hình 24: [Learner] Exam_Writing_Instructions - /exams/writing/[id] (ID: 24:9455)](#màn-hình-24-learner-exam_writing_instructions---examswritingid-frame-id-249455)
     - [Màn hình 25: [Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt (ID: 13:69370)](#màn-hình-25-learner-exam_writing_virtual_room---examswritingidattempt-frame-id-1369370)
     - [Màn hình 26: [Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId] (ID: 24:9528)](#màn-hình-26-learner-exam_writing_result_report---examswritingidresultattemptid-frame-id-249528)
     - [Màn hình 27: [Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId] (ID: 24:9605)](#màn-hình-27-learner-exam_writing_review_mode---examswritingidreviewattemptid-frame-id-249605)
   - **PHÂN HỆ 8: QUẢN TRỊ ADMIN - TỔNG QUAN & NGƯỜI DÙNG (ADMIN CORE - 4 MÀN HÌNH)**
     - [Màn hình 28: [Admin] Overview_Dashboard - /admin/overview (ID: 1:823)](#màn-hình-28-admin-overview_dashboard---adminoverview-frame-id-1823)
     - [Màn hình 29: [Admin] Users_Management - /admin/users (ID: 1:393)](#màn-hình-29-admin-users_management---adminusers-frame-id-1393)
     - [Màn hình 30: [Admin] Users_Loading_State - /admin/users?state=loading (ID: 13:70854)](#màn-hình-30-admin-users_loading_state---adminusersstateloading-frame-id-1370854)
     - [Màn hình 31: [Admin] Users_Empty_State - /admin/users?state=empty (ID: 13:70775)](#màn-hình-31-admin-users_empty_state---adminusersstateempty-frame-id-1370775)
   - **PHÂN HỆ 9: QUẢN TRỊ ADMIN - KHO BÀI TẬP 4 KỸ NĂNG (EXERCISE BANK CMS - 8 MÀN HÌNH)**
     - [Màn hình 32: [Admin] Exercises_LR_Parts - /admin/exercises/lr (ID: 1:1116)](#màn-hình-32-admin-exercises_lr_parts---adminexerciseslr-frame-id-11116)
     - [Màn hình 33: [Admin] Listening_Part1_Management - /admin/exercises/lr/p1 (ID: 1:1389)](#màn-hình-33-admin-listening_part1_management---adminexerciseslrp1-frame-id-11389)
     - [Màn hình 34: [Admin] Reading_Part7_Management - /admin/exercises/lr/p7 (ID: 13:70307)](#màn-hình-34-admin-reading_part7_management---adminexerciseslrp7-frame-id-1370307)
     - [Màn hình 35: [Admin] Modal_Add_Reading_Part7_Cluster - Overlay (ID: 13:70476)](#màn-hình-35-admin-modal_add_reading_part7_cluster---overlay-frame-id-1370476)
     - [Màn hình 36: [Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1 (ID: 1:1804)](#màn-hình-36-admin-speaking_part1_management---adminexercisesspeakingp1-frame-id-11804)
     - [Màn hình 37: [Admin] Modal_Import_Exercises_Excel - Overlay (ID: 1:2218)](#màn-hình-37-admin-modal_import_exercises_excel---overlay-frame-id-12218)
     - [Màn hình 38: [Admin] Writing_Parts - /admin/exercises/writing (ID: 1:2674)](#màn-hình-38-admin-writing_parts---adminexerciseswriting-frame-id-12674)
     - [Màn hình 39: [Admin] Writing_Part1_Management - /admin/exercises/writing/p1 (ID: 1:2875)](#màn-hình-39-admin-writing_part1_management---adminexerciseswritingp1-frame-id-12875)
   - **PHÂN HỆ 10: QUẢN TRỊ ADMIN - QUẢN LÝ ĐỀ THI & TRÌNH SOẠN THẢO (EXAM BUILDER CMS - 5 MÀN HÌNH)**
     - [Màn hình 40: [Admin] Exams_Management - /admin/exams/lr (ID: 1:3237)](#màn-hình-40-admin-exams_management---adminexamslr-frame-id-13237)
     - [Màn hình 41: [Admin] Exams_Speaking_Management - /admin/exams/speaking (ID: 24:9700)](#màn-hình-41-admin-exams_speaking_management---adminexamsspeaking-frame-id-249700)
     - [Màn hình 42: [Admin] Exams_Writing_Management - /admin/exams/writing (ID: 24:9863)](#màn-hình-42-admin-exams_writing_management---adminexamswriting-frame-id-249863)
     - [Màn hình 43: [Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit (ID: 1:3475)](#màn-hình-43-admin-exambuilder_lr_200slots---adminexamslroidedit-frame-id-13475)
     - [Màn hình 44: [Admin] Modal_Create_Exam_Metadata - Overlay (ID: 1:4227)](#màn-hình-44-admin-modal_create_exam_metadata---overlay-frame-id-14227)
4. [ĐÁNH GIÁ CHUYÊN MÔN UX/UI & SO SÁNH TIẾN HÓA THIẾT KẾ (DESIGN AUDIT V1.0 → V2.1)](#4-đánh-giá-chuyên-môn-uxui--so-sánh-tiến-hóa-thiết-kế)
5. [HƯỚNG DẪN TRIỂN KHAI FRONTEND & CONTRACT INTEGRATION](#5-hướng-dẫn-triển-khai-frontend--contract-integration)

---

# 1. TỔNG QUAN HỆ THỐNG THIẾT KẾ & DESIGN TOKENS

Bản thiết kế trên Figma đã được nâng cấp lên phiên bản hoàn chỉnh với riêng một trang chuyên biệt: **`Design System & Foundations` (Node ID: `11:2`)**, chuẩn hóa toàn bộ Design Tokens và Components nền tảng.

```
                    ┌─────────────────────────────────────────────────────────┐
                    │               TOEICMASTER DESIGN SYSTEM                 │
                    ├────────────────────┬────────────────────────────────────┤
                    │ BRAND & SEMANTIC   │ EXAM CATEGORY THEMING (3 BÀI THI)  │
                    │ Primary: #FF5A1F   │ L&R: Blue (#1D4ED8 / #EFF6FF)      │
                    │ Gradient: Đỏ-Cam   │ Speaking: Purple (#7E22CE / #FAF5FF│
                    │ Dark: #302C2A      │ Writing: Green (#047857 / #ECFDF5) │
                    └────────────────────┴────────────────────────────────────┘
```

### 1.1. Triết lý thiết kế & Bảng màu thương hiệu (01 · Color tokens - Frame ID: `11:82`)

Hệ thống token màu sắc được tổ chức theo 4 nhóm rõ rệt:

| Nhóm Token | Tên Token Figma | Giá Trị HEX | Vai Trò & Vị Trí Áp Dụng |
| :--- | :--- | :--- | :--- |
| **Brand Primary** | `Primary` | `#FF5A1F` | Màu chủ đạo thương hiệu, CTA chính, viền focus |
| | `Primary_Hover` | `#E04810` | Trạng thái hover của nút chính |
| | `Primary_Light` | `#FFF7ED` | Nền sáng active, badge nhấn, highlight lựa chọn |
| | `Primary_Dark` | `#C70E1A` | Màu chữ thương hiệu nhấn đậm, trạng thái đặc biệt |
| | `Gradient_Start` | `#EC251B` | Điểm bắt đầu gradient đỏ thương hiệu |
| | `Gradient_End` | `#FF6C0D` | Điểm kết thúc gradient cam thương hiệu |
| **Neutral Surface**| `Dark_Title` | `#302C2A` | Tiêu đề chính, văn bản có trọng số cao |
| | `Dark_Body` | `#4C4542` | Văn bản nội dung cơ bản (Body text) |
| | `Muted_Text` | `#77716E` | Văn bản phụ, chú thích, thời gian, breadcrumb |
| | `Placeholder` | `#AAA39F` | Chữ gợi ý trong ô input (placeholder text) |
| | `Border_Light` | `#EEE9E6` | Viền mờ ngăn cách danh sách, divider |
| | `Border_Medium`| `#D9D2CE` | Viền ô nhập liệu, viền thẻ card, viền bảng |
| | `Bg_Warm` | `#FAF7F5` | Nền toàn trang, nền khu vực luyện tập |
| | `Bg_Surface` | `#FFFFFF` | Nền thẻ Card nội dung, Modal, Panel |
| | `Sidebar_Bg` | `#FAF8F7` | Nền cố định của Admin Sidebar |
| **Semantic State** | `Success_Text` | `#398565` | Chữ trạng thái Hoạt động, câu trả lời đúng |
| | `Success_Bg` | `#E5F4EC` | Nền badge Thành công, nền câu đúng |
| | `Success_Border`| `#A3D9C0` | Viền thông báo thành công |
| | `Warning_Text` | `#D97706` | Cảnh báo, trạng thái bản nháp, câu chưa xong |
| | `Warning_Bg` | `#FEF3C7` | Nền badge Cảnh báo |
| | `Danger_Text` | `#DC2626` | Lỗi xác thực, tài khoản bị khóa, câu trả lời sai |
| | `Danger_Bg` | `#FEE2E2` | Nền badge Thất bại / Bị khóa / Cần sửa |
| | `Info_Text` | `#2563EB` | Thông tin hướng dẫn, nhãn kỹ năng L&R |
| | `Info_Bg` | `#EFF6FF` | Nền badge Thông tin / Banner nhắc nhở |

---

### 1.2. Màu sắc nhận diện 3 Bài thi Độc lập (Category Theming)

Theo kiến trúc chuẩn hóa **v2.1 Independent Exam Foundations (`24:2`)**, hệ thống TOEICMaster phân định 3 bài thi độc lập thông qua bộ thẻ màu nhận diện trực quan:

```
┌─────────────────────────────────┬─────────────────────────────────┬─────────────────────────────────┐
│     LISTENING & READING (L&R)   │             SPEAKING            │             WRITING             │
├─────────────────────────────────┼─────────────────────────────────┼─────────────────────────────────┤
│ Text: #1D4ED8 (Xanh dương đậm)  │ Text: #7E22CE (Tím thẫm)        │ Text: #047857 (Xanh lục bảo)    │
│ Background: #EFF6FF (Xanh nhạt) │ Background: #FAF5FF (Tím nhạt)  │ Background: #ECFDF5 (Lục nhạt)  │
│ 200 câu · 120 phút · Thang 990  │ 11 câu · ~20 phút · Thang 0-200 │ 8 câu · 60 phút · Thang 0-200   │
└─────────────────────────────────┴─────────────────────────────────┴─────────────────────────────────┘
```

---

### 1.3. Trạng thái Ma trận Câu hỏi (Question Palette Exam Tokens)

Bản thiết kế chuẩn hóa màu sắc cho Question Palette (áp dụng trong phòng thi L&R và màn hình Review Mode):

| Mã Token Palette | Giá Trị HEX | Ý Nghĩa Trong Phòng Thi Thực Tế | Ý Nghĩa Trong Review Mode |
| :--- | :--- | :--- | :--- |
| **Answered** | `#10B981` (Xanh lá) | Câu hỏi học viên đã chọn đáp án | - |
| **Unanswered** | `#FFFFFF` (Trắng) | Câu hỏi còn trống, chưa có đáp án | - |
| **Active** | `#FFF7ED` (Bg) / `#FF5A1F` (Viền) / `#EA580C` (Text) | Ô câu hỏi đang chọn làm bài / biên tập | Ô câu hỏi đang xem chi tiết |
| **Flagged** | `#EAB308` (Biểu tượng cờ ⚑ vàng) | Học viên đánh dấu để kiểm tra lại | - |
| **Locked_Listening**| Bg `#F3F4F6`, Text `#9CA3AF` | Câu hỏi phần Listening bị khóa khi audio qua | - |
| **Review_Correct** | `#10B981` (Xanh lá) | - | Câu trả lời Chính xác |
| **Review_Incorrect**| `#EF4444` (Đỏ tươi) | - | Câu trả lời Bị sai |
| **Review_Skipped** | `#94A3B8` (Xám Slate) | - | Câu hỏi Bị bỏ qua (không trả lời)|

---

### 1.4. Hệ thống kiểu chữ (02 · Typography - Frame ID: `11:184`)

Sử dụng đồng bộ họ phông chữ **`Poppins`** với các quy chuẩn tỉ lệ dòng (line-height) và cấp bậc rõ ràng:

- **Display Hero / Score 990**: `48px Bold` (Dùng cho tổng điểm thi thử L&R, điểm số nổi bật).
- **Display 1**: `32px Bold` (Dùng cho Headline trang chủ, tiêu đề lớn phòng thi).
- **Heading 1 / Score 200**: `24px SemiBold / Bold` (Tiêu đề trang, điểm số Speaking & Writing 200).
- **Heading 2**: `18px SemiBold` (Tiêu đề Part, tiêu đề thẻ Card lớn, tên đề thi).
- **Heading 3**: `15px SemiBold` (Tiêu đề nhóm chức năng, trạng thái pha Speaking).
- **Body Large**: `15px Regular` (Đoạn mô tả ngắn hero, hướng dẫn thi tổng thể).
- **Body Base**: `13px Regular` (Nội dung câu hỏi, phương án trắc nghiệm, bài đọc, bảng dữ liệu).
- **Body Small**: `11px Regular / SemiBold` (Số thứ tự ô ma trận, tag badge, breadcrumb, footnote).
- **Code Timer**: `20px Bold` (Đồng hồ đếm ngược thời gian thi dạng Monospace `01:45:12`).

---

### 1.5. Thư viện Master Components & States (03 · Frame ID: `11:194`)

Đặc tả các thành phần UI dùng chung và trạng thái biến thể:
1. **Button Master**: Primary Gradient, Primary Solid, Secondary Outlined, Ghost, Disabled.
2. **Input Fields**: Normal, Hover, Focus (viền cam `#FF5A1F`), Error (viền đỏ `#DC2626` kèm thông báo lỗi *"Email không hợp lệ"* bên dưới), Disabled.
3. **Timer Module (Đồng hồ phòng thi)**:
   - Bình thường (Thời gian > 5 phút): Chữ xám đậm `01:45:12`.
   - Cảnh báo (Thời gian `<= 5 phút`): Nền vàng cam nhạt, chữ cam `#D97706` `00:04:59`.
   - Khẩn cấp (Thời gian `<= 1 phút`): Nền đỏ nhạt, nhấp nháy chữ đỏ `#DC2626` `00:00:59`.
4. **Speaking Two-Phase Recorder Component**:
   - `PHA 1 · MICRO CHƯA THU ÂM`: 45s chuẩn bị, đếm ngược `00:32`, dải sóng micro xám tĩnh, text: *"Sau tiếng Bíp bắt đầu nói"*.
   - `PHA 2 · ● REC`: 30s trả lời, đếm ngược `00:24`, chấm đỏ REC nhấp nháy, dải sóng âm dao động theo âm lượng thực tế của học viên.
5. **Writing Anti-Cheat Workspace Component**:
   - Khung gõ bài vô hiệu hóa chuột phải, vô hiệu hóa phím tắt dán `Ctrl+V` / `Cmd+V`.
   - Bộ đếm từ tự động theo thời gian thực (Real-time Word Counter) hiển thị dạng: *"320 từ / Khuyến nghị 300 từ"*.

---

### 1.6. Nền tảng 3 Bài thi Độc lập (04 · Frame ID: `24:2`)

Hệ thống TOEICMaster định hình 3 bài thi độc lập:
1. **Bài thi Listening & Reading (L&R)**:
   - Cấu trúc: 200 câu hỏi trắc nghiệm (Part 1 - Part 7).
   - Thời gian: 120 phút liên tục (45 phút Listening + 75 phút Reading).
   - Cơ chế: Master Audio phát xuyên suốt phần Listening, Question Palette 200 câu.
   - Thang điểm: 10–990 điểm ETS.
2. **Bài thi Speaking**:
   - Cấu trúc: 11 câu hỏi (Part 1 - Part 5).
   - Thời gian: ~20 phút.
   - Cơ chế: Thiết bị kiểm tra Microphone trước khi thi, quy trình 2 pha tự động (Pha 1 Chuẩn bị → Tiếng Bíp → Pha 2 Thu âm trả lời).
   - Thang điểm: 0–200 điểm ETS.
3. **Bài thi Writing**:
   - Cấu trúc: 8 câu hỏi (Part 1 Viết câu: 5 câu; Part 2 Phản hồi email: 2 câu; Part 3 Viết luận: 1 bài).
   - Thời gian: 60 phút.
   - Cơ chế: Soạn thảo văn bản chống gian lận, chặn paste, đếm từ tự động.
   - Thang điểm: 0–200 điểm ETS.

---

# 2. BẢNG TỔNG HỢP 44 MÀN HÌNH HIỆN CÓ TRÊN FIGMA

Tất cả 44 màn hình đều được bố trí trực quan trên **Page 1 (Node ID: `0:1`)**, được định danh theo quy chuẩn `[Role] Name - /route`:

| STT | Tên Frame trên Figma | Node ID | Kích Thước (W x H) | Phân Hệ | Route Đường Dẫn Frontend |
| :---: | :--- | :---: | :---: | :---: | :--- |
| **01** | `[Learner] Login_Page - /login` | `1:2` | 1722 x 836 px | Auth | `/login` |
| **02** | `[Learner] Register_Page - /register` | `1:144` | 1722 x 836 px | Auth | `/register` |
| **03** | `[Learner] Forgot_Password_Page - /forgot-password` | `1:294` | 1722 x 836 px | Auth | `/forgot-password` |
| **04** | `[Learner] Home_Landing_Page - /` | `13:67084` | 1722 x 1275 px | Portal | `/` |
| **05** | `[Learner] Profile_Settings_Page - /profile` | `13:70116` | 1722 x 875 px | Portal | `/profile` |
| **06** | `[Learner] History_Progression_Page - /history` | `13:70205` | 1722 x 1985 px | Portal | `/history` |
| **07** | `[Learner] Practice_Hub_Overview - /practice` | `13:67225` | 1722 x 965 px | Practice | `/practice` |
| **08** | `[Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId]` | `13:67472` | 1722 x 1000 px | Practice | `/practice/lr/[partId]` |
| **09** | `[Learner] Practice_Speaking_Mode - /practice/speaking/[partId]` | `13:67617` | 1722 x 850 px | Practice | `/practice/speaking/[partId]` |
| **10** | `[Learner] Practice_Writing_Mode - /practice/writing/[partId]` | `13:67739` | 1722 x 982 px | Practice | `/practice/writing/[partId]` |
| **11** | `[Learner] Exams_Library_Hub - /exams` | `13:67810` | 1722 x 950 px | Exams | `/exams` |
| **12** | `[Learner] Exams_LR_List - /exams/lr` | `24:8730` | 1722 x 918 px | Exams | `/exams/lr` |
| **13** | `[Learner] Exams_Speaking_List - /exams/speaking` | `24:8867` | 1722 x 918 px | Exams | `/exams/speaking` |
| **14** | `[Learner] Exams_Writing_List - /exams/writing` | `24:9318` | 1722 x 918 px | Exams | `/exams/writing` |
| **15** | `[Learner] Exam_LR_Instructions - /exams/lr/[id]` | `13:67968` | 1722 x 895 px | Exam Flow L&R | `/exams/lr/[id]` |
| **16** | `[Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt` | `13:68036` | 1722 x 902 px | Exam Flow L&R | `/exams/lr/[id]/attempt` |
| **17** | `[Learner] Modal_Exam_LR_Confirm_Submit - Overlay` | `13:68592` | 1722 x 900 px | Exam Flow L&R | Overlay Modal |
| **18** | `[Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId]` | `13:69428` | 1722 x 950 px | Exam Flow L&R | `/exams/lr/[id]/result/[attemptId]` |
| **19** | `[Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId]` | `13:69536` | 1722 x 1050 px | Exam Flow L&R | `/exams/lr/[id]/review/[attemptId]` |
| **20** | `[Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check` | `13:69176` | 1722 x 843 px | Exam Flow Speaking | `/exams/speaking/[id]/device-check` |
| **21** | `[Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt` | `13:69278` | 1722 x 932 px | Exam Flow Speaking | `/exams/speaking/[id]/attempt` |
| **22** | `[Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId]` | `24:9004` | 1722 x 998 px | Exam Flow Speaking | `/exams/speaking/[id]/result/[attemptId]` |
| **23** | `[Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId]` | `24:9106` | 1722 x 1000 px | Exam Flow Speaking | `/exams/speaking/[id]/review/[attemptId]` |
| **24** | `[Learner] Exam_Writing_Instructions - /exams/writing/[id]` | `24:9455` | 1722 x 864 px | Exam Flow Writing | `/exams/writing/[id]` |
| **25** | `[Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt` | `13:69370` | 1722 x 882 px | Exam Flow Writing | `/exams/writing/[id]/attempt` |
| **26** | `[Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId]` | `24:9528` | 1722 x 900 px | Exam Flow Writing | `/exams/writing/[id]/result/[attemptId]` |
| **27** | `[Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId]` | `24:9605` | 1722 x 1478 px | Exam Flow Writing | `/exams/writing/[id]/review/[attemptId]` |
| **28** | `[Admin] Overview_Dashboard - /admin/overview` | `1:823` | 1722 x 1200 px | Admin Core | `/admin/overview` |
| **29** | `[Admin] Users_Management - /admin/users` | `1:393` | 1722 x 871 px | Admin Core | `/admin/users` |
| **30** | `[Admin] Users_Loading_State - /admin/users?state=loading` | `13:70854` | 1722 x 948 px | Admin Core | `/admin/users?state=loading` |
| **31** | `[Admin] Users_Empty_State - /admin/users?state=empty` | `13:70775` | 1722 x 908 px | Admin Core | `/admin/users?state=empty` |
| **32** | `[Admin] Exercises_LR_Parts - /admin/exercises/lr` | `1:1116` | 1722 x 915 px | Exercise CMS | `/admin/exercises/lr` |
| **33** | `[Admin] Listening_Part1_Management - /admin/exercises/lr/p1` | `1:1389` | 1722 x 836 px | Exercise CMS | `/admin/exercises/lr/p1` |
| **34** | `[Admin] Reading_Part7_Management - /admin/exercises/lr/p7` | `13:70307` | 1722 x 836 px | Exercise CMS | `/admin/exercises/lr/p7` |
| **35** | `[Admin] Modal_Add_Reading_Part7_Cluster - Overlay` | `13:70476` | 1722 x 1100 px | Exercise CMS | Overlay Modal |
| **36** | `[Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1` | `1:1804` | 1722 x 836 px | Exercise CMS | `/admin/exercises/speaking/p1` |
| **37** | `[Admin] Modal_Import_Exercises_Excel - Overlay` | `1:2218` | 1722 x 836 px | Exercise CMS | Overlay Modal |
| **38** | `[Admin] Writing_Parts - /admin/exercises/writing` | `1:2674` | 1722 x 836 px | Exercise CMS | `/admin/exercises/writing` |
| **39** | `[Admin] Writing_Part1_Management - /admin/exercises/writing/p1` | `1:2875` | 1722 x 836 px | Exercise CMS | `/admin/exercises/writing/p1` |
| **40** | `[Admin] Exams_Management - /admin/exams/lr` | `1:3237` | 1722 x 836 px | Exam CMS | `/admin/exams/lr` |
| **41** | `[Admin] Exams_Speaking_Management - /admin/exams/speaking` | `24:9700` | 1722 x 931 px | Exam CMS | `/admin/exams/speaking` |
| **42** | `[Admin] Exams_Writing_Management - /admin/exams/writing` | `24:9863` | 1722 x 931 px | Exam CMS | `/admin/exams/writing` |
| **43** | `[Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit` | `1:3475` | 1722 x 1952 px | Exam Builder | `/admin/exams/lr/[id]/edit` |
| **44** | `[Admin] Modal_Create_Exam_Metadata - Overlay` | `1:4227` | 1722 x 836 px | Exam CMS | Overlay Modal |

---

# 3. MÔ TẢ CHI TIẾT TỪNG MÀN HÌNH

---

## PHÂN HỆ 1: XÁC THỰC NGƯỜI HỌC (LEARNER AUTH)

### Màn hình 01: [Learner] Login_Page - /login (Frame ID: `1:2`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/login`.
- **Bố cục (Layout)**: Split-screen tỉ lệ 45 : 55.
- **Vùng trái (Showcase Brand)**:
  - Nền ấm `#FAF7F5` kèm các gradient circle mờ.
  - Logo thương hiệu `TOEICMaster` kèm slogan *"Học theo cách của bạn"*.
  - Headline: *"Mỗi cuộc trò chuyện, một bước tiến bộ."* (chữ "tiến bộ" cách điệu nghiêng).
  - Khối chat preview AI: Bong bóng thoại học ngoại ngữ song ngữ (Hello! - Xin chào; Great! - Rất tốt).
  - Metric Card: `+24% Tiến bộ tuần này` kèm biểu tượng tăng trưởng.
  - Testimonial Card: Đánh giá 5 sao `★★★★★`, avatar `MT`, trích dẫn: *“Mình đã tự tin giao tiếp chỉ sau 8 tuần!”*.
- **Vùng phải (Form Đăng nhập)**:
  - Tab chuyển đổi: **[Đăng nhập]** (Active - Nền trắng, chữ đậm) | **[Đăng ký]**.
  - Header: *"Chào mừng đến TOEICMaster - Tiếp tục hành trình của bạn"*.
  - Input Email: Placeholder `ban@example.com`, nhãn "Email".
  - Input Mật khẩu: Kèm icon ẩn/hiện mắt (Show/Hide Password).
  - Tùy chọn: Checkbox *"Ghi nhớ đăng nhập"*; Link *"Quên mật khẩu?"* (`/forgot-password`).
  - Button chính: **[ĐĂNG NHẬP]** Gradient đỏ-cam (`#EC251B` → `#FF6C0D`).
  - Phân cách: *"hoặc tiếp tục với"*; Button SSO: **[Tiếp tục với Google]**.
  - Footer: *"Chưa có tài khoản? Đăng ký miễn phí"* (`/register`).

---

### Màn hình 02: [Learner] Register_Page - /register (Frame ID: `1:144`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/register`.
- **Bố cục**: Đồng bộ Split-screen với màn hình Đăng nhập.
- **Form Đăng ký**:
  - Tab chuyển đổi: **[Đăng ký]** (Active).
  - Tiêu đề: *"Chào mừng đến TOEICMaster - Bắt đầu hành trình mới"*.
  - Phụ đề: *"Tạo tài khoản miễn phí và bắt đầu luyện nói ngay hôm nay."*.
  - Các trường:
    1. Họ và tên (`Nguyễn Minh Anh`).
    2. Email (`ban@example.com`).
    3. Mật khẩu (`Tối thiểu 8 ký tự`).
    4. Checkbox thỏa thuận: *"Tôi đồng ý với Điều khoản sử dụng"*.
  - Button chính: **[Tạo tài khoản]** (Gradient đỏ-cam).
  - Footer: *"Bạn đã có tài khoản? Đăng nhập"* (`/login`).

---

### Màn hình 03: [Learner] Forgot_Password_Page - /forgot-password (Frame ID: `1:294`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/forgot-password`.
- **Chi tiết form**:
  - Tiêu đề: *"Khôi phục tài khoản - Quên mật khẩu?"*.
  - Hướng dẫn: *"Nhập email đã đăng ký, chúng tôi sẽ gửi cho bạn liên kết để đặt lại mật khẩu."*.
  - Trường nhập: Duy nhất 1 ô **Email** (`ban@example.com`).
  - Button chính: **[Gửi mã xác minh]** (Gradient đỏ-cam).
  - Footer: *"Bạn đã nhớ mật khẩu? Quay lại đăng nhập"* (`/login`).

---

## PHÂN HỆ 2: CỔNG NGƯỜI HỌC, HỒ SƠ & LỊCH SỬ TIẾN ĐỘ (LEARNER PORTAL & PROGRESSION)

### Màn hình 04: [Learner] Home_Landing_Page - / (Frame ID: `13:67084`)

- **Kích thước**: `1722 x 1275 px`.
- **Route**: `/`.
- **Kiểu bố cục**: Landing Page hiện đại gồm Global Learner Header + Hero Banner + Feature Cards + CTA.
- **Thành phần giao diện**:
  1. **Global Learner Header**:
     - Logo: `TOEICMaster`.
     - Navigation Menu: `Luyện tập 15 Parts`, `Đề thi thử ETS (3 Kỹ năng)`, `Bảng vàng thành tích`, `Hướng dẫn học`.
     - User Action Group: Nếu chưa đăng nhập: Nút **[Đăng nhập]** và Nút **[Đăng ký miễn phí]**.
  2. **Guest Alert Banner (Khách học thử)**:
     - Dải màu vàng cam nhẹ: *"Bạn đang học với tư cách Khách. Kết quả bài làm sẽ không được lưu vào hồ sơ cá nhân sau khi đóng trình duyệt."* kèm nút **[Đăng ký tài khoản miễn phí]**.
  3. **Hero Section**:
     - Badge nhận diện: `4 KỸ NĂNG · 3 BÀI THI ĐỘC LẬP`.
     - Headline: *"Hệ Thống Luyện Thi TOEIC Chuẩn Format ETS Số 1 Việt Nam"*.
     - Subtitle: *"Hệ thống phòng thi ảo chân thực 100%, ngân hàng đề 200 câu L&R, AI chấm điểm Speaking & Writing chi tiết."*.
     - Cụm CTA kép: **[Thi thử L&R · 120 phút]** (Gradient đỏ-cam) và **[Luyện tập theo từng Part]** (Viền cam nền trong suốt).
     - Trust Badges: `✓ Đề chuẩn ETS` · `✓ Giải thích song ngữ` · `✓ Bắt đầu miễn phí`.
  4. **Khối 3 Bài Thi Độc Lập (3 Independent Exams Card Grid)**:
     - Thẻ 1: **Listening & Reading** (200 câu, 120 phút, thang điểm 990, Master Audio liên tục).
     - Thẻ 2: **Speaking** (11 câu, 20 phút, thang điểm 200, phòng thi 2 pha Chuẩn bị/Nói).
     - Thẻ 3: **Writing** (8 câu, 60 phút, thang điểm 200, soạn thảo chống gian lận).

---

### Màn hình 05: [Learner] Profile_Settings_Page - /profile (Frame ID: `13:70116`)

- **Kích thước**: `1722 x 875 px`.
- **Route**: `/profile`.
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Hồ sơ cá nhân`.
  - Header: *"Hồ sơ & mục tiêu học tập - Cá nhân hóa hành trình chinh phục TOEIC của bạn."*.
  - Thẻ thông tin cá nhân:
    - Avatar học viên: Ký tự viết tắt `MA` (Minh Anh).
    - Meta info: Ngày tham gia `12/08/2026` · Tổng giờ luyện thi `48 giờ 30 phút`.
    - Các trường nhập liệu: Họ và tên, Email, Số điện thoại.
    - Nút **[Lưu thay đổi]**.
  - Thẻ Thiết lập Mục tiêu Điểm số (Target Score):
    - Mục tiêu Listening & Reading: Thang 10–990 (Điểm kỳ vọng ví dụ `850`).
    - Mục tiêu Speaking: Thang 0–200 (Điểm kỳ vọng ví dụ `160`).
    - Mục tiêu Writing: Thang 0–200 (Điểm kỳ vọng ví dụ `150`).
  - Thẻ Bảo mật & Đổi mật khẩu:
    - Mật khẩu hiện tại, Mật khẩu mới (*"Ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và số"*), Xác nhận mật khẩu mới.
    - Nút **[Cập nhật mật khẩu]**.

---

### Màn hình 06: [Learner] History_Progression_Page - /history (Frame ID: `13:70205`)

- **Kích thước**: `1722 x 1985 px` (Trang biểu đồ tiến trình dạng kéo dài).
- **Route**: `/history`.
- **Mục đích**: Bảng điều khiển theo dõi lịch sử làm bài và đồ thị tăng trưởng điểm số qua các lần thi.
- **Chi tiết giao diện**:
  - Header: *"Mỗi lần luyện thi, một bước tiến bộ - Theo dõi kết quả và nhìn lại hành trình chinh phục mục tiêu của bạn."*.
  - Thống kê tháng: `6 lần thi · +170 điểm trong tháng`.
  - 3 Tab phân tách theo 3 bài thi độc lập:
    - **[1. Lịch sử Đề Listening & Reading]** (Thang 990 - Active).
    - **[2. Lịch sử Đề Speaking]** (Thang 200).
    - **[3. Lịch sử Đề Writing]** (Thang 200).
  - Biểu đồ Tiến bộ Điểm số (Progression Line Chart):
    - Khung thời gian: `01/10/2026 — 31/10/2026 ⌄`.
    - Trục tung điểm số: 0 - 990 (Đường mục tiêu 750 / 850).
    - Các điểm mốc lần thi: Lần 1 (650) → Lần 3 (720) → Lần 6 (820).
  - Bảng Lịch sử Lần thi chi tiết (Exam Attempts Table):
    - Cột: `Lần thi`, `Tên đề thi`, `Ngày làm bài`, `Thời gian hoàn thành`, `Điểm Nghe / Đọc`, `Tổng điểm`, `Thao tác`.
    - Cột Thao tác: Nút **[Xem kết quả]** (`/exams/lr/[id]/result/[attemptId]`) và **[Xem lại bài]** (`/exams/lr/[id]/review/[attemptId]`).

---

## PHÂN HỆ 3: TRUNG TÂM LUYỆN TẬP KỸ NĂNG NGƯỜI HỌC (LEARNER PRACTICE HUB)

### Màn hình 07: [Learner] Practice_Hub_Overview - /practice (Frame ID: `13:67225`)

- **Kích thước**: `1722 x 965 px`.
- **Route**: `/practice`.
- **Mục đích**: Trung tâm lựa chọn luyện tập theo từng Part chuyên biệt có giải thích tức thì.
- **Chi tiết giao diện**:
  - Header: *"Ngân hàng Luyện tập theo từng Part - Chọn kỹ năng và dạng bài để rèn luyện chuyên sâu có đáp án và giải thích tức thì."*.
  - 3 Tabs chuyển đổi kỹ năng:
    - **[Listening & Reading (Part 1–7)]** (Active).
    - **[Speaking (Part 1–5)]**.
    - **[Writing (Part 1–3)]**.
  - Lưới các thẻ Part (Part Cards Grid):
    - *Part 1: Photographs* (Mô tả tranh - 240 câu hỏi · Dễ / TB / Khó).
    - *Part 2: Question & Response* (Hỏi & Đáp - 300 câu).
    - *Part 3: Short Conversations* (Hội thoại ngắn - 180 câu).
    - *Part 4: Short Talks* (Bài nói ngắn - 160 câu).
    - *Part 5: Incomplete Sentences* (Điền câu - 350 câu).
    - *Part 6: Text Completion* (Điền đoạn văn - 140 câu).
    - *Part 7: Reading Comprehension* (Đọc hiểu văn bản đơn & đa đoạn - 220 câu).
  - Nút trên từng thẻ: **[Bắt đầu luyện tập →]**.

---

### Màn hình 08: [Learner] Practice_LR_Interactive_Mode - /practice/lr/[partId] (Frame ID: `13:67472`)

- **Kích thước**: `1722 x 1000 px`.
- **Route**: `/practice/lr/[partId]`.
- **Mục đích**: Giao diện làm bài luyện tập tương tác có phản hồi ngay sau khi chọn đáp án.
- **Chi tiết giao diện**:
  - Top Control Bar:
    - Nút **[← Quay lại Hub]**.
    - Định danh: `Part 3 · Conversations` | Vị trí: `Câu 4 / 20 câu`.
    - Bộ công cụ học tập: Phím đổi cỡ chữ `A−` / `A+`, Dropdown `Chuyển nhanh câu 04 ⌄`.
  - Bộ điều khiển phát Audio chuyên sâu:
    - Thanh tua phát `00:24 / 00:58`.
    - Nút lùi 5s `↶ 5s`, Phát/Tạm dừng `▶`, Tiến 5s `5s ↷`.
    - Lựa chọn tốc độ phát âm thanh: `0.8×` · `1.0×` · `1.2×`.
  - Khung nội dung hội thoại & Ngữ cảnh:
    - Tiêu đề chủ đề: `CONVERSATION · OFFICE RELOCATION` - *"A new home for our team"*.
    - Hướng dẫn: *"Nghe cuộc hội thoại giữa hai đồng nghiệp và trả lời câu hỏi. Bạn có thể nghe lại, giảm tốc độ hoặc lặp đoạn A–B."*.
  - Khung câu hỏi & Phản hồi tức thì:
    - Các phương án trắc nghiệm A, B, C, D.
    - Banner phản hồi ngay khi chọn: `Phản hồi tức thì · Đã chọn B` (Màu xanh nếu đúng, Đỏ nếu sai).
    - Box Giải thích & Dịch nghĩa song ngữ mở rộng bên dưới.

---

### Màn hình 09: [Learner] Practice_Speaking_Mode - /practice/speaking/[partId] (Frame ID: `13:67617`)

- **Kích thước**: `1722 x 850 px`.
- **Route**: `/practice/speaking/[partId]`.
- **Mục đích**: Luyện nói tự do theo từng Part (ví dụ Part 1: Read a text aloud).
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Luyện tập / Speaking / Part 1`.
  - Header: *"Read a text aloud - Luyện phát âm, trọng âm và ngữ điệu. Nói thử, nghe lại và tự tin tiến bộ."*.
  - Đoạn văn bản mẫu cần đọc to.
  - Bộ công cụ ghi âm thử và nghe lại giọng đọc của bản thân.
  - Nút điều hướng: **[← Quay lại Hub]** và **[Bài tiếp theo →]**.

---

### Màn hình 10: [Learner] Practice_Writing_Mode - /practice/writing/[partId] (Frame ID: `13:67739`)

- **Kích thước**: `1722 x 982 px`.
- **Route**: `/practice/writing/[partId]`.
- **Mục đích**: Luyện viết câu theo tranh (Part 1), email (Part 2), hoặc bài luận (Part 3).
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Luyện tập / Writing / Part 1`.
  - Header: *"Write a sentence based on a picture - Viết một câu mô tả bức ảnh, sử dụng cả hai từ khóa. Có thể thay đổi dạng từ nếu cần."*.
  - Khung ảnh bài tập kèm 2 từ gợi ý bắt buộc.
  - Ô soạn thảo câu trả lời và nút gửi kiểm tra ngữ pháp tự động.

---

## PHÂN HỆ 4: THƯ VIỆN ĐỀ THI THỬ ETS (LEARNER EXAMS LIBRARY HUB)

### Màn hình 11: [Learner] Exams_Library_Hub - /exams (Frame ID: `13:67810`)

- **Kích thước**: `1722 x 950 px`.
- **Route**: `/exams`.
- **Mục đích**: Thư viện tổng quan dẫn vào 3 danh mục đề thi thử của 3 bài thi độc lập.
- **Chi tiết giao diện**:
  - Header: *"Thư viện Đề thi thử Chuẩn ETS - Khám phá kho đề thi thực chiến 4 kỹ năng được phân chia thành 3 bài thi độc lập."*.
  - 3 Thẻ Banner định hướng bài thi lớn:
    1. **Listening & Reading**: 200 câu · 120 phút · Thang 990 · Nút **[Vào danh sách đề L&R →]**.
    2. **Speaking**: 11 câu · ~20 phút · Thang 200 · Nút **[Vào danh sách đề Speaking →]**.
    3. **Writing**: 8 câu · 60 phút · Thang 200 · Nút **[Vào danh sách đề Writing →]**.

---

### Màn hình 12: [Learner] Exams_LR_List - /exams/lr (Frame ID: `24:8730`)

- **Kích thước**: `1722 x 918 px`.
- **Route**: `/exams/lr`.
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Đề thi thử / Listening & Reading`.
  - Badge: `L&R · BÀI THI ĐỘC LẬP` (Xanh `#EFF6FF` / `#1D4ED8`).
  - Tiêu đề: *"Đề thi Listening & Reading - Phòng thi mô phỏng 120 phút liên tục với Master Audio chuẩn ETS."*.
  - Metric học viên: `THÀNH TÍCH CAO NHẤT: 855 / 990`.
  - Toolbar lọc & Tìm kiếm:
    - Input: *"Tìm kiếm tên đề L&R…"*.
    - Dropdown: `Trạng thái: Tất cả` (Đã làm / Chưa làm), `Độ khó: Tất cả`, `Sắp xếp: Mới nhất`.
  - Danh sách đề thi (Exam Cards Grid):
    - Đề mẫu: `ETS 2024 · Full Test 01` (200 câu · 120 phút · Thang 990 · Tag `✓ Đã làm` · Tốt nhất: 855/990 · Nút **[Làm lại]**).
    - Đề mẫu: `ETS 2024 · Full Test 02` (Tag `Chưa làm` · Nút **[Bắt đầu thi]**).
  - Phân trang: `Hiển thị 1–6 trong 24 đề thi`.

---

### Màn hình 13: [Learner] Exams_Speaking_List - /exams/speaking (Frame ID: `24:8867`)

- **Kích thước**: `1722 x 918 px`.
- **Route**: `/exams/speaking`.
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Đề thi thử / Speaking`.
  - Badge: `SPEAKING · BÀI THI ĐỘC LẬP` (Tím `#FAF5FF` / `#7E22CE`).
  - Tiêu đề: *"Đề thi Speaking - Luyện phản xạ nói trong phòng thi hai pha và nhận đánh giá theo 5 tiêu chí."*.
  - Metric học viên: `THÀNH TÍCH CAO NHẤT: 160 / 200`.
  - Danh sách đề mẫu: `ETS Speaking · Test 01`, `Test 02`, `Test 03`... (Mỗi đề 11 câu · ~20 phút · 0–200 điểm · 5 Parts).

---

### Màn hình 14: [Learner] Exams_Writing_List - /exams/writing (Frame ID: `24:9318`)

- **Kích thước**: `1722 x 918 px`.
- **Route**: `/exams/writing`.
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Đề thi thử / Writing`.
  - Badge: `WRITING · BÀI THI ĐỘC LẬP` (Xanh lục `#ECFDF5` / `#047857`).
  - Tiêu đề: *"Đề thi Writing - Luyện viết câu theo tranh, phản hồi email và bài luận quan điểm trong 60 phút."*.
  - Danh sách đề thi: Mỗi đề 8 câu · 60 phút · Thang 0–200 · 3 Parts.

---

## PHÂN HỆ 5: LUỒNG THI THỬ & ĐÁNH GIÁ LISTENING & READING (LEARNER L&R EXAM FLOW)

### Màn hình 15: [Learner] Exam_LR_Instructions - /exams/lr/[id] (Frame ID: `13:67968`)

- **Kích thước**: `1722 x 895 px`.
- **Route**: `/exams/lr/[id]`.
- **Mục đích**: Trang hướng dẫn quy chế và kiểm tra tai nghe trước khi chính thức bấm giờ thi.
- **Chi tiết giao diện**:
  - Tiêu đề: `ETS 2024 · Full Test 01 - Hướng dẫn làm bài thi Listening & Reading`.
  - Bảng tóm tắt cấu trúc: `200 câu hỏi` | `120 phút` | `Phần 1: Listening (100 câu - 45 phút)` | `Phần 2: Reading (100 câu - 75 phút)`.
  - Khu vực kiểm tra âm thanh (Sound Check): Nút phát thử audio mẫu để kiểm tra tai nghe.
  - Quy chế thi nghiêm ngặt: Master Audio không thể tua ngược; Đồng hồ đếm ngược chạy tự động khi vào phòng thi.
  - Nút CTA chính: **[Bắt đầu làm bài thi ngay →]** (Chuyển sang Màn hình 16).

---

### Màn hình 16: [Learner] Exam_LR_Virtual_Room - /exams/lr/[id]/attempt (Frame ID: `13:68036`)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ HEADER: [ETS 2024 · Test 01] | Đang thi Listening (Part 3) | ⏰ Thời gian còn lại: 01:14:28     │
│ NÚT: [⚑ Đánh dấu câu] [Tạm dừng (Nếu cho phép)]                            [NỘP BÀI THI]        │
├─────────────────────────────────────────────────────────────┬───────────────────────────────────┤
│ CỘT TRÁI (68%): NỘI DUNG CÂU HỎI & NGỮ CẢNH                │ CỘT PHẢI (32%): MA TRẬN 200 CÂU   │
│                                                             │ (QUESTION PALETTE - CỐ ĐỊNH)      │
│ [PART 3: SHORT CONVERSATIONS] (Câu 32–34)                   │ Đã làm: 34/200 · Đánh dấu: 2      │
│ 🔊 Master Audio Đang Phát: [━━━━━━●━━━━━] 18:24 / 45:00     ├───────────────────────────────────┤
│ (Khóa tua ngược - Locked Listening Mode)                    │ Chú thích:                        │
│                                                             │ ■ Xanh: Đã chọn  □ Trắng: Chưa làm│
│ CÂU 32: What are the speakers mainly discussing?            │ ■ Cam: Đang làm  ⚑ Vàng: Đánh dấu │
│ ( ) A. A client presentation schedule                       ├───────────────────────────────────┤
│ (•) B. An upcoming office relocation                        │ LISTENING (1–100):                │
│ ( ) C. New software installation                            │ [1] [2] [3] [4] [5] [6] (Part 1)  │
│ ( ) D. Budget approval for marketing                        │ [7]...[31] (Part 2)               │
│                                                             │ [32][33][34]...[70] (Part 3)      │
│ CÂU 33: What problem does the woman mention?                │ READING (101–200):                │
│ ( ) A...  ( ) B...                                          │ [101]...[130] (Part 5)            │
│ CÂU 34: What will the man do next?                          │ [131]...[146] (Part 6)            │
│ ( ) A...  ( ) B...                                          │ [147]...[200] (Part 7)            │
└─────────────────────────────────────────────────────────────┴───────────────────────────────────┘
```

- **Kích thước**: `1722 x 902 px`.
- **Route**: `/exams/lr/[id]/attempt`.
- **Đặc điểm tương tác then chốt**:
  1. **Header phòng thi**:
     - Đồng hồ đếm ngược Monospace `01:14:28` có cảnh báo đổi màu theo thời gian thực.
     - Nút **[Nộp bài thi]** màu đỏ cam góc phải.
  2. **Cơ chế Master Audio (Listening Section)**:
     - Player chạy liên tục 45 phút không có nút Pause/Seek (chống tua).
     - Khi audio chuyển sang đoạn thoại câu 32–34, màn hình tự động focus vào cụm câu tương ứng.
  3. **Ma trận 200 câu hỏi (Question Palette)**:
     - 200 ô vuông phân chia 7 Part.
     - Ô đổi sang màu xanh lục `#10B981` ngay khi học viên click chọn đáp án.
     - Ô có biểu tượng cờ vàng `⚑` khi học viên bật cờ đánh dấu.
     - Click vào bất kỳ ô nào thuộc Reading (101–200) sẽ cuộn ngay đến câu đó.

---

### Màn hình 17: [Learner] Modal_Exam_LR_Confirm_Submit - Overlay (Frame ID: `13:68592`)

- **Kích thước**: `1722 x 900 px` (Hộp thoại xác nhận hiển thị dạng Overlay làm mờ nền phòng thi).
- **Mục đích**: Cảnh báo số lượng câu hỏi chưa hoàn thành trước khi chốt nộp bài.
- **Chi tiết giao diện**:
  - Tiêu đề: *"Xác nhận nộp bài thi Listening & Reading"*.
  - Bảng thống kê trạng thái làm bài:
    - `Đã hoàn thành`: **182 / 200 câu** (Màu xanh).
    - `Chưa trả lời`: **18 câu** (Màu đỏ cam cảnh báo).
    - `Đang gắn cờ kiểm tra lại`: **4 câu** (Màu vàng).
  - Cảnh báo: *"Bạn vẫn còn 12 phút làm bài. Khi đã nộp bài, bạn không thể thay đổi đáp án đã chọn."*.
  - 2 Nút hành động:
    - **[Tiếp tục làm bài]** (Đóng modal quay lại phòng thi).
    - **[Xác nhận nộp bài ngay]** (Gửi bài lên server và chuyển hướng sang Màn hình 18).

---

### Màn hình 18: [Learner] Exam_LR_Result_Report - /exams/lr/[id]/result/[attemptId] (Frame ID: `13:69428`)

- **Kích thước**: `1722 x 950 px`.
- **Route**: `/exams/lr/[id]/result/[attemptId]`.
- **Mục đích**: Báo cáo điểm số chính thức chuẩn thang điểm 990 ETS sau khi nộp bài.
- **Chi tiết giao diện**:
  - Breadcrumb: `Trang chủ / Đề thi thử / Listening & Reading / Kết quả lần thi #01`.
  - Hero Score Card:
    - Tổng điểm ETS: Con số lớn `855 / 990` (`48px Bold`).
    - Điểm thành phần:
      - **Listening**: `445 / 495` (89/100 câu đúng).
      - **Reading**: `410 / 495` (83/100 câu đúng).
    - Thời gian làm bài: `112 phút 45 giây`.
  - Phân tích hiệu suất theo từng Part (Part Breakdown Bars):
    - Hiển thị tỷ lệ phần trăm đúng của từng Part từ Part 1 đến Part 7.
  - Cụm nút hành động:
    - **[Xem lại chi tiết bài làm]** (Chuyển sang Màn hình 19 - Review Mode).
    - **[Làm lại đề này]**.
    - **[Về danh sách đề thi]**.

---

### Màn hình 19: [Learner] Exam_LR_Review_Mode - /exams/lr/[id]/review/[attemptId] (Frame ID: `13:69536`)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ HEADER: [← Về báo cáo kết quả] | ETS 2024 · Test 01 · Xem lại bài làm (Lần thi #01) · CHỈ ĐỌC   │
│ BỘ LỌC CÂU: [Tất cả (200)] [✓ Đúng (172)] [✗ Sai (18)] [⊘ Bỏ qua (10)]                         │
├─────────────────────────────────────────────────────────────┬───────────────────────────────────┤
│ CỘT TRÁI (68%): NỘI DUNG, TRANSCRIPT & LỜI GIẢI             │ CỘT PHẢI (32%): MA TRẬN REVIEW    │
│                                                             │                                   │
│ [PART 3: SHORT CONVERSATIONS] · Câu 32                      │ Ma trận xem lại (200 câu)         │
│ Audio: [▶ 00:38] kèm Interactive Transcript có Highlight:   ├───────────────────────────────────┤
│ Man: Did you finish the quarterly report?                   │ Chú thích:                        │
│ Woman: [Not yet. There is a software error...] (Manh mối)   │ ■ Xanh: Đúng (172)                │
│                                                             │ ■ Đỏ: Sai (18)                    │
│ CÂU 32: What are the speakers mainly discussing?            │ ■ Xám: Bỏ qua (10)                │
│ ( ) A. A new office computer                                ├───────────────────────────────────┤
│ [✓] B. The quarterly financial report [ĐÁP ÁN ĐÚNG]         │ [1][2][3][4][5][6]...             │
│ [✗] C. A customer meeting [BẠN ĐÃ CHỌN - SAI]               │ [32][33][34]...                   │
│                                                             │ (Ô 32 đỏ viền cam đang xem)       │
│ BOX GIẢI THÍCH CHI TIẾT (SONG NGỮ):                         │                                   │
│ - Đáp án đúng là B vì người phụ nữ đề cập đến báo cáo quý. │                                   │
│ - Manh mối nằm ở câu thoại: "Did you finish the quarterly..."│                                   │
└─────────────────────────────────────────────────────────────┴───────────────────────────────────┘
```

- **Kích thước**: `1722 x 1050 px`.
- **Route**: `/exams/lr/[id]/review/[attemptId]`.
- **Đặc tính UX Chuyên biệt**:
  - Chế độ chỉ đọc (Read-only review mode).
  - Thanh lọc phân loại: Cho phép chỉ lọc ra các câu làm Sai (`18 câu`) hoặc Bỏ qua (`10 câu`) để học viên rút kinh nghiệm nhanh.
  - Interactive Transcript: Transcript hội thoại có bôi màu vàng các câu thoại là "manh mối" (Clue highlighting) dẫn đến đáp án đúng.
  - Ma trận mã hóa màu: Xanh lá (Đúng), Đỏ (Sai), Xám (Bỏ qua).

---

## PHÂN HỆ 6: LUỒNG THI THỬ & ĐÁNH GIÁ SPEAKING (LEARNER SPEAKING EXAM FLOW)

### Màn hình 20: [Learner] Exam_Speaking_Device_Check - /exams/speaking/[id]/device-check (Frame ID: `13:69176`)

- **Kích thước**: `1722 x 843 px`.
- **Route**: `/exams/speaking/[id]/device-check`.
- **Mục đích**: Bước bắt buộc kiểm tra phần cứng Micro và Tai nghe trước khi mở phòng thi Speaking.
- **Chi tiết giao diện**:
  - Tiêu đề: *"Kiểm tra thiết bị thu âm & Tai nghe - ETS Speaking Test 01"*.
  - Khối kiểm tra Microphone:
    - Dropdown chọn nguồn micro máy tính.
    - Nút **[Nói thử để kiểm tra]**: Học viên đọc câu mẫu *"Hello, I am ready for the TOEIC Speaking test."*.
    - Dải sóng đo cường độ âm thanh (Volume VU Meter) dao động thời gian thực.
    - Badge thông báo: `✓ Microphone hoạt động tốt, âm lượng đạt chuẩn`.
  - Khối kiểm tra Loa/Tai nghe: Nút phát âm thanh mẫu *"Click to test audio playback"*.
  - Nút CTA: **[Thiết bị sẵn sàng · Vào phòng thi Speaking →]** (Chuyển sang Màn hình 21).

---

### Màn hình 21: [Learner] Exam_Speaking_Virtual_Room - /exams/speaking/[id]/attempt (Frame ID: `13:69278`)

- **Kích thước**: `1722 x 932 px`.
- **Route**: `/exams/speaking/[id]/attempt`.
- **Cơ chế vận hành 2 pha tự động**:
  1. **Header**:
     - Định danh: `ETS Speaking · Test 01 | Question 1 of 11 (Part 1: Read a text aloud)`.
     - Bộ đếm tổng thể bài thi.
  2. **Vùng hiển thị đề bài**:
     - Đoạn văn bản đọc to hiển thị rõ ràng trên nền sáng ấm.
  3. **Khối điều khiển 2 Pha (Two-phase Execution)**:
     - **PHA 1 (Chuẩn bị)**:
       - Banner: `PHA 1 · MICRO CHƯA THU ÂM`.
       - Đồng hồ đếm ngược chuẩn bị: `00:45` → `00:00`.
       - Dải micro xám tĩnh, text nhắc: *"Sau tiếng Bíp bắt đầu nói"*.
     - **CHUYỂN PHA**: Hệ thống tự động phát âm thanh thông báo tiếng "BÍP" chuẩn format thi ETS.
     - **PHA 2 (Thu âm trả lời)**:
       - Banner: `PHA 2 · ● REC (ĐANG THU ÂM)`.
       - Đồng hồ đếm ngược thời gian nói: `00:45` → `00:00`.
       - Biểu tượng chấm tròn đỏ REC nhấp nháy, dải sóng âm thanh dao động theo giọng đọc.
       - Không cho phép thu âm lại; hết giờ hệ thống tự động lưu file audio và chuyển sang câu tiếp theo.

---

### Màn hình 22: [Learner] Exam_Speaking_Result_Report - /exams/speaking/[id]/result/[attemptId] (Frame ID: `24:9004`)

- **Kích thước**: `1722 x 998 px`.
- **Route**: `/exams/speaking/[id]/result/[attemptId]`.
- **Mục đích**: Báo cáo điểm số Speaking chuẩn thang điểm 0–200 ETS kèm nhận xét 5 tiêu chí.
- **Chi tiết giao diện**:
  - Tiêu đề: *"SPEAKING · PERSONAL SCORE REPORT - Kết quả bài thi Speaking"*.
  - Điểm tổng: `160 / 200` (Thang điểm ETS bước nhảy 10 điểm).
  - Đánh giá theo 5 Tiêu chí chấm thi chuẩn ETS:
    1. **Pronunciation (Phát âm)**: `High / Tốt` (Nguyên âm, phụ âm rõ ràng).
    2. **Intonation & Stress (Ngữ điệu & Trọng âm)**: `High / Tốt` (Ngắt nghỉ đúng cụm nghĩa).
    3. **Grammar (Ngữ pháp)**: `Medium / Trung bình` (Một số lỗi chia thì quá khứ).
    4. **Vocabulary (Từ vựng)**: `High / Tốt` (Sử dụng từ vựng phù hợp ngữ cảnh).
    5. **Cohesion & Relevance (Tính liên kết & Đúng chủ đề)**: `High / Tốt`.
  - Cụm nút: **[Nghe lại bài nói & Xem phản hồi]** (`/exams/speaking/[id]/review/[attemptId]`).

---

### Màn hình 23: [Learner] Exam_Speaking_Review_Mode - /exams/speaking/[id]/review/[attemptId] (Frame ID: `24:9106`)

- **Kích thước**: `1722 x 1000 px`.
- **Route**: `/exams/speaking/[id]/review/[attemptId]`.
- **Chi tiết giao diện**:
  - Header: *"Speaking · Test 01 · Xem lại bài làm · Lần thi #02 · CHỈ ĐỌC"*.
  - Palette 11 câu hỏi Speaking: Cho phép chọn từng câu hỏi từ Question 1 đến Question 11.
  - Khung chi tiết câu hỏi đang chọn:
    - Đề bài gốc của câu hỏi.
    - Player phát lại đoạn ghi âm thực tế của học viên: `00:36 / 00:45`.
    - Player phát audio bài mẫu chuẩn bản xứ để đối sánh.
    - Nhận xét chi tiết của AI/Chuyên gia về điểm mạnh và các từ phát âm chưa chuẩn cần cải thiện.

---

## PHÂN HỆ 7: LUỒNG THI THỬ & ĐÁNH GIÁ WRITING (LEARNER WRITING EXAM FLOW)

### Màn hình 24: [Learner] Exam_Writing_Instructions - /exams/writing/[id] (Frame ID: `24:9455`)

- **Kích thước**: `1722 x 864 px`.
- **Route**: `/exams/writing/[id]`.
- **Mục đích**: Hướng dẫn quy chế phòng thi Writing 60 phút và giải thích quy chuẩn chống gian lận.
- **Chi tiết giao diện**:
  - Tiêu đề: `ETS Writing · Test 01 - Hướng dẫn làm bài thi Writing`.
  - Cấu trúc: 8 câu hỏi trong 60 phút gồm:
    - *Part 1 (Questions 1–5)*: Viết câu theo tranh (Target words).
    - *Part 2 (Questions 6–7)*: Viết email phản hồi yêu cầu.
    - *Part 3 (Question 8)*: Viết bài luận quan điểm (Opinion essay - tối thiểu 300 từ).
  - Cảnh báo quy chế thi: Hệ thống sẽ khóa chức năng copy-paste từ ngoài vào khung soạn thảo.
  - Nút CTA: **[Bắt đầu làm bài thi Writing →]**.

---

### Màn hình 25: [Learner] Exam_Writing_Virtual_Room - /exams/writing/[id]/attempt (Frame ID: `13:69370`)

- **Kích thước**: `1722 x 882 px`.
- **Route**: `/exams/writing/[id]/attempt`.
- **Đặc điểm giao diện & Chống gian lận**:
  - Header: Đồng hồ đếm ngược 60 phút `00:54:12` | Nút **[Nộp bài thi]**.
  - Thanh điều hướng 8 câu hỏi dạng tabs: `Q1` | `Q2` | `Q3` | `Q4` | `Q5` | `Q6` | `Q7` | `Q8`.
  - Khung hiển thị đề bài bên trái (Tranh ảnh / Email yêu cầu / Đề bài luận).
  - **Khung soạn thảo văn bản an toàn bên phải**:
    - Chặn hoàn toàn sự kiện `paste` và menu chuột phải.
    - Bộ đếm từ tự động hiển thị góc dưới: *"320 từ (Khuyến nghị 300-350 từ)"*.
    - Nút chuyển câu: **[← Câu trước]** và **[Lưu & Câu tiếp theo →]**.

---

### Màn hình 26: [Learner] Exam_Writing_Result_Report - /exams/writing/[id]/result/[attemptId] (Frame ID: `24:9528`)

- **Kích thước**: `1722 x 900 px`.
- **Route**: `/exams/writing/[id]/result/[attemptId]`.
- **Chi tiết giao diện**:
  - Tiêu đề: *"WRITING · PERSONAL SCORE REPORT - Kết quả bài thi Writing"*.
  - Điểm tổng ước tính: `150 / 200` (Thang điểm ETS bước nhảy 10 điểm).
  - Tóm tắt bài nộp: `8 / 8 câu đã nộp` · `320 từ bài luận (Đạt độ dài khuyến nghị)` · `5/5 câu Part 1 dùng đúng từ khóa`.
  - Phân tích chi tiết:
    - Điểm Ngữ pháp & Cấu trúc câu.
    - Điểm Từ vựng & Độ phong phú diễn đạt.
    - Điểm Tính logic & Bố cục bài luận.
  - Cụm nút: **[Xem lại bài viết & Bài mẫu]** (`/exams/writing/[id]/review/[attemptId]`).

---

### Màn hình 27: [Learner] Exam_Writing_Review_Mode - /exams/writing/[id]/review/[attemptId] (Frame ID: `24:9605`)

- **Kích thước**: `1722 x 1478 px` (Khung xem lại bài viết và đối sánh bài văn mẫu).
- **Route**: `/exams/writing/[id]/review/[attemptId]`.
- **Chi tiết giao diện**:
  - Header: *"ETS Writing · Test 01 · Lần thi #02 · BẢN NỘP CUỐI · CHỈ ĐỌC"*.
  - Điều hướng danh sách 8 câu hỏi (Part 1, Part 2, Part 3).
  - Split-view đối sánh song song:
    - Cột 1: Đề bài và yêu cầu đề thi.
    - Cột 2: Toàn bộ bài làm nguyên văn mà học viên đã nộp.
    - Cột 3: Bài viết mẫu đạt điểm tối đa (Band Score cao) kèm phân tích ngữ pháp, các cấu trúc câu nâng cao và gợi ý từ đồng nghĩa.

---

## PHÂN HỆ 8: QUẢN TRỊ ADMIN - TỔNG QUAN & NGƯỜI DÙNG (ADMIN CORE)

### Màn hình 28: [Admin] Overview_Dashboard - /admin/overview (Frame ID: `1:823`)

- **Kích thước**: `1722 x 1200 px`.
- **Route**: `/admin/overview`.
- **Mục đích**: Trung tâm điều hành giám sát vận hành toàn hệ thống cho Quản trị viên.
- **Cấu trúc dữ liệu**:
  1. Header & Meta: Breadcrumb `Quản trị / Tổng quan` · Dấu thời gian `Cập nhật: 19/10/2026, 10:30`.
  2. 4 Thẻ KPI vận hành:
     - *Tổng người dùng*: `3,250` (`+120 đăng ký mới tuần này`).
     - *Tài khoản bị khóa*: `14` (`Đang tạm dừng truy cập`).
     - *Kho bài tập*: `1,940 câu` (Phủ khắp 15 Parts của 4 kỹ năng).
     - *Đề thi thử*: `28 đề` (`20 Chính thức / 8 Bản nháp`).
  3. Khối Biểu đồ cơ cấu:
     - Stacked Bar Chart phân bổ câu hỏi: Listening 800 câu, Reading 680 câu, Speaking 270 câu, Writing 190 câu.
     - Donut Chart trạng thái đề thi: 20 đề Chính thức (71%) / 8 đề Bản nháp (29%).
  4. Hoạt động gần đây (Activity Timeline):
     - Ghi nhận hành động của Admin và Operator thời gian thực.
     - System Health Badge: *"Hệ thống đang hoạt động ổn định"*.

---

### Màn hình 29: [Admin] Users_Management - /admin/users (Frame ID: `1:393`)

- **Kích thước**: `1722 x 871 px`.
- **Route**: `/admin/users`.
- **Chi tiết giao diện**:
  - Tiêu đề: `Quản lý người dùng (64 tài khoản)` kèm nút **[+ Thêm người dùng]**.
  - Tabs lọc trạng thái: **[Tất cả (64)]** | **[Hoạt động (45)]** | **[Chờ xác minh (11)]** | **[Bị khóa (8)]**.
  - Toolbar lọc: Ô tìm kiếm tên/email, Dropdown lọc Vai trò, Dropdown lọc Trạng thái, Nút **[Xuất CSV]**.
  - Bảng dữ liệu người dùng (User Data Table):
    - Cột: `Người dùng ↕`, `Vai trò ↕`, `Trạng thái ↕`, `Đăng nhập gần nhất ↕`, `THAO TÁC`.
    - Hành động: Xem chi tiết (Con mắt), Khóa/Mở khóa tài khoản (Ổ khóa).
  - Phân trang: `Hiển thị 1 - 7 của 64 người dùng`.

---

### Màn hình 30: [Admin] Users_Loading_State - /admin/users?state=loading (Frame ID: `13:70854`)

- **Kích thước**: `1722 x 948 px`.
- **Route**: `/admin/users?state=loading`.
- **Mục đích**: Đặc tả trạng thái tải dữ liệu bảng (Skeleton Loading State) chuẩn UX, ngăn giật layout khi fetch API.
- **Chi tiết giao diện**:
  - Giữ nguyên bố cục Sidebar và Header.
  - Toàn bộ các dòng trong Data Table và Badge số lượng được thay thế bằng các thanh Skeleton xám nhạt bo góc hiệu ứng Shimmer nhấp nháy.

---

### Màn hình 31: [Admin] Users_Empty_State - /admin/users?state=empty (Frame ID: `13:70775`)

- **Kích thước**: `1722 x 908 px`.
- **Route**: `/admin/users?state=empty`.
- **Mục đích**: Đặc tả trạng thái không tìm thấy dữ liệu khi áp dụng bộ lọc hoặc tìm kiếm.
- **Chi tiết giao diện**:
  - Vùng bảng dữ liệu hiển thị hình minh họa (Empty Illustration) thân thiện.
  - Thông báo: *"Không tìm thấy người dùng nào phù hợp với bộ lọc hiện tại."*.
  - Nút CTA phụ: **[Đặt lại bộ lọc]** để đưa bảng về trạng thái hiển thị toàn bộ 64 tài khoản.

---

## PHÂN HỆ 9: QUẢN TRỊ ADMIN - KHO BÀI TẬP 4 KỸ NĂNG (EXERCISE BANK CMS)

### Màn hình 32: [Admin] Exercises_LR_Parts - /admin/exercises/lr (Frame ID: `1:1116`)

- **Kích thước**: `1722 x 915 px`.
- **Route**: `/admin/exercises/lr`.
- **Chi tiết giao diện**:
  - Tabs cấp 1: **[Bài tập]** (Active) | **[Đề thi]**.
  - Tabs cấp 2: **[Listening & Reading (7 Parts)]** (Active) | **[Speaking (5 Parts)]** | **[Writing (3 Parts)]**.
  - Thống kê: `Tổng số bài tập: 1,248 | Cập nhật: 17/10/2026`.
  - Lưới quản trị 7 Parts: Part 1 Photographs (186 bài), Part 2 Question-Response (240 bài), Part 3 Short Conversations (174 bài), Part 4 Short Talks (158 bài), Part 5 Incomplete Sentences (226 bài), Part 6 Text Completion (132 bài), Part 7 Reading Comprehension (132 cụm bài).

---

### Màn hình 33: [Admin] Listening_Part1_Management - /admin/exercises/lr/p1 (Frame ID: `1:1389`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exercises/lr/p1`.
- **Chi tiết giao diện**:
  - Quản lý danh sách câu hỏi Part 1 (Photographs).
  - Bảng câu hỏi: Mã ID, Ảnh preview thu nhỏ, Audio player, Transcript 4 đáp án A-B-C-D kèm nhãn `KEY` tại đáp án đúng, Độ khó, Trạng thái.
  - Dialog Thêm câu hỏi mới: Ô tải ảnh (JPG/PNG tối đa 5MB), ô tải file MP3 (tối đa 10MB), 4 radio button chọn đáp án đúng, ô nhập giải thích tiếng Việt.

---

### Màn hình 34: [Admin] Reading_Part7_Management - /admin/exercises/lr/p7 (Frame ID: `13:70307`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exercises/lr/p7`.
- **Mục đích**: Màn hình quản lý chuyên sâu cho Part khó và dài nhất TOEIC Reading: Reading Comprehension.
- **Chi tiết giao diện**:
  - Tiêu đề: *"Part 7: Reading Comprehension - Quản lý 128 cụm bài đọc · 452 câu hỏi con · Single, Double & Triple Passages"*.
  - Nút hành động: **[+ Thêm cụm bài đọc]** và **[Import từ Excel]**.
  - Bảng quản lý cụm bài:
    - Cột: `Mã Cụm bài`, `Loại đoạn văn (Single / Double / Triple)`, `Đoạn văn trích dẫn`, `Số câu hỏi con`, `Chủ đề`, `Độ khó`, `Thao tác`.

---

### Màn hình 35: [Admin] Modal_Add_Reading_Part7_Cluster - Overlay (Frame ID: `13:70476`)

- **Kích thước**: `1722 x 1100 px` (Overlay Modal).
- **Mục đích**: Hộp thoại phức tạp cấu hình cụm bài đọc Part 7 gồm nhiều đoạn văn và các câu hỏi con đi kèm.
- **Chi tiết form**:
  - Chọn cấu trúc đoạn: `Đoạn đơn (Single - 2 đến 4 câu)`, `Đoạn đôi (Double - 5 câu)`, `Đoạn ba (Triple - 5 câu)`.
  - Trình soạn thảo văn bản Rich-text nhập nội dung 1, 2 hoặc 3 bài đọc (email, bài báo, hóa đơn...).
  - Vùng thêm danh sách câu hỏi con: Mỗi câu hỏi gồm tiêu đề câu hỏi, 4 phương án trắc nghiệm A-B-C-D, chọn radio đáp án đúng và lời giải thích.

---

### Màn hình 36: [Admin] Speaking_Part1_Management - /admin/exercises/speaking/p1 (Frame ID: `1:1804`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exercises/speaking/p1`.
- **Chi tiết giao diện**:
  - Tiêu đề: *"Speaking Part 1: Read a text aloud - Quản lý 85 bài tập luyện phát âm, trọng âm và ngữ điệu (Đoạn văn 50-70 từ)"*.
  - Bảng quản lý: Trích dẫn đoạn văn, Thời gian chuẩn bị (45s), Thời gian nói (45s), File audio bài mẫu nghe thử, Trạng thái.
  - Modal chỉnh sửa: Cung cấp bộ đếm từ tự động, tải file audio mẫu `sp1_sample_01.mp3`, ghi chú hướng dẫn phát âm các từ khó.

---

### Màn hình 37: [Admin] Modal_Import_Exercises_Excel - Overlay (Frame ID: `1:2218`)

- **Kích thước**: `1722 x 836 px` (Overlay Modal).
- **Mục đích**: Hộp thoại tải lên tệp Excel (.xlsx) để import bài tập hàng loạt.
- **Chi tiết giao diện**:
  - Banner tải file Excel mẫu chuẩn hóa.
  - Dropzone kéo thả tệp hỗ trợ định dạng `.xlsx`, `.xls` dung lượng tối đa 10MB.
  - Tùy chọn xử lý: Checkbox *"Tự động kích hoạt câu hỏi sau khi nhập"* và *"Bỏ qua câu trùng ID"*.
  - Nút **[Hủy bỏ]** và **[Bắt đầu tải lên]**.

---

### Màn hình 38: [Admin] Writing_Parts - /admin/exercises/writing (Frame ID: `1:2674`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exercises/writing`.
- **Chi tiết giao diện**:
  - Tab kỹ năng: **[Writing (3 Parts)]**.
  - Lưới 3 Part:
    - *Part 1: Write a Sentence* (108 bài tập - Viết câu theo tranh).
    - *Part 2: Respond to a Request* (76 bài tập - Phản hồi email).
    - *Part 3: Write an Opinion Essay* (54 bài tập - Viết luận quan điểm).

---

### Màn hình 39: [Admin] Writing_Part1_Management - /admin/exercises/writing/p1 (Frame ID: `1:2875`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exercises/writing/p1`.
- **Chi tiết giao diện**:
  - Quản lý bài tập viết câu dựa trên hình ảnh và 2 từ khóa bắt buộc.
  - Modal thêm mới: Tải ảnh đề bài, nhập Từ gợi ý 1, nhập Từ gợi ý 2, danh sách các câu viết mẫu đạt điểm tối đa tương đương, tiêu chí chấm ngữ pháp.

---

## PHÂN HỆ 10: QUẢN TRỊ ADMIN - QUẢN LÝ ĐỀ THI & TRÌNH SOẠN THẢO (EXAM BUILDER CMS)

### Màn hình 40: [Admin] Exams_Management - /admin/exams/lr (Frame ID: `1:3237`)

- **Kích thước**: `1722 x 836 px`.
- **Route**: `/admin/exams/lr`.
- **Chi tiết giao diện**:
  - Quản lý danh mục đề thi thử Listening & Reading.
  - Bảng đề thi: `Mã đề`, `Tên đề thi`, `Thời lượng (120 phút)`, `Số câu (200 câu)`, `Trạng thái (Chính thức / Bản nháp)`.
  - Nút hành động: **[+ Tạo đề thi mới]** (Mở Modal 44) và **[Import đề từ Excel]**.

---

### Màn hình 41: [Admin] Exams_Speaking_Management - /admin/exams/speaking (Frame ID: `24:9700`)

- **Kích thước**: `1722 x 931 px`.
- **Route**: `/admin/exams/speaking`.
- **Mục đích**: Màn hình mới được bổ sung chuyên trách quản lý đề thi thử Speaking độc lập.
- **Chi tiết giao diện**:
  - Sub-tab: **[Speaking]** kích hoạt trong menu Đề thi.
  - Header: *"Danh sách đề Speaking | 11 câu · ~20 phút · Thang 200"*.
  - Nút CTA: **[+ Tạo đề Speaking mới]**.
  - Bảng quản lý đề: Mã đề (`SP-2024-01`), Tên đề, Số slot (`11/11 · Đủ`), Barem (`0–200`), Trạng thái (`Chính thức / Bản nháp`).

---

### Màn hình 42: [Admin] Exams_Writing_Management - /admin/exams/writing (Frame ID: `24:9863`)

- **Kích thước**: `1722 x 931 px`.
- **Route**: `/admin/exams/writing`.
- **Mục đích**: Màn hình mới được bổ sung chuyên trách quản lý đề thi thử Writing độc lập.
- **Chi tiết giao diện**:
  - Sub-tab: **[Writing]** kích hoạt trong menu Đề thi.
  - Header: *"Danh sách đề Writing | 8 câu · 60 phút · Thang 200"*.
  - Nút CTA: **[+ Tạo đề Writing mới]**.
  - Bảng quản lý đề: Mã đề (`WR-2024-01`), Tên đề, Số slot (`8/8 · Đủ` hoặc `5/8 · Chưa đủ`), Barem (`0–200`), Trạng thái (`Chính thức / Bản nháp`).

---

### Màn hình 43: [Admin] ExamBuilder_LR_200Slots - /admin/exams/lr/[id]/edit (Frame ID: `1:3475`)

- **Kích thước**: `1722 x 1952 px`.
- **Route**: `/admin/exams/lr/[id]/edit`.
- **Bố cục Master-Detail**:
  - **Top Bar**: Mã đề `ETS-2024-TEST-01`, Badge `Bản nháp`, Tiến độ `145/200 câu hoàn thiện (72.5%)`, Nút **[Xuất bản đề thi]** (Disabled khi chưa đủ 200 câu).
  - **Validation Banner**: Cảnh báo chi tiết vị trí lỗi: *"Part 3 thiếu audio câu 36; Part 7 chưa chọn đáp án câu 180. Hoàn thiện 45 ô trống và sửa 10 ô lỗi."*. Click vào lỗi tự cuộn chuột đến câu đó.
  - **Cột trái (68% - Editor)**: Soạn thảo câu hỏi đơn hoặc cụm câu hỏi dùng chung audio/đoạn văn.
  - **Cột phải (32% - Sticky Palette)**: Ma trận 200 ô vuông phân chia 7 Part, phản hồi đồng bộ thời gian thực theo trạng thái điền dữ liệu của Admin.

---

### Màn hình 44: [Admin] Modal_Create_Exam_Metadata - Overlay (Frame ID: `1:4227`)

- **Kích thước**: `1722 x 836 px` (Overlay Modal).
- **Mục đích**: Bước 1 khởi tạo vỏ đề thi trước khi chuyển hướng vào trình lắp ráp 200 slots.
- **Chi tiết form**:
  - Tiêu đề: *"Khởi tạo đề thi mới"*.
  - Loại đề thi: Chọn tab giữa **[Listening & Reading]** hoặc **[Speaking]** hoặc **[Writing]**.
  - Các trường: Mã đề thi (`ETS-2025-TEST-01`), Tên đề thi, Thời gian thi (phút), Barem quy đổi điểm (Mặc định ETS), Mô tả nguồn đề.
  - Nút: **[Tiếp tục lắp ráp câu hỏi]** (Chuyển tiếp vào Exam Builder).

---

# 4. ĐÁNH GIÁ CHUYÊN MÔN UX/UI & SO SÁNH TIẾN HÓA THIẾT KẾ

### 4.1. Bảng so sánh bước nhảy vọt giữa Bản V1.0 và Bản Hiện Tại (V2.1)

| Tiêu Chí So Sánh | Phiên Bản Cũ (V1.0 - Tháng 10/2026) | Phiên Bản Hiện Tại (V2.1 - Tháng 11/2026) | Đánh Giá Tác Động UX/UI |
| :--- | :--- | :--- | :--- |
| **Quy mô số lượng màn hình** | Chỉ có 14 màn hình sơ khai | **44 màn hình đầy đủ** + **Trang Design System** | Bao phủ 100% User Journey từ Học viên đến Quản trị |
| **Quy chuẩn đặt tên (Naming)** | Tên tự do, chứa nhiều lỗi chính tả (*"người dừng"*, *"Writinig"*) | Chuẩn hóa toàn bộ: `[Role] Name - /route` | Lập trình viên FE nhìn tên frame là biết chính xác route path |
| **Kiến trúc bài thi (Exam Architecture)**| Lẫn lộn giữa 4 kỹ năng trong một bài | Chuẩn hóa **3 Bài thi độc lập**: L&R, Speaking, Writing | Phù hợp 100% với format tổ chức thi quốc tế của IIG/ETS |
| **Phân hệ Học viên (Learner App)** | Hoàn toàn chưa có thiết kế | Hoàn chỉnh từ Landing, Profile, History, Practice đến Phòng thi ảo | Học viên có trải nghiệm học tập và làm bài mượt mà |
| **Phòng thi ảo L&R** | Chỉ có trang soạn thảo Admin | Phòng thi 120p, Locked audio, Question Palette, Confirm Modal | Trải nghiệm thi thử sát thực tế phòng thi thật |
| **Phòng thi Speaking & Writing** | Chưa có | Device check, Two-phase recorder (Beep sound), Anti-cheat editor | Giải quyết trọn vẹn bài toán thi thử tương tác cao |
| **Trạng thái hệ thống (UI States)** | Thiếu Empty State, Loading State | Đầy đủ Skeleton Shimmer và Empty State minh họa | Nâng cao cảm nhận về độ hoàn thiện (Polish) của sản phẩm |
| **Hệ thống Design System** | Rải rác trong các artboard | Trang riêng biệt `11:2` gồm Colors, Typo, Master components | Giúp team Frontend xây dựng UI Component Library cực nhanh |

### 4.2. Các điểm sáng thiết kế vượt trội

1. **Luồng thi Speaking 2 Pha (Preparation → Beep → REC)**: Tái hiện chân thực 100% phòng thi máy tính của IIG/ETS, giúp học viên không bị bỡ ngỡ về tâm lý khi thi thật.
2. **Khung soạn thảo Writing Chống gian lận**: Việc vô hiệu hóa dán văn bản bên ngoài và đếm từ thời gian thực rèn luyện thói quen tư duy và gõ phím trực tiếp bằng tiếng Anh.
3. **Chế độ Xem lại (Interactive Review Mode) có Clue Highlighting**: Tô sáng câu thoại manh mối trong transcript giúp người học hiểu rõ bản chất tại sao mình sai thay vì chỉ nhìn vào đáp án A/B/C/D khô khan.

---

# 5. HƯỚNG DẪN TRIỂN KHAI FRONTEND & CONTRACT INTEGRATION

Dựa trên tài liệu đặc tả chi tiết trên, đội ngũ **Frontend Developer** cần lưu ý các quy chuẩn kỹ thuật khi hiện thực hóa mã nguồn trong kho lưu trữ `TOEICMaster-FE/`:

1. **Khởi tạo Route cấu trúc thư mục Next.js App Router**:
   - `app/(auth)/login/page.tsx`
   - `app/(auth)/register/page.tsx`
   - `app/(auth)/forgot-password/page.tsx`
   - `app/(learner)/page.tsx` (Home Landing Page)
   - `app/(learner)/profile/page.tsx`
   - `app/(learner)/history/page.tsx`
   - `app/(learner)/practice/page.tsx`
   - `app/(learner)/practice/lr/[partId]/page.tsx`
   - `app/(learner)/practice/speaking/[partId]/page.tsx`
   - `app/(learner)/practice/writing/[partId]/page.tsx`
   - `app/(learner)/exams/page.tsx`
   - `app/(learner)/exams/lr/page.tsx` & `app/(learner)/exams/lr/[id]/attempt/page.tsx` & `review/[attemptId]/page.tsx`
   - `app/(learner)/exams/speaking/page.tsx` & `[id]/device-check/page.tsx` & `[id]/attempt/page.tsx`
   - `app/(learner)/exams/writing/page.tsx` & `[id]/attempt/page.tsx` & `review/[attemptId]/page.tsx`
   - `app/admin/overview/page.tsx`
   - `app/admin/users/page.tsx`
   - `app/admin/exercises/lr/page.tsx` & `lr/[partId]/page.tsx`
   - `app/admin/exercises/speaking/[partId]/page.tsx`
   - `app/admin/exercises/writing/[partId]/page.tsx`
   - `app/admin/exams/lr/page.tsx` & `lr/[id]/edit/page.tsx`
   - `app/admin/exams/speaking/page.tsx`
   - `app/admin/exams/writing/page.tsx`
2. **Cấu hình Design Tokens vào `tailwind.config.ts`**:
   - Khai báo đầy đủ các màu thương hiệu (`#FF5A1F`, `#EC251B`), màu Category bài thi (Blue `#1D4ED8`, Purple `#7E22CE`, Green `#047857`), và màu Question Palette.
   - Nhúng phông chữ Google Fonts `Poppins` với các trọng số 400, 500, 600, 700.
3. **Quản lý State & Đồng bộ thời gian thực**:
   - Với phòng thi L&R: Cần bảo đảm Master Audio phát qua HTML5 Web Audio API đồng bộ với Question Palette, lưu đáp án học viên vào LocalStorage/IndexedDB phòng ngừa sự cố mạng.
   - Với phòng thi Speaking: Sử dụng `MediaRecorder API` thu âm định dạng `.webm` / `.mp3`, vẽ dải sóng bằng `AudioContext AnalyserNode`.
   - Với phòng thi Writing: Bắt sự kiện `onPaste={(e) => e.preventDefault()}` và tính số từ bằng regex `text.trim().split(/\s+/).length`.

---

> **KẾT LUẬN & PHÊ DUYỆT TỪ PRODUCT DESIGN LEAD:**  
> Toàn bộ 44 màn hình trên bản vẽ Figma mới nhất và hệ thống Design System Foundations của **TOEICMaster** đã được chuẩn hóa đồng bộ, đạt cấp độ sẵn sàng sản xuất (Production-Ready). Tài liệu đặc tả chi tiết này là căn cứ tối cao phục vụ quá trình phát triển, kiểm thử và nghiệm thu sản phẩm.
