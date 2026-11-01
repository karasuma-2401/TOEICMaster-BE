# Feature Specification: Xác thực, Hồ sơ & Quản trị Tài khoản (Auth & Profile)

> **Trạng thái:** Đã triển khai / Production Ready  
> **Ánh xạ Figma:** Màn hình 01 (`/login`), Màn hình 02 (`/register`), Màn hình 03 (`/forgot-password`), Màn hình 05 (`/profile`), Màn hình 28 (`/admin/users`)  
> **Giao ước API:** [contracts/api-contract.md](../../contracts/api-contract.md)

---

## 1. Mục tiêu & Phạm vi (Goal & Scope)

### Mục tiêu chính
Cung cấp giải pháp định danh an toàn cho hệ thống TOEICMaster bao gồm đăng ký tài khoản qua Email + OTP, đăng nhập Email/Password, đăng nhập một chạm với Google OAuth, quản lý hồ sơ cá nhân và quản trị viên xem danh sách tài khoản.

### Phạm vi chức năng (In-scope)
1. **Xác thực người dùng (`/api/v1/auth`):**
   - Đăng ký: Nhận email, họ tên, mật khẩu -> gửi mã xác thực OTP qua SMTP (Mailpit/MailKit).
   - Đăng nhập: Cấp cặp `accessToken` (15 phút) và `refreshToken` (1-30 ngày).
   - Google Login: Nhận Google Identity ID token -> xác minh chữ ký -> tự động liên kết tài khoản Gmail/Workspace.
   - Quên mật khẩu & Đổi mật khẩu: Reset qua token gửi email.
2. **Hồ sơ cá nhân (`/api/v1/profile`):**
   - Xem thông tin tài khoản hiện tại (`displayName`, `avatarUrl`, `phone`, `dateOfBirth`, `targetScore`).
   - Cập nhật thông tin và tải ảnh đại diện lên MinIO bucket `profile-avatars`.
   - Đổi mật khẩu -> tự động thu hồi toàn bộ refresh token cũ.
3. **Quản trị tài khoản (`/api/v1/accounts`):**
   - Admin xem danh sách tài khoản phân trang (`pageNumber`, `pageSize`).

---

## 2. Ràng buộc An toàn & Bảo mật (Security Guardrails)

1. **Lưu trữ Token & Mật khẩu:**
   - PostgreSQL chỉ lưu chuỗi băm SHA-256 của `refreshToken`, không lưu token thô.
   - Mật khẩu mã hóa một chiều qua thuật toán bảo mật cao.
2. **Rate Limiting nghiêm ngặt:**
   - Gửi email: 5 request/phút/IP.
   - Xác thực/Login/Google: 10 request/phút/IP.
   - Phiên refresh/logout: 30 request/phút/IP.
3. **Upload Avatar:**
   - Giới hạn 2 MiB, bắt buộc kiểm tra chữ ký file thực tế (magic bytes) hỗ trợ `image/png`, `image/jpeg`, `image/webp`.

---

## 3. Tiêu chí nghiệm thu (Acceptance Criteria)

```gherkin
Scenario: Đăng ký và kích hoạt tài khoản bằng OTP
  Given người dùng gửi email hợp lệ chưa tồn tại trên hệ thống
  When hoàn tất form đăng ký
  Then hệ thống gửi mã OTP 6 số vào hộp thư email
  And tài khoản ở trạng thái chưa kích hoạt cho đến khi xác minh OTP thành công

Scenario: Đổi mật khẩu thành công
  Given người dùng đã đăng nhập và gửi mật khẩu cũ chính xác kèm mật khẩu mới
  When gọi PUT /api/v1/profile/password
  Then hệ thống cập nhật mật khẩu mới
  And thu hồi tất cả refresh session đang hoạt động của người dùng
```
