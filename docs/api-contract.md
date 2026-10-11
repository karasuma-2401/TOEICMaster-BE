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

## Đăng nhập local

`POST /api/auth/login`

Request:

```json
{
  "email": "learner@example.com",
  "password": "user-password",
  "rememberMe": true
}
```

Backend trim và chuẩn hóa email không phân biệt hoa thường; password được kiểm tra nguyên trạng. Tài khoản không tồn
tại, không có mật khẩu local, bị vô hiệu hóa hoặc sai mật khẩu đều nhận cùng response `401` để không tiết lộ trạng thái
tài khoản.

Response `200 OK`:

```json
{
  "tokenType": "Bearer",
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-10-12T00:15:00+00:00",
  "refreshToken": "<opaque-random-token>",
  "refreshTokenExpiresAtUtc": "2026-11-11T00:00:00+00:00",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "displayName": "Nguyen Van A",
    "email": "learner@example.com"
  }
}
```

- Access token sống 15 phút. JWT chứa `sub`, `email`, `name`, `jti`, `iss`, `aud`, `iat`, `nbf`, `exp`; chưa có role
  claim vì mô hình phân quyền chưa được chốt.
- Refresh session sống 1 ngày khi `rememberMe=false`, 30 ngày khi `rememberMe=true`.
- PostgreSQL chỉ lưu SHA-256 hash của refresh token. Token thô chỉ xuất hiện trong response lúc cấp phát.
- Next.js BFF nên giữ refresh token trong cookie `Secure`, `HttpOnly`, `SameSite`; không lưu refresh token vào
  `localStorage` hoặc JavaScript phía browser.

| Status | Nội dung |
| --- | --- |
| `200 OK` | Token pair và thông tin người dùng. |
| `400 Bad Request` | `ValidationProblemDetails` theo các field `Email`, `Password`. |
| `401 Unauthorized` | `ProblemDetails` với title chung `Email hoặc mật khẩu không chính xác.` |

## Làm mới và thu hồi phiên

`POST /api/auth/refresh`

```json
{ "refreshToken": "<opaque-random-token>" }
```

Response `200` có cùng cấu trúc với login. Mỗi lần refresh, token cũ bị thu hồi và thay bằng token mới trong cùng thời
hạn tuyệt đối ban đầu. Hai request dùng đồng thời cùng một refresh token thì chỉ một request thành công; request còn lại
nhận `401`.

`POST /api/auth/logout`

```json
{ "refreshToken": "<opaque-random-token>" }
```

Logout trả `204 No Content` và có tính idempotent. Refresh token không tồn tại hoặc đã bị thu hồi vẫn trả `204`.
Access token đã cấp không bị thu hồi tức thời và tự hết hạn sau tối đa 15 phút.
