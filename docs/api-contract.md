# Backend API contract

Các endpoint trong tài liệu này dùng prefix `/api`. Frontend gọi backend qua Next.js route handler và cấu hình
server-side `API_BASE_URL`; không đưa URL backend hoặc credential vào browser bundle.

## Đăng ký tài khoản local

`POST /api/auth/register`

Request:

```json
{
  "displayName": "Nguyen Van A",
  "email": "learner@example.com",
  "password": "at-least-eight-characters"
}
```

- `displayName`: bắt buộc, sau khi trim còn 1–100 ký tự.
- `email`: bắt buộc, định dạng email hợp lệ, tối đa 254 ký tự. Backend trim và dùng bản chuẩn hóa không phân biệt
  hoa thường để kiểm tra trùng.
- `password`: giữ nguyên khoảng trắng, từ 8–128 ký tự. Backend chỉ lưu BCrypt enhanced hash, không lưu plaintext.

| Status | Nội dung |
| --- | --- |
| `202 Accepted` | Đăng ký được chấp nhận, response không có body. |
| `400 Bad Request` | `ValidationProblemDetails`, lỗi theo các field `DisplayName`, `Email`, `Password`. |
| `409 Conflict` | `ProblemDetails` với title `Email này đã được sử dụng.` |

Mọi ProblemDetails có `traceId`. Unique index trên `NormalizedEmail` là lớp bảo vệ cuối cùng khi hai request đăng ký
cùng email chạy đồng thời.

Endpoint xác minh email chưa được định nghĩa. Frontend hiện chỉ mô phỏng màn hình nhập mã sau response `202`; không
được hiểu response này là email xác minh đã được gửi.
