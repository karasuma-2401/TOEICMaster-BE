# TÀI LIỆU ĐẶC TẢ YÊU CẦU DỰ ÁN CHI TIẾT (PRODUCT REQUIREMENTS DOCUMENT - PRD / SRS)
## HỆ THỐNG LUYỆN THI THỬ TOEIC TRỰC TUYẾN (TOEICMASTER - MVP)
### ĐẶC TẢ PHÂN HỆ THI THỬ ĐỘC LẬP: LISTENING & READING, SPEAKING VÀ WRITING

---

| **Thông Tin Dự Án** | **Chi Tiết** |
| :--- | :--- |
| **Tên dự án** | **TOEICMaster** — Nền tảng luyện thi & đánh giá năng lực TOEIC trực tuyến |
| **Phiên bản tài liệu** | **2.1** (Cập nhật chuẩn hóa nghiệp vụ: Tách biệt hoàn toàn 3 luồng thi thử độc lập: L&R, Speaking, Writing) |
| **Vai trò người lập** | Senior EdTech Business Analyst (Lead BA) |
| **Đối tượng sử dụng** | UI/UX Designer, Frontend Engineer, Backend Engineer, QA/QC Tester, Product Owner |
| **Mục tiêu phiên bản** | Đặc tả chi tiết toàn bộ tính năng phiên bản MVP phục vụ thiết kế giao diện (UI/UX) và triển khai kỹ thuật |
| **Trạng thái tài liệu** | **Approved for Design & Sprint Implementation** |

---

## MỤC LỤC TỔNG THỂ

1. [TỔNG QUAN DỰ ÁN & CHIẾN LƯỢC MVP (EXECUTIVE SUMMARY)](#1-tổng-quan-dự-án--chiến-lược-mvp)
2. [HỆ THỐNG VAI TRÒ & PHÂN QUYỀN (ACTOR & RBAC SPECIFICATIONS)](#2-hệ-thống-vai-trò--phân-quyền)
3. [KIẾN TRÚC THÔNG TIN & SƠ ĐỒ ĐIỀU HƯỚNG (IA & SITEMAP)](#3-kiến-trúc-thông-tin--sơ-đồ-điều-hướng)
4. [ĐẶC TẢ CHỨC NĂNG PHÂN HỆ HỌC VIÊN (LEARNER APP)](#4-đặc-tả-chức-năng-phân-hệ-học-viên)
   - 4.1. Luồng Xác thực, Khách vãng lai & Hồ sơ cá nhân (Auth, Guest & Profile)
   - 4.2. Phân hệ Luyện tập theo từng Part (Skill & Part-based Practice Mode)
   - 4.3. Phân hệ Thi thử Mô phỏng Chuẩn ETS theo từng Bài thi Độc lập (Independent Mock Test Engines)
     - 4.3.0. Nguyên tắc Tổ chức Thi thử Độc lập & Chu trình Làm bài
     - 4.3.1. Bài thi TOEIC Listening & Reading Độc lập (200 câu - 120 phút)
     - 4.3.2. Bài thi TOEIC Speaking Độc lập (11 câu - ~20 phút)
     - 4.3.3. Bài thi TOEIC Writing Độc lập (8 câu - 60 phút)
   - 4.4. Phân hệ Báo cáo Kết quả & Chế độ Xem lại Riêng biệt (Separate Results & Review Modes)
     - 4.4.1. Báo cáo Kết quả & Review Mode cho bài thi L&R (Thang điểm ETS 990)
     - 4.4.2. Báo cáo Kết quả & Review Mode cho bài thi Speaking (Thang điểm ETS 200)
     - 4.4.3. Báo cáo Kết quả & Review Mode cho bài thi Writing (Thang điểm ETS 200)
   - 4.5. Phân hệ Lịch sử Làm bài & Theo dõi Tiến độ theo từng Kỹ năng (History & Progress)
5. [ĐẶC TẢ CHỨC NĂNG PHÂN HỆ QUẢN TRỊ VIÊN (ADMIN PORTAL)](#5-đặc-tả-chức-năng-phân-hệ-quản-trị-viên)
   - 5.1. Bảng điều khiển Tổng quan Vận hành (Admin Overview Dashboard)
   - 5.2. Quản lý Người dùng & Phân quyền (User Management)
   - 5.3. Quản trị Ngân hàng Bài tập & Câu hỏi lẻ theo Part (Exercise Bank)
   - 5.4. Quản trị Đề thi TOEIC Listening & Reading (Exam Management - L&R)
   - 5.5. Quản trị Đề thi TOEIC Speaking & Writing (Exam Management - S&W)
   - 5.6. Quản lý Thư viện Media (Media Asset Management)
   - 5.7. Quản lý Thang điểm Barem chuẩn ETS (Score Profiles)
6. [HƯỚNG DẪN THIẾT KẾ UX/UI CHO DESIGNER (UX/UI DESIGN DIRECTIVES)](#6-hướng-dẫn-thiết-kế-uxui-cho-designer)
   - 6.1. Nguyên tắc cốt lõi về trải nghiệm người dùng EdTech
   - 6.2. Thiết kế các UI Component đặc thù
   - 6.3. Quy định về trạng thái giao diện (UI States)
   - 6.4. Hướng dẫn Responsive & Breakpoints
7. [YÊU CẦU PHI CHỨC NĂNG & AN TOÀN HỆ THỐNG (NFR & EDGE CASES)](#7-yêu-cầu-phi-chức-năng--an-toàn-hệ-thống)
8. [TIÊU CHÍ NGHIỆM THU TỔNG THỂ (ACCEPTANCE CRITERIA MATRIX)](#8-tiêu-chí-nghiệm-thu-tổng-thể)

---

# 1. TỔNG QUAN DỰ ÁN & CHIẾN LƯỢC MVP

### 1.1. Bối cảnh & Bản chất Nghiệp vụ Thi TOEIC
TOEIC (Test of English for International Communication) bao gồm 4 kỹ năng: Nghe (Listening), Đọc (Reading), Nói (Speaking), Viết (Writing). Tuy nhiên, theo cấu trúc tổ chức thi chuẩn quốc tế của Viện Khảo thí Giáo dục Hoa Kỳ (ETS) và thói quen thực tế của người học tại Việt Nam:
1. **Bài thi Listening & Reading (L&R):** Là bài thi trắc nghiệm trên giấy/máy tính kết hợp 2 kỹ năng Nghe và Đọc, kéo dài **120 phút (200 câu)** với thang điểm quy đổi chung **10 – 990 điểm**.
2. **Bài thi Speaking:** Là bài thi đánh giá kỹ năng Nói độc lập qua máy tính, kéo dài **~20 phút (11 câu)** với thang điểm quy đổi riêng **0 – 200 điểm**.
3. **Bài thi Writing:** Là bài thi đánh giá kỹ năng Viết độc lập qua máy tính, kéo dài **60 phút (8 câu)** với thang điểm quy đổi riêng **0 – 200 điểm**.

> [!IMPORTANT]
> **ĐỊNH HƯỚNG NGHIỆP VỤ CỐT LÕI CỦA HỌC VIÊN KHI LÀM BÀI THI:**
> Hệ thống **KHÔNG bắt buộc và KHÔNG tổ chức thi gộp cả 4 kỹ năng trong cùng một phiên thi duy nhất**. Người học khi vào nền tảng sẽ có toàn quyền lựa chọn làm:
> - **Đề thi TOEIC Listening & Reading riêng** (Làm riêng, tính giờ riêng 120 phút, nộp bài riêng, trả điểm thang 990).
> - **Đề thi TOEIC Speaking riêng** (Làm riêng, tính giờ riêng từng câu theo pha Chuẩn bị/Nói, nộp bài riêng, chấm điểm thang 200).
> - **Đề thi TOEIC Writing riêng** (Làm riêng, tính giờ riêng 60 phút, nộp bài riêng, chấm điểm thang 200).
> Mọi quy trình từ tạo phiên thi (`Attempt`), đồng hồ đếm ngược, nộp bài, chấm điểm và xem lại chi tiết (`Review Mode`) đều diễn ra **hoàn toàn độc lập** cho từng bài thi.

### 1.2. Mục tiêu Phiên bản MVP (Minimum Viable Product)
- **Trụ cột 1 (Học viên - Luyện tập linh hoạt theo Part):** Luyện tập độc lập theo từng Part trong 15 Parts của 4 kỹ năng. Có phản hồi đúng/sai tức thì, transcript, dịch nghĩa và lời giải chi tiết.
- **Trụ cột 2 (Học viên - Thi thử 3 Bài thi Độc lập Chuẩn ETS):** 
  - Phòng thi L&R (200 câu, 120 phút, Master Audio cấm tua/dừng, Question Palette, chấm tự động Barem 990).
  - Phòng thi Speaking (11 câu, 2 pha thời gian, ghi âm tự động, chấm thang 200).
  - Phòng thi Writing (8 câu, 60 phút, chống copy-paste, spellcheck=false, chấm thang 200).
- **Trụ cột 3 (Quản trị viên - Kiểm soát & Quản trị dữ liệu):** Quản lý người dùng (Khóa/Mở khóa), quản lý kho bài tập theo Part, quản lý và xuất bản 3 loại đề thi độc lập, import đề L&R qua Excel XLSX, quản trị kho Media (Ảnh/Audio) và Barem điểm chuẩn ETS.

### 1.3. Bảng Phạm vi MVP (In-Scope vs. Out-of-Scope)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             PHẠM VI DỰ ÁN TOEICMASTER                       │
├──────────────────────────────────────────────┬──────────────────────────────┤
│              IN-SCOPE (MVP)                  │     OUT-OF-SCOPE (PHASE 2)   │
├──────────────────────────────────────────────┼──────────────────────────────┤
│ 1. Đăng ký, đăng nhập email + OTP, Google.   │ 1. Đấu trường thi đấu        │
│ 2. Khách (Guest) làm thử có banner cảnh báo. │    (Battle mode 1vs1).       │
│ 3. Hồ sơ cá nhân (Profile) & Lịch sử làm bài.│ 2. Mạng xã hội, kết bạn, chat│
│ 4. Luyện tập theo từng Part (15 Parts).      │ 3. Sổ tay ghi chép Notion-   │
│ 5. Thi thử ĐỘC LẬP:                          │    like lồng trang.          │
│    - Đề thi Listening & Reading riêng.       │ 4. Chợ tài liệu, E-commerce  │
│    - Đề thi Speaking riêng.                  │    bán khóa học / sách.      │
│    - Đề thi Writing riêng.                   │ 5. AI Chatbot hỏi đáp tự do  │
│ 6. Barem điểm riêng cho từng bài thi         │    về mọi chủ đề.            │
│    (L&R: Thang 990; Speaking: Thang 200;     │ 6. Hệ thống Flashcard Spaced │
│     Writing: Thang 200).                     │    Repetition chuyên sâu.    │
│ 7. Review Mode riêng biệt cho từng bài thi.  │ 7. Cổng thanh toán nạp tiền  │
│ 8. Admin: Quản trị User, Khóa/Mở tài khoản.  │    (VNPAY, MoMo, Stripe).    │
│ 9. Admin: Quản lý Bài tập Part & 3 loại Đề.  │                              │
│ 10. Admin: Import đề thi L&R qua Excel XLSX. │                              │
└──────────────────────────────────────────────┴──────────────────────────────┘
```

---

# 2. HỆ THỐNG VAI TRÒ & PHÂN QUYỀN

### 2.1. Danh sách Tác nhân Hệ thống (Actors)
1. **Khách vãng lai (Guest / Anonymous User):**
   - Người dùng truy cập website nhưng chưa đăng nhập.
   - Có thể xem danh mục đề thi, chọn làm thử một số bài tập Part mẫu hoặc làm 1 bài thi thử độc lập (với mã phiên tạm thời `X-Anonymous-Session`).
   - Có Sticky Banner cảnh báo kết quả không được lưu vĩnh viễn và lời mời đăng ký tài khoản.
2. **Học viên (Learner / Authenticated Member):**
   - Đã xác thực qua Email + OTP hoặc Google Account.
   - Có quyền lựa chọn làm bất kỳ bài thi nào: Đề L&R, Đề Speaking, hoặc Đề Writing.
   - Lịch sử bài thi và điểm số được lưu trữ vĩnh viễn phân theo 3 kỹ năng; được quyền xem lại chi tiết bài làm (Review Mode) kèm transcript, giải thích chi tiết, audio mẫu và bài văn mẫu.
3. **Quản trị viên (Admin / System Administrator):**
   - Tài khoản có quyền tối cao được gán vai trò `Admin`.
   - Toàn quyền truy cập Admin Portal (`/admin`): Quản lý người dùng, tạo và biên tập đề thi/bài tập theo từng kỹ năng, import file Excel, upload file media, cấu hình barem điểm ETS.
4. **Hệ thống tự động (Automated System / Background Worker):**
   - Quản lý đồng hồ thời gian thực độc lập cho từng phiên thi (`Attempt`), tự động khóa bài khi hết giờ, tính điểm theo Barem tương ứng, xử lý hàng đợi đánh giá Writing/Speaking và dọn dẹp file âm thanh tạm thời.

### 2.2. Ma trận Phân quyền Chức năng (RBAC Matrix)

| Nhóm Chức năng / Hành động | Khách (Guest) | Học viên (Learner) | Quản trị viên (Admin) |
| :--- | :---: | :---: | :---: |
| Xem trang chủ, cấu trúc bài thi, danh mục đề | ✅ Có | ✅ Có | ✅ Có |
| Luyện tập theo từng Part (Practice Mode) | ✅ Có (tạm thời) | ✅ Có | ✅ Có (Preview) |
| Chọn và làm đề thi **Listening & Reading** riêng | ✅ Có (Cảnh báo mất data) | ✅ Có (Lưu lịch sử) | ✅ Có (Chế độ Test) |
| Chọn và làm đề thi **Speaking** riêng | ✅ Có (Cảnh báo mất data) | ✅ Có (Lưu lịch sử) | ✅ Có (Chế độ Test) |
| Chọn và làm đề thi **Writing** riêng | ✅ Có (Cảnh báo mất data) | ✅ Có (Lưu lịch sử) | ✅ Có (Chế độ Test) |
| Nhận báo cáo kết quả & điểm số riêng từng bài thi | ✅ Có | ✅ Có | ✅ Có |
| Xem lại chi tiết bài làm (Review Mode riêng biệt) | ⚠️ Giới hạn câu mẫu | ✅ Có | ✅ Có |
| Lưu lịch sử bài thi theo 3 tab (L&R, Speaking, Writing) | ❌ Không | ✅ Có | ✅ Có |
| Cập nhật hồ sơ, đổi mật khẩu, xem tiến độ cá nhân | ❌ Không | ✅ Có | ✅ Có |
| Truy cập cổng quản trị `/admin` | ❌ Không (403/401) | ❌ Không (403/401) | ✅ Có |
| Quản lý tài khoản người dùng (Khóa / Mở khóa) | ❌ Không | ❌ Không | ✅ Có |
| Tạo, sửa, xuất bản (Publish) Đề thi & Bài tập riêng lẻ | ❌ Không | ❌ Không | ✅ Có |
| Upload Media Asset & Quản trị Barem điểm ETS | ❌ Không | ❌ Không | ✅ Có |
| Import đề thi L&R từ file Excel XLSX | ❌ Không | ❌ Không | ✅ Có |

---

# 3. KIẾN TRÚC THÔNG TIN & SƠ ĐỒ ĐIỀU HƯỚNG

### 3.1. Sơ đồ Điều hướng Phân hệ Học viên (Learner Sitemap)
Trang Thư viện đề thi (`/exams`) được tổ chức thành **3 phân khu đề thi độc lập**:

```
[Trang Chủ - Landing Page] (/)
 ├── [Đăng Nhập / Đăng Ký / Quên MK] (/login, /register, /forgot-password)
 ├── [Hồ Sơ Cá Nhân] (/profile)
 ├── [Lịch Sử Làm Bài - 3 Phân Khu] (/history)
 │    ├── Lịch sử Đề Listening & Reading (/history/lr)
 │    ├── Lịch sử Đề Speaking (/history/speaking)
 │    └── Lịch sử Đề Writing (/history/writing)
 ├── [LUYỆN TẬP THEO PART] (/practice)
 │    ├── Luyện tập L&R (Part 1 - Part 7) (/practice/lr/[partId])
 │    ├── Luyện tập Speaking (Part 1 - Part 5) (/practice/speaking/[partId])
 │    └── Luyện tập Writing (Part 1 - Part 3) (/practice/writing/[partId])
 └── [TRUNG TÂM ĐỀ THI THỬ MOCK TEST] (/exams)
      │
      ├── [1] PHÂN KHU ĐỀ LISTENING & READING (/exams/lr)
      │    ├── Danh sách đề L&R (Lọc: Đã làm, Chưa làm, Độ khó) (/exams/lr)
      │    ├── Chi tiết đề & Hướng dẫn thi L&R (/exams/lr/[examId])
      │    ├── Phòng thi trực tuyến L&R (200 câu, 120 phút) (/exams/lr/[examId]/attempt)
      │    ├── Báo cáo kết quả bài thi L&R (Thang điểm 990) (/exams/lr/[examId]/result/[attemptId])
      │    └── Xem lại chi tiết bài thi L&R (Review Mode) (/exams/lr/[examId]/review/[attemptId])
      │
      ├── [2] PHÂN KHU ĐỀ SPEAKING (/exams/speaking)
      │    ├── Danh sách đề Speaking (/exams/speaking)
      │    ├── Kiểm tra Micro & Tai nghe (/exams/speaking/[examId]/device-check)
      │    ├── Phòng thi Speaking (11 câu, 2 pha thời gian) (/exams/speaking/[examId]/attempt)
      │    ├── Báo cáo kết quả bài thi Speaking (Thang điểm 200) (/exams/speaking/[examId]/result/[attemptId])
      │    └── Xem lại & Nghe lại bài thi Speaking (/exams/speaking/[examId]/review/[attemptId])
      │
      └── [3] PHÂN KHU ĐỀ WRITING (/exams/writing)
           ├── Danh sách đề Writing (/exams/writing)
           ├── Chi tiết đề & Hướng dẫn thi Writing (/exams/writing/[examId])
           ├── Phòng thi Writing (8 câu, 60 phút) (/exams/writing/[examId]/attempt)
           ├── Báo cáo kết quả bài thi Writing (Thang điểm 200) (/exams/writing/[examId]/result/[attemptId])
           └── Xem lại & Đọc nhận xét bài thi Writing (/exams/writing/[examId]/review/[attemptId])
```

### 3.2. Sơ đồ Điều hướng Phân hệ Quản trị (Admin Sitemap)
Admin quản lý đề thi và bài tập được phân bổ tương ứng thành các module độc lập:

```
[Cổng Quản Trị Admin] (/admin)
 ├── [Tổng Quan Vận Hành] (/admin/overview)
 ├── [Quản Lý Người Dùng] (/admin/users)
 │    ├── Danh sách tài khoản (/admin/users)
 │    └── Chi tiết người dùng & Lịch sử thi 3 kỹ năng (/admin/users/[userId])
 ├── [Quản Lý Ngân Hàng Bài Tập Theo Part] (/admin/exercises)
 │    ├── Ngân hàng bài tập L&R (Part 1 - 7) (/admin/exercises/lr)
 │    ├── Ngân hàng bài tập Speaking (Part 1 - 5) (/admin/exercises/speaking)
 │    └── Ngân hàng bài tập Writing (Part 1 - 3) (/admin/exercises/writing)
 ├── [Quản Lý Đề Thi Theo Kỹ Năng] (/admin/exams)
 │    ├── Quản lý Đề L&R (200 câu, Master Audio, Barem 990) (/admin/exams/lr)
 │    │    ├── Tạo mới / Fork Revision (/admin/exams/lr/create)
 │    │    ├── Soạn thảo 200 Slot (/admin/exams/lr/[examId]/edit)
 │    │    └── Import đề từ Excel XLSX (/admin/exams/lr/import)
 │    ├── Quản lý Đề Speaking (11 câu, Prep/Resp Time, Rubric 200) (/admin/exams/speaking)
 │    └── Quản lý Đề Writing (8 câu, Target words, Rubric 200) (/admin/exams/writing)
 ├── [Quản Lý Thư Viện Media] (/admin/media)
 │    ├── Kho hình ảnh (/admin/media/images)
 │    └── Kho âm thanh (/admin/media/audio)
 └── [Quản Lý Barem Điểm ETS] (/admin/score-profiles)
```

---

# 4. ĐẶC TẢ CHỨC NĂNG PHÂN HỆ HỌC VIÊN

## 4.1. Luồng Xác thực, Khách vãng lai & Hồ sơ cá nhân

### 4.1.1. Trải nghiệm Khách vãng lai (Guest Mode)
- **Mục tiêu UX:** Tạo cơ hội trải nghiệm trực tiếp tính năng làm bài mà không gặp rào cản đăng ký ngay từ đầu.
- **Quy tắc Nghiệp vụ:**
  - Khách vãng lai có thể chọn làm thử một đề L&R riêng, đề Speaking riêng, đề Writing riêng hoặc làm bài tập Part bất kỳ.
  - Phía trên cùng màn hình làm bài của Khách luôn có **Sticky Alert Banner (Màu vàng cam nhẹ)**:
    > *"Bạn đang làm bài với tư cách Khách. Kết quả bài làm của bạn sẽ không được lưu vào lịch sử cá nhân sau khi đóng trình duyệt. [Đăng ký miễn phí] để lưu giữ lộ trình ôn luyện."*
  - Khi Khách bấm "Nộp bài", hệ thống vẫn tính điểm và hiển thị kết quả bài thi đó. Tuy nhiên, nút *"Lưu kết quả"* hoặc *"Xem lại chi tiết 100% lời giải"* sẽ mở Modal mời đăng ký tài khoản (Google 1-click hoặc Email).
  - Không tạo tài khoản ngầm; sử dụng định danh phiên tạm thời (`X-Anonymous-Session`) lưu trong Cookie/Local Storage.

### 4.1.2. Đăng ký, Đăng nhập Email + OTP & Đăng nhập Google
- **Đăng ký Email:** Nhập Tên hiển thị (1-100 ký tự), Email hợp lệ, Mật khẩu (tối thiểu 8 ký tự, có thước đo độ mạnh yếu). Backend gửi OTP 6 số vào email, xác thực trong vòng 10 phút (giới hạn 5 lần nhập sai, cooldown gửi lại 60s).
- **Đăng nhập Google:** Nút *"Tiếp tục với Google"*. Bắt sự kiện khi người dùng đóng popup thì không reload trang, giữ nguyên trạng thái form và thông báo nhẹ *"Đã hủy đăng nhập Google"*. Tự động tạo tài khoản role `Learner` nếu tài khoản Google lần đầu truy cập.
- **Hồ sơ Cá nhân (Profile):** Hiển thị avatar (hỗ trợ upload ảnh JPG/PNG/WebP <= 2MB kèm cắt tròn), Tên hiển thị (cho phép sửa), Email (chỉ đọc có icon khóa), Target Score mong muốn và Đổi mật khẩu.

---

## 4.2. Phân hệ Luyện tập theo từng Part (Skill & Part-based Practice Mode)

Chế độ Luyện tập theo Part phục vụ việc **học tập sâu và củng cố kiến thức từng dạng bài**, tách biệt hoàn toàn với bài thi thử.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                 BẢNG PHÂN BỔ 15 PARTS LUYỆN TẬP ĐỘC LẬP                    │
├──────────────────────────┬──────────────────────────────────────────────────┤
│ KỸ NĂNG                  │ CÁC PART LUYỆN TẬP RIÊNG BIỆT                    │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 1. TOEIC Listening       │ Part 1: Mô tả tranh (Photographs - 4 phương án)  │
│                          │ Part 2: Hỏi & Đáp (Question-Response - 3 PA)     │
│                          │ Part 3: Hội thoại ngắn (Conversations - Cụm 3 câu)│
│                          │ Part 4: Bài nói chuyện ngắn (Talks - Cụm 3 câu)  │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 2. TOEIC Reading         │ Part 5: Điền câu (Incomplete Sentences)          │
│                          │ Part 6: Điền đoạn văn (Text Completion)          │
│                          │ Part 7: Đọc hiểu văn bản (Reading Comprehension) │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 3. TOEIC Speaking        │ Part 1: Đọc to đoạn văn (Read a text aloud)      │
│                          │ Part 2: Miêu tả tranh (Describe a picture)       │
│                          │ Part 3: Trả lời câu hỏi ngắn (Respond to Qs)     │
│                          │ Part 4: Trả lời theo thông tin cho sẵn (Docs)    │
│                          │ Part 5: Trình bày quan điểm (Express an opinion) │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ 4. TOEIC Writing         │ Part 1: Viết 1 câu dựa trên tranh & 2 từ khóa   │
│                          │ Part 2: Viết email phản hồi yêu cầu (Directions) │
│                          │ Part 3: Viết bài luận quan điểm (Tối thiểu 300 từ)│
└──────────────────────────┴──────────────────────────────────────────────────┘
```

- **Đặc trưng Giao diện Chế độ Luyện tập:**
  - Không giới hạn thời gian gắt gao; người học tự kiểm soát nhịp độ.
  - **Phản hồi tức thì (Instant Feedback):** Chọn phương án xong thì hệ thống báo ngay Đúng (Xanh) / Sai (Đỏ kèm đáp án đúng).
  - **Audio Player có điều khiển đầy đủ:** Tua tới/lui 5 giây, lặp đoạn A-B, điều chỉnh tốc độ nghe (0.8x, 1.0x, 1.2x).
  - **Hộp thoại Giải thích Thông minh (Explanation Drawer):** Mở ra ngay dưới câu hỏi, gồm 3 tab: Lời giải chi tiết, Transcript song ngữ Anh - Việt có phát lại câu tương ứng, Từ vựng trọng tâm.
  - **Luyện Speaking:** Thu âm thử -> Nghe lại âm thanh của chính mình -> So sánh với Audio mẫu chuẩn bản xứ và Transcript mẫu.
  - **Luyện Writing:** Khung nhập văn bản đếm từ thời gian thực, kiểm tra sự xuất hiện của từ khóa bắt buộc (Part 1), đối chiếu bài văn mẫu điểm tối đa và tiêu chí chấm.

---

## 4.3. Phân hệ Thi thử Mô phỏng Chuẩn ETS theo từng Bài thi Độc lập (Independent Mock Test Engines)

### 4.3.0. Nguyên tắc Tổ chức Thi thử Độc lập & Chu trình Làm bài
Người học khi vào Thư viện đề thi (`/exams`) sẽ nhìn thấy **3 phân khu đề thi riêng biệt**. Người học bấm chọn loại đề mình muốn thi:

```
                  ┌───────────────────────────────┐
                  │    THƯ VIỆN ĐỀ THI (/exams)   │
                  └───────────────┬───────────────┘
          ┌───────────────────────┼───────────────────────┐
          ▼                       ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ [1] ĐỀ THI L&R   │    │ [2] ĐỀ SPEAKING  │    │  [3] ĐỀ WRITING  │
│ - 200 câu hỏi    │    │ - 11 câu hỏi     │    │ - 8 câu hỏi      │
│ - 120 phút       │    │ - ~20 phút       │    │ - 60 phút        │
│ - Barem ETS 990  │    │ - Barem ETS 200  │    │ - Barem ETS 200  │
└─────────┬────────┘    └─────────┬────────┘    └─────────┬────────┘
          │                       │                       │
     [Thi L&R]               [Thi Speaking]          [Thi Writing]
          │                       │                       │
          ▼                       ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ KẾT QUẢ ĐỀ L&R   │    │ KẾT QUẢ SPEAKING │    │ KẾT QUẢ WRITING  │
│ (Thang điểm 990) │    │ (Thang điểm 200) │    │ (Thang điểm 200) │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

Mỗi bài thi là một phiên độc lập (`Attempt` riêng biệt):
- Không có sự ràng buộc phải làm bài thi này mới được làm bài thi kia.
- Đồng hồ đếm ngược và bộ câu hỏi độc lập.
- Nộp bài độc lập và chấm điểm theo Barem chuẩn của bài thi đó.

---

### 4.3.1. Bài thi TOEIC Listening & Reading Độc lập (200 câu - 120 phút)

#### A. Màn hình Chờ & Chuẩn bị (Pre-exam Instructions)
- Tên đề: Ví dụ *"ETS 2026 Test 1 - Listening & Reading"*.
- Thể thức: Đúng 200 câu hỏi chia làm 2 phần: Listening (Câu 1-100) và Reading (Câu 101-200).
- Tổng thời gian: 120 phút liên tục.
- Quy định:
  - File Master Audio phần Listening tự động phát liên tục từ đầu đến cuối, cấm tua và cấm dừng.
  - Khi luồng âm thanh phần Listening kết thúc hoặc chuyển sang câu hỏi kế tiếp, câu hỏi trước đó sẽ bị khóa để ngăn nghe lại hoặc đổi đáp án.
  - Hết 120 phút, hệ thống tự động thu bài.
- Nút bấm chính: **[BẮT ĐẦU LÀM BÀI L&R]**.

#### B. Giao diện Phòng thi Ảo L&R (Virtual Exam Room Layout)
- **Khu vực 1 (Topbar cố định):**
  - Tên đề thi, huy hiệu *"Listening & Reading"*.
  - Đồng hồ đếm ngược đồng bộ Server dạng `01:59:45`.
  - Nút **[Nộp bài L&R]**.
- **Khu vực 2 (Split-View bài làm):**
  - Cột trái (55%): Player Master Audio (phát liên tục), hình ảnh minh họa (Part 1, 3, 4) hoặc đoạn văn bản đọc hiểu (Part 6, 7). Hỗ trợ thanh cuộn độc lập và công cụ bôi vàng (Highlighter).
  - Cột phải (45%): Câu hỏi và các phương án chọn A, B, C, D (hoặc A, B, C ở Part 2). Nút gắn cờ phân vân (Flag) và nút chuyển câu.
- **Khu vực 3 (Ma trận 200 câu hỏi - Question Palette):**
  - Hiển thị đầy đủ lưới 200 ô vuông.
  - Mã màu trực quan: Trắng (chưa làm), Xanh lá (đã chọn đáp án), Cam (câu đang làm, highlight đồng bộ toàn bộ cụm bài đọc khi click vào 1 câu con), Cờ vàng (đã gắn cờ).
  - Tab lọc nhanh: *Tất cả (200)*, *Listening (1-100)*, *Reading (101-200)*.

#### C. Quy tắc Kỹ thuật Nghiêm ngặt phòng thi L&R
1. **Server-synced Countdown:** Thời gian kết thúc = `StartTimeUtc + 120 phút`. Tự động đồng bộ với Server dù học viên đổi giờ máy tính hay F5 lại trang. `<= 5 phút` đổi màu cam; `<= 1 phút` đổi màu đỏ nhấp nháy; `00:00:00` tự động nộp bài.
2. **Khóa chức năng tua âm thanh:** Master Audio phát liên tục, ẩn seekbar và nút pause. Khóa vĩnh viễn 100 câu Listening khi đã chuyển sang phần Reading (từ câu 101).
3. **Autosave tức thì:** Lưu vào Local Storage và gửi ngầm lên Server sau mỗi thao tác chọn đáp án (debounce 500ms). Rớt mạng hoặc tắt tab mở lại sẽ khôi phục 100% bài làm.
4. **Modal Xác nhận Nộp bài L&R:** Thống kê: *"Đã làm X/200 câu, còn trống Y câu. Bạn có chắc chắn muốn nộp bài thi L&R?"*.
5. **Anti-cheating:** Chặn chuột phải, chặn copy văn bản, chặn inspect F12; API trả về đề thi tuyệt đối không chứa `correctKey`, `transcript` hay `explanation`.

---

### 4.3.2. Bài thi TOEIC Speaking Độc lập (11 câu - ~20 phút)

#### A. Kiểm tra Thiết bị Bắt buộc (Device Check)
Học viên chọn thi Speaking bắt buộc phải hoàn thành 3 bước kiểm tra trước khi vào thi:
1. Cấp quyền Microphone cho trình duyệt.
2. Nói thử 1 câu để kiểm tra thanh âm lượng bắt mic màu xanh lá.
3. Nghe thử đoạn âm thanh mẫu để xác nhận loa/tai nghe hoạt động tốt.
- Hoàn tất 3 bước mới mở khóa nút: **[VÀO THI SPEAKING]**.

#### B. Cơ chế Chuyển Pha 2 Giai đoạn Tự động cho 11 Câu Speaking
Mỗi câu Speaking vận hành tự động theo chu trình nghiêm ngặt chuẩn ETS:
- **Pha 1 (Chuẩn bị):** Màn hình hiện đề bài (Đoạn văn, ảnh, lịch trình hoặc câu hỏi). Đồng hồ đếm ngược thời gian chuẩn bị (ví dụ 45s). Micro ở trạng thái TẮT.
- **Pha chuyển tiếp:** Đồng hồ chuẩn bị về 0, loa phát tiếng "Bíp" chuẩn ETS.
- **Pha 2 (Trả lời):** Đồng hồ đếm ngược thời gian trả lời bắt đầu chạy. Micro BẬT TỰ ĐỘNG, icon REC đỏ nhấp nháy, hiển thị sóng âm thời gian thực (Live Audio Waveform) phản hồi giọng nói.
- **Kết thúc câu:** Đồng hồ về 0, hệ thống tự động ngắt thu âm ngay lập tức. **Cấm thu âm lại**. Hệ thống lưu file âm thanh trong tối đa 2 giây rồi tự động nhảy sang câu hỏi tiếp theo.

#### C. Bảng Cấu hình Thời gian 11 Câu Đề thi Speaking

| Câu hỏi | Part | Dạng câu hỏi | Thời gian chuẩn bị | Thời gian trả lời |
| :---: | :---: | :--- | :---: | :---: |
| **Câu 1 - 2** | Part 1 | Read a text aloud (Đọc đoạn văn) | 45 giây | 45 giây |
| **Câu 3 - 4** | Part 2 | Describe a picture (Miêu tả tranh) | 45 giây | 30 giây |
| **Câu 5 - 6** | Part 3 | Respond to questions (Hỏi đáp nhanh) | 3 giây | 15 giây |
| **Câu 7** | Part 3 | Respond to questions (Hỏi đáp sâu) | 3 giây | 30 giây |
| **Câu 8 - 9** | Part 4 | Respond to questions using info | Đọc tài liệu: 45s / Từng câu: 3s | 15 giây |
| **Câu 10** | Part 4 | Respond to questions using info | 3 giây | 30 giây |
| **Câu 11** | Part 5 | Express an opinion (Trình bày quan điểm) | 45 giây | 60 giây |

---

### 4.3.3. Bài thi TOEIC Writing Độc lập (8 câu - 60 phút)

#### A. Cấu trúc Giao diện Phòng thi Writing
- **Header cố định:** Tên đề thi, huy hiệu *"Writing"*, đồng hồ tổng đếm ngược 60 phút (hoặc đếm ngược từng phần), nút **[Nộp bài Writing]**.
- **Bố cục 2 phần:**
  - Khung đề bài:
    - *Câu 1 - 5 (Part 1):* Hiển thị bức ảnh và 2 từ khóa bắt buộc bên dưới ảnh.
    - *Câu 6 - 7 (Part 2):* Hiển thị email gửi đến kèm 2-3 yêu cầu chỉ dẫn bắt buộc.
    - *Câu 8 (Part 3):* Hiển thị đề bài luận quan điểm.
  - Khung Soạn thảo Văn bản:
    - Thẻ `<textarea>` chuyên dụng.
    - Bộ đếm từ thời gian thực (Word Count Indicator).
    - Cảnh báo từ khóa tự động gạch xanh khi từ khóa xuất hiện (với Part 1).

#### B. Ràng buộc Kỹ thuật Nghiêm ngặt phòng thi Writing
1. **Khóa kiểm tra chính tả:** `spellcheck="false"`, chặn các tiện ích mở rộng can thiệp như Grammarly, LanguageTool.
2. **Chặn dán nội dung từ bên ngoài:** Sự kiện `onPaste` chặn toàn bộ dữ liệu clipboard sao chép từ ngoài trình duyệt. Thí sinh chỉ được thao tác gõ trực tiếp hoặc copy/paste nội dung do chính mình vừa gõ trong khung bài thi.
3. **Autosave văn bản:** Tự động lưu văn bản lên Server sau mỗi 800ms ngừng gõ phím. Đảm bảo khôi phục nguyên vẹn 100% nếu có sự cố mất mạng hoặc tải lại trang.

---

## 4.4. Phân hệ Báo cáo Kết quả & Chế độ Xem lại Riêng biệt (Separate Results & Review Modes)

> [!IMPORTANT]
> Vì người học thi các bài thi độc lập, hệ thống cung cấp **3 màn hình Báo cáo kết quả và 3 Chế độ Xem lại (Review Mode) riêng biệt**, tối ưu hóa cho đặc thù của từng bài thi.

### 4.4.1. Báo cáo Kết quả & Review Mode cho bài thi L&R (Thang điểm ETS 990)
- **Màn hình Báo cáo Kết quả L&R:**
  - **Điểm số Tổng quan:** Hiển thị điểm quy đổi chuẩn ETS **10 – 990 điểm** (In to, nổi bật).
  - **Điểm thành phần:** Điểm Listening: **... / 495** | Điểm Reading: **... / 495**.
  - **Số câu đúng:** Listening: X/100 câu | Reading: Y/100 câu.
  - **Biểu đồ Năng lực 7 Parts:** Biểu đồ thanh ngang hiển thị % làm đúng từ Part 1 đến Part 7, chỉ rõ Part nào là điểm mạnh và Part nào là lỗ hổng cần ôn luyện.
- **Chế độ Xem lại Chi tiết L&R (Review Mode):**
  - Mở lại toàn bộ 200 câu dưới dạng Split-View (Read-only).
  - Ma trận 200 câu phân loại màu: Xanh lá (Đúng), Đỏ (Sai), Xám (Chưa làm). Bộ lọc nhanh: *"Chỉ xem câu sai"*, *"Xem câu gắn cờ"*.
  - Tại từng câu: Hiển thị rõ phương án người học đã chọn (nếu sai viền đỏ) và đáp án đúng chuẩn ETS (viền xanh lá đậm).
  - Hộp thoại Học thuật: Audio player nghe lại đoạn nghe của câu đó, **Interactive Transcript** song ngữ Anh - Việt (bôi vàng câu chứa bằng chứng chọn đáp án), dịch nghĩa toàn bộ đoạn văn và giải thích chi tiết ngữ pháp/từ vựng.

### 4.4.2. Báo cáo Kết quả & Review Mode cho bài thi Speaking (Thang điểm ETS 200)
- **Màn hình Báo cáo Kết quả Speaking:**
  - **Điểm số Tổng quan:** Điểm quy đổi ước tính **0 – 200 điểm** (bước nhảy 10 điểm).
  - Trạng thái đánh giá: *"Đã hoàn thành đánh giá"* (hoặc *"Đang trong hàng đợi đánh giá..."* nếu chờ xử lý).
  - Đánh giá theo 5 tiêu chí chuẩn ETS: Phát âm (Pronunciation), Ngữ điệu & Trọng âm (Intonation & Stress), Ngữ pháp (Grammar), Từ vựng (Vocabulary), Tính liên kết & Phù hợp nội dung (Cohesion & Relevance).
- **Chế độ Xem lại Chi tiết Speaking (Review Mode):**
  - Danh sách 11 câu hỏi Speaking phân theo 5 Part.
  - Tại mỗi câu:
    - Audio Player phát lại **chính xác đoạn thu âm giọng nói của học viên** trong kỳ thi.
    - Audio Player phát **đoạn âm thanh mẫu (Native Speaker Sample Audio)** chuẩn bản xứ.
    - Transcript bài nói mẫu đạt điểm tối đa kèm phân tích cách nhấn nhá ngữ điệu.

### 4.4.3. Báo cáo Kết quả & Review Mode cho bài thi Writing (Thang điểm ETS 200)
- **Màn hình Báo cáo Kết quả Writing:**
  - **Điểm số Tổng quan:** Điểm quy đổi ước tính **0 – 200 điểm** (bước nhảy 10 điểm).
  - Phân tích chi tiết 3 phần:
    - Part 1 (Câu 1-5): Đánh giá ngữ pháp, sự phù hợp với tranh và việc sử dụng đúng 2 từ khóa bắt buộc.
    - Part 2 (Câu 6-7): Đánh giá chất lượng và độ hoàn thiện của email phản hồi theo các chỉ dẫn.
    - Part 3 (Câu 8): Đánh giá bài luận quan điểm (Bố cục mở-thân-kết, luận điểm, phát triển ý, từ vựng học thuật).
- **Chế độ Xem lại Chi tiết Writing (Review Mode):**
  - Hiển thị lại nguyên văn toàn bộ bài viết mà học viên đã gõ trong kỳ thi.
  - Đánh dấu các lỗi ngữ pháp/chính tả cần khắc phục.
  - Cung cấp **Bài văn mẫu đạt điểm tối đa (High-score Model Essay)** chuẩn ETS để học viên đối chiếu và học hỏi cách hành văn.

---

## 4.5. Phân hệ Lịch sử Làm bài & Theo dõi Tiến độ theo từng Kỹ năng (History & Progress)
- Màn hình Lịch sử làm bài (`/history`) chia thành **3 Tab độc lập**:
  - **Tab 1: Lịch sử Đề Listening & Reading:** Danh sách các lượt thi L&R, điểm 990, điểm L/R, ngày thi, nút mở lại Review Mode L&R.
  - **Tab 2: Lịch sử Đề Speaking:** Danh sách các lượt thi Speaking, điểm 200, ngày thi, nút mở lại Review Mode Speaking.
  - **Tab 3: Lịch sử Đề Writing:** Danh sách các lượt thi Writing, điểm 200, ngày thi, nút mở lại Review Mode Writing.
- Biểu đồ đường xu hướng (Progression Chart): Học viên có thể chọn xem biểu đồ tiến độ điểm số riêng cho từng kỹ năng qua thời gian.

---

# 5. ĐẶC TẢ CHỨC NĂNG PHÂN HỆ QUẢN TRỊ VIÊN (ADMIN PORTAL)

## 5.1. Bảng điều khiển Tổng quan Vận hành (Admin Overview Dashboard)
- Đường dẫn: `/admin/overview`
- **4 Thẻ Chỉ số KPI:**
  - Tổng số học viên trên hệ thống & Tăng trưởng tuần.
  - Tổng số lượt nộp bài thi (phân tách rõ: Lượt nộp L&R, Speaking, Writing).
  - Kho đề thi sẵn sàng (`PUBLISHED`): Thống kê số lượng đề L&R, đề Speaking, đề Writing.
  - Trạng thái hàng đợi đánh giá AI Writing/Speaking.
- **Trung tâm Cảnh báo Cần Hành động:** Cảnh báo đề Draft thiếu audio/barem, bài nộp bị lỗi đánh giá.

---

## 5.2. Quản lý Người dùng & Phân quyền (User Management)
- Đường dẫn: `/admin/users`
- Danh sách tài khoản phân trang (20 users/trang), tìm kiếm theo Tên/Email, lọc theo Trạng thái (`Active`, `Pending`, `Locked`), lọc theo Vai trò (`Learner`, `Admin`).
- **Thao tác Khóa tài khoản:** Mở modal, bắt buộc nhập lý do khóa. Lập tức thu hồi session, tài khoản bị đăng xuất ngay.
- **Thao tác Mở khóa tài khoản:** Khôi phục trạng thái hoạt động.
- **Xem chi tiết người dùng:** Xem thông tin hồ sơ và danh sách các bài thi L&R, Speaking, Writing người này đã làm.

---

## 5.3. Quản trị Ngân hàng Bài tập & Câu hỏi lẻ theo Part (Exercise Bank)
- Đường dẫn: `/admin/exercises`
- Quản lý kho câu hỏi độc lập dùng cho chế độ Luyện tập theo Part (hoàn toàn tách biệt với kho câu hỏi của đề thi).
- Phân chia theo 3 nhóm: L&R (Part 1-7), Speaking (Part 1-5), Writing (Part 1-3).
- Hỗ trợ câu hỏi đơn (Part 1, 2, 5) và cụm câu hỏi dùng chung ngữ cảnh Text/Audio/Image (Part 3, 4, 6, 7).
- Gán nhãn Part bắt buộc, Mức độ khó (Dễ/Trung bình/Khó), Chủ đề (Topic).
- Nhập Transcript, Bản dịch nghĩa tiếng Việt, Lời giải thích chi tiết, Audio mẫu và Bài văn mẫu.
- Công cụ Import bài tập theo Part qua file Excel có preview và validate lỗi từng dòng.

---

## 5.4. Quản trị Đề thi TOEIC Listening & Reading (Exam Management - L&R)
- Đường dẫn: `/admin/exams/lr`
- **Cơ chế Phiên bản Bất biến (Versioned Revisions):** Đề đã xuất bản (`PUBLISHED`) là bất biến để bảo vệ dữ liệu lịch sử của học viên. Khi cần sửa, Admin bấm **[Fork New Draft]** để tạo bản nháp mới.
- **Khung cố định 200 Slot chuẩn ETS:** Tự động sinh khung 200 câu theo đúng cấu trúc nhóm chuẩn (Part 1: 6 câu, Part 2: 25 câu, Part 3: 39 câu, Part 4: 30 câu, Part 5: 30 câu, Part 6: 16 câu, Part 7: 54 câu).
- **Quản lý Master Audio:** Đề thi L&R bắt buộc tải lên duy nhất 1 file âm thanh tổng (.mp3 hoặc .wav <= 100MB). Không cho phép upload audio lẻ ở các câu con.
- **Publish Checklist:** Chặn xuất bản nếu chưa đủ 200/200 câu, câu nào chưa có đáp án, chưa upload Master Audio, hoặc chưa liên kết Score Profile (Barem điểm 990).
- **Import đề thi L&R qua Excel XLSX 4 sheets:** `Manifest`, `Blocks`, `ContextBlocks`, `Questions` đính kèm file Master Audio.

---

## 5.5. Quản trị Đề thi TOEIC Speaking & Writing (Exam Management - S&W)

### 5.5.1. Quản lý Đề thi Speaking (/admin/exams/speaking)
- Soạn thảo đề Speaking độc lập gồm đúng 11 câu hỏi thuộc 5 Part.
- Cấu hình từng câu: Thời gian chuẩn bị (giây) và Thời gian trả lời (giây).
- Tải lên file audio câu hỏi, hình ảnh tranh minh họa (Part 2), bảng tài liệu (Part 4).
- Tải lên file Audio mẫu chuẩn bản xứ và Transcript mẫu đạt điểm tối đa.
- Ràng buộc xuất bản: Đủ 11/11 câu, 100% câu đã cấu hình thời gian chuẩn và có bài mẫu.

### 5.5.2. Quản lý Đề thi Writing (/admin/exams/writing)
- Soạn thảo đề Writing độc lập gồm đúng 8 câu hỏi thuộc 3 Part:
  - Part 1 (Câu 1-5): Upload 1 ảnh tranh + Nhập đúng 2 từ khóa bắt buộc.
  - Part 2 (Câu 6-7): Nhập email gửi đến + Danh sách các chỉ dẫn trả lời.
  - Part 3 (Câu 8): Nhập đề bài luận + Cấu hình số từ tối thiểu khuyến nghị.
- Nhập bài văn mẫu (Model Essay) và tiêu chí chấm điểm (Rubric) cho từng câu.
- Ràng buộc xuất bản: Đủ 8/8 câu, đầy đủ từ khóa, đề bài và bài văn mẫu.

---

## 5.6. Quản lý Thư viện Media (Media Asset Management)
- Lưu trữ trên Object Storage (MinIO S3).
- Hình ảnh: PNG, JPG, JPEG, WebP `<= 5MB`. Kiểm tra file signature, trả về Asset UUID bất biến.
- Âm thanh: MP3, WAV `<= 15MB` (với bài tập lẻ) hoặc `<= 100MB` (với Master Audio đề L&R).
- Trình xem trước dạng Lightbox cho ảnh và Trình phát âm thanh có sóng nhạc cho audio.

---

## 5.7. Quản lý Thang điểm Barem chuẩn ETS (Score Profiles)
- Đường dẫn: `/admin/score-profiles`
- Barem quy đổi chuẩn ETS 990 cho bài thi Listening & Reading:
  - Bảng 101 mốc quy đổi Listening (0-100 câu đúng -> 5-495 điểm).
  - Bảng 101 mốc quy đổi Reading (0-100 câu đúng -> 5-495 điểm).
- Admin có thể tạo nhiều Score Profile khác nhau và gán cho các đề thi L&R tương ứng.

---

# 6. HƯỚNG DẪN THIẾT KẾ UX/UI CHO DESIGNER

### 6.1. Nguyên tắc Cốt lõi về Trải nghiệm Người dùng EdTech
1. **Rõ ràng & Phân định Bài thi:** Giao diện Thư viện đề thi (`/exams`) phải thể hiện rõ 3 lựa chọn thẻ độc lập: *Đề Listening & Reading*, *Đề Speaking*, *Đề Writing*. Không gây hiểu lầm rằng người học phải thi gộp cả 4 kỹ năng trong một lần.
2. **Tập trung Cao độ trong Phòng thi:** Khi vào phòng thi bất kỳ (L&R, Speaking, Writing), ẩn hoàn toàn Header/Footer của web, chỉ giữ lại Topbar phòng thi (Tên đề, huy hiệu kỹ năng, đồng hồ đếm ngược, nút nộp bài).
3. **Phản hồi Rõ Ràng & Nhân văn:** 
   - Luyện tập: Thưởng cảm xúc khi làm đúng, giải thích ân cần khi làm sai.
   - Báo cáo kết quả: Mã màu rõ ràng, phân tích điểm mạnh/yếu cụ thể theo từng kỹ năng học viên vừa hoàn thành.

### 6.2. Thiết kế các UI Component Đặc thù
1. **Thẻ Chọn Bài Thi (Exam Category Cards - Trang /exams):**
   - 3 Card lớn nổi bật đại diện cho 3 bài thi:
     - Card L&R: Icon tai nghe & cuốn sách, nhãn *"200 câu | 120 phút | Thang 990"*.
     - Card Speaking: Icon micro, nhãn *"11 câu | 20 phút | Thang 200"*.
     - Card Writing: Icon cây bút, nhãn *"8 câu | 60 phút | Thang 200"*.
2. **Ma trận Câu hỏi L&R (Question Palette - 200 ô):** Ô kích thước `36x36px`, bo góc `6px`. Trắng (chưa làm), Xanh lá (đã làm), Cam (đang chọn / highlight cả chùm câu con), Cờ vàng (gắn cờ).
3. **Đồng hồ Đếm ngược HUD (Countdown Timer):** Font Monospace (`Roboto Mono`). >5 phút (chữ đen/xám), <=5 phút (nền vàng, chữ cam), <=1 phút (nền đỏ, chữ đỏ nhấp nháy).
4. **Trình chia Đôi Màn hình (Split-View):** Áp dụng cho Part 6, 7 (L&R) và Part 2, 3 (Writing). Cột tài liệu/ngữ cảnh (55%) và Cột câu hỏi/soạn thảo (45%) cuộn độc lập.
5. **Khung Ghi âm Speaking (Waveform Visualizer):** Vòng tròn chuyển từ Xám (Pha Chuẩn bị) sang Đỏ REC (Pha Trả lời) kèm thanh sóng âm thời gian thực dao động theo giọng nói.

### 6.3. Quy định về Trạng thái Giao diện (UI States)
Mọi màn hình và Component bắt buộc thiết kế đủ 5 trạng thái:
1. **Normal State:** Dữ liệu đầy đủ, cân đối.
2. **Hover / Focus / Active State:** Hiệu ứng chuyển động mượt mà (<200ms).
3. **Loading State:** Sử dụng **Skeleton UI (Khung xương xám nhấp nháy)** thay vì vòng xoay spinner đơn điệu.
4. **Empty State:** Hình minh họa thân thiện kèm nút kêu gọi hành động (CTA): *"Chưa có bài thi nào trong lịch sử. [Khám phá đề thi ngay]"*.
5. **Error State:** Banner thông báo sự cố mạng hoặc lỗi hệ thống, cam kết dữ liệu bài làm đã được lưu an toàn.

### 6.4. Hướng dẫn Responsive & Breakpoints
- **Phòng thi thử L&R, Speaking, Writing:** Tối ưu hóa **Desktop-First (>= 1024px)** để bảo đảm không gian hiển thị Split-View, Question Palette và micro chuẩn phòng thi thật.
- **Chế độ Luyện tập theo Part:** Tối ưu hóa **Mobile-First & Responsive hoàn hảo** (Mobile 375-430px, Tablet 768-1024px, Desktop >1024px). Trên mobile, Split-View chuyển thành 2 Tab trượt (Tab Đề bài và Tab Câu hỏi).

---

# 7. YÊU CẦU PHI CHỨC NĂNG & AN TOÀN HỆ THỐNG

1. **Hiệu năng Tải trang (Performance):** Tải đề thi < 1.5 giây. Master Audio phát theo luồng Audio Streaming, không bắt tải toàn bộ 100MB mới bắt đầu thi.
2. **Khả năng Chịu lỗi & Khôi phục (Fault Tolerance):** Khôi phục 100% trạng thái câu trả lời và thời gian chuẩn Server khi F5, mất mạng hoặc tắt trình duyệt. Server chặn 1 tài khoản mở đồng thời 2 tab cùng 1 bài thi.
3. **Bảo mật Đề thi & Chống Gian lận (Exam Integrity):** Frontend không chứa `correctKey`, `transcript`, `explanation` khi đang thi. Chặn copy-paste từ ngoài vào bài Writing, chặn F12/Inspect.
4. **Bảo mật Tài khoản (Security):** Mật khẩu băm BCrypt + Salt, quản lý phiên qua `HttpOnly Secure Cookie`.

---

# 8. TIÊU CHÍ NGHIỆM THU TỔNG THỂ (ACCEPTANCE CRITERIA MATRIX)

| Mã AC | Hạng mục | Tiêu chí Nghiệm thu Cụ thể (Acceptance Criteria) |
| :--- | :--- | :--- |
| **AC-HUB-01**  | Chọn bài thi | Học viên có thể chọn làm riêng Đề L&R, Đề Speaking hoặc Đề Writing từ trang `/exams`; mỗi bài thi khởi tạo một phiên làm bài (`Attempt`) độc lập. |
| **AC-LR-01**   | Thi thử L&R | Đúng 200 câu chuẩn ETS; Master Audio phát liên tục cấm tua/dừng; đồng hồ 120 phút đồng bộ Server; tự động thu bài khi hết giờ. |
| **AC-LR-02**   | Chấm điểm L&R| Tự động đối chiếu Barem ETS 990; hiển thị điểm Listening (5-495), Reading (5-495), tổng điểm (10-990) và % đúng từng Part 1-7. |
| **AC-LR-03**   | Review L&R   | Chế độ xem lại hiển thị toàn bộ 200 câu; đáp án người học vs đáp án đúng; Interactive Transcript song ngữ; giải thích chi tiết. |
| **AC-SPEAK-01**| Thi Speaking | Kiểm tra Micro bắt buộc trước khi thi; tự động chuyển pha Chuẩn bị -> Tiếng Bíp -> Trả lời; tự động thu âm và ngắt khi hết giờ; cấm thu âm lại. |
| **AC-SPEAK-02**| Kết quả Speaking| Trả điểm quy đổi thang 200; nghe lại chính xác file âm thanh giọng nói của thí sinh; nghe audio mẫu bản xứ và transcript mẫu. |
| **AC-WRITE-01**| Thi Writing  | Khóa spellcheck và chặn dán văn bản bên ngoài; đếm từ thời gian thực; autosave định kỳ 800ms; đồng hồ 60 phút độc lập. |
| **AC-WRITE-02**| Kết quả Writing| Trả điểm quy đổi thang 200; hiển thị lại bài viết; cung cấp bài văn mẫu chuẩn ETS và nhận xét tiêu chí rubric. |
| **AC-PRAC-01** | Luyện tập    | Đầy đủ 15 Parts của 4 kỹ năng; phản hồi đúng/sai tức thì; audio lặp đoạn A-B; hiển thị giải thích chi tiết ngay dưới câu hỏi. |
| **AC-HIST-01** | Lịch sử thi  | Trang lịch sử chia thành 3 tab độc lập: Tab L&R, Tab Speaking, Tab Writing; cho phép mở lại kết quả và review mode tương ứng. |
| **AC-ADM-01**  | Quản trị User| Danh sách phân trang, tìm kiếm/lọc; Khóa tài khoản có nhập lý do và thu hồi session tức thì; mở khóa tài khoản thành công. |
| **AC-ADM-02**  | Soạn đề L&R  | Khung cố định 200 slot; cơ chế Revision Draft/Published bất biến; chặn xuất bản nếu chưa đủ 200 câu, chưa có Master Audio hoặc Barem. |
| **AC-ADM-03**  | Soạn đề S&W  | Quản lý riêng đề Speaking (11 câu) và đề Writing (8 câu); ràng buộc đủ số câu, thời gian chuẩn và bài mẫu trước khi xuất bản. |
| **AC-ADM-04**  | Import Excel | Hỗ trợ import đề L&R qua file XLSX 4 sheets + Master Audio; validate dữ liệu và highlight lỗi chi tiết nếu có sai sót. |

---

> **LỜI KẾT DÀNH CHO ĐỘI NGŨ DỰ ÁN:**  
> Tài liệu đặc tả yêu cầu phiên bản **2.1** đã chuẩn hóa chính xác nghiệp vụ: **Học viên lựa chọn làm và nhận kết quả độc lập cho từng bài thi (Listening & Reading, Speaking, Writing)**.  
> - **UI/UX Designer:** Thiết kế trang Thư viện đề thi (`/exams`) thành 3 phân khu thẻ rõ ràng, xây dựng 3 luồng phòng thi và 3 màn hình kết quả/review mode riêng biệt theo đúng chỉ dẫn tại Mục 3, 4, 6.  
> - **Lập trình viên (FE/BE):** Triển khai kiến trúc Entity, API endpoint, Attempt session và Barem điểm độc lập cho từng loại bài thi theo đúng tiêu chí nghiệm thu tại Mục 8.
