# Hệ thống Design Tokens & Foundations

> Trích xuất từ [specs/figma-screens-master.md](../specs/figma-screens-master.md) (Frame ID: )

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
