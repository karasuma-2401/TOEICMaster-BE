# Writing UAT và checklist bàn giao

Tài liệu này chốt phạm vi **Writing**: 5 câu Part 1, 2 câu Part 2 và 1 câu Part 3. Speaking không thuộc checklist.

## 1. Chuẩn bị an toàn

1. Tạo `.env` từ `.env.example`; không copy key vào source, screenshot hoặc ticket.
2. Có PostgreSQL/Redis/MinIO hoạt động và chạy migrations sau khi nhóm duyệt:

   ```powershell
   dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
   ```

3. Đặt `GEMINI_ENABLED=true` cùng `GEMINI_API_KEY`, model và timeout trong `.env`. Không cần, và không được, đưa key
   vào `.env.local` của frontend.
4. Chạy API và frontend theo README của từng repository. Chỉ dùng đề/bài làm thử không chứa thông tin cá nhân khi UAT
   Gemini Free Tier.
5. Seed một account có role `ADMIN` theo mục **Seed tài khoản Admin local** trong README, rồi đăng xuất/đăng nhập
   lại để JWT và session nhận role mới. Không thay bằng header, query string hoặc bypass ở client.

## 2. Checklist User

| Luồng | Kết quả cần có |
|---|---|
| Thư viện | Chỉ có đề `Published`; search, loading, empty và lỗi có nút thử lại. |
| Bắt đầu | Chọn Full có 8 câu theo 5/2/1; chọn Part chỉ có đúng Part đó; màn trước khi bắt đầu hiển thị hai thời lượng. |
| Timer | Part 1 đi từ 30 giây chuẩn bị sang 60 giây làm bài; reload/đổi tab không làm tăng thời gian. |
| Autosave | Gõ, chờ trạng thái đã lưu; ẩn tab/reload ngay sau khi gõ rồi mở lại, câu trả lời server mới nhất vẫn còn. |
| Khóa câu | Next hoặc hết giờ khóa câu; không thể sửa/quay lại. Câu cuối tự nộp một lần. |
| Consent | Nộp bài không tự gửi đến AI; chỉ gửi sau checkbox đồng ý. |
| AI | Queued/processing/retry poll mỗi 5 giây; có thể rời trang rồi quay lại. Lỗi key/quota không làm mất attempt và có nút chấm lại. |
| Kết quả | Có nhãn điểm AI ước tính 0–200, không phải ETS; feedback/rubric và bài mẫu chỉ xuất hiện sau `COMPLETED`. |
| Ownership | Guest chỉ thấy attempt từ cookie HttpOnly hiện tại; account chỉ thấy attempt của chính account. |

## 3. Checklist Admin/API

| Luồng | Kết quả cần có |
|---|---|
| Question bank | CRUD Part 1/2/3, validation field errors, ảnh có MIME/magic-byte/size validation. |
| Import | Excel preview báo đúng dòng/cột lỗi trước khi commit. |
| Test | Draft không cho User bắt đầu; publish chỉ qua khi đủ 5/2/1 và snapshot không đổi attempt cũ. |
| Review AI | `GET /api/v1/admin/writing/evaluations/{attemptId}` yêu cầu policy Admin; không trả provider response hoặc API key. |
| Security | API attempt/history có `Cache-Control: no-store`; responses có `X-Content-Type-Options`, `X-Frame-Options` và `Referrer-Policy`. |

## 4. Quyết định go/no-go

Chỉ bật Gemini cho demo khi tất cả dòng User bắt buộc pass, migrations đã áp dụng vào đúng database và một bài calibration
thử không có PII trả JSON/điểm hợp lệ. Free Tier có quota và không phải scorer ETS; điểm phải luôn giữ nhãn **ước tính
bởi AI**.

Các giới hạn đã chấp nhận:

- Browser không phân biệt tin cậy clipboard nội bộ/ngoài, nên không chặn paste có thể làm User mất bài.
- Autosave lúc page lifecycle là best-effort; API/server timer mới là nguồn chân lý.
- Admin override kèm audit log và việc tự cấp role cho tài khoản Admin là backlog platform, không bypass trong Writing.
