# Backend API contract

Các endpoint trong tài liệu này dùng prefix `/api/v1`. Frontend gọi backend qua Next.js route handler và cấu hình
server-side `API_BASE_URL`; không đưa URL backend hoặc credential vào browser bundle.

## Giới hạn request xác thực

Các endpoint xác thực có fixed-window rate limit theo địa chỉ IP trong từng API instance:

| Nhóm | Endpoint | Giới hạn local |
| --- | --- | --- |
| Gửi email | `register`, `resend-verification` | 5 request/phút/IP |
| Credential | `verify-email`, `login`, `google` | 10 request/phút/IP |
| Phiên | `refresh`, `logout` | 30 request/phút/IP |

Khi vượt giới hạn, API trả `429 Too Many Requests`, header `Retry-After` và `ProblemDetails` có `traceId`. Rate limit
này bổ sung cho cooldown resend và giới hạn nhập sai OTP theo account; không thay thế hai cơ chế đó. Bản local lưu bộ
đếm trong memory của process. Khi triển khai nhiều API instance, chuyển lớp giới hạn chung sang gateway hoặc distributed
store và cấu hình trusted proxy trước khi dùng forwarded client IP.

## Đăng ký tài khoản local

`POST /api/v1/auth/register`

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
| `202 Accepted` | Tạo tài khoản chưa kích hoạt và gửi OTP, response không có body. |
| `400 Bad Request` | `ValidationProblemDetails`, lỗi theo các field `DisplayName`, `Email`, `Password`. |
| `409 Conflict` | Email thuộc tài khoản đã kích hoạt. |

Tài khoản local mới không thể đăng nhập trước khi xác minh. OTP gồm 6 chữ số, hết hạn sau 10 phút, chỉ lưu HMAC trong
PostgreSQL và cho phép tối đa 5 lần nhập sai. Đăng ký lại một email đang chờ xác minh không thay đổi tên hoặc mật khẩu
đã lưu; backend chỉ gửi mã mới nếu đã qua cooldown 60 giây. Trong cooldown vẫn trả `202` để tránh tạo cơ chế spam.

`POST /api/v1/auth/verify-email`

```json
{ "email": "learner@example.com", "code": "123456" }
```

Trả `204 No Content` khi xác minh thành công. Mã sai, hết hạn hoặc đã đủ 5 lần sai trả `400 ProblemDetails` với thông báo
chung. Sau khi nhận `204`, frontend chuyển người dùng về trang đăng nhập.

`POST /api/v1/auth/resend-verification`

```json
{ "email": "learner@example.com" }
```

Luôn trả `202 Accepted` cho request hợp lệ, kể cả email không tồn tại, đã kích hoạt hoặc còn trong cooldown. Chỉ tài
khoản chưa kích hoạt đã qua cooldown mới được cấp và gửi mã mới; việc này tránh dò email và spam SMTP.

## Đăng nhập local

`POST /api/v1/auth/login`

Request:

```json
{
  "email": "learner@example.com",
  "password": "user-password",
  "rememberMe": true
}
```

Backend trim và chuẩn hóa email không phân biệt hoa thường; password được kiểm tra nguyên trạng. Tài khoản không tồn
tại, chưa xác minh email, không có mật khẩu local, bị vô hiệu hóa hoặc sai mật khẩu đều nhận cùng response `401` để
không tiết lộ trạng thái tài khoản.

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

`POST /api/v1/auth/refresh`

```json
{ "refreshToken": "<opaque-random-token>" }
```

Response `200` có cùng cấu trúc với login. Mỗi lần refresh, token cũ bị thu hồi và thay bằng token mới trong cùng thời
hạn tuyệt đối ban đầu. Hai request dùng đồng thời cùng một refresh token thì chỉ một request thành công; request còn lại
nhận `401`.

`POST /api/v1/auth/logout`

```json
{ "refreshToken": "<opaque-random-token>" }
```

Logout trả `204 No Content` và có tính idempotent. Refresh token không tồn tại hoặc đã bị thu hồi vẫn trả `204`.
Access token đã cấp không bị thu hồi tức thời và tự hết hạn sau tối đa 15 phút.

## Đăng nhập Google

`POST /api/v1/auth/google`

Frontend dùng Google Identity Services với cùng Web Client ID đã cấu hình cho backend, nhận trường `credential` từ
Google rồi chuyển nguyên ID token đó qua Next.js BFF:

```json
{
  "credential": "<google-signed-id-token>",
  "rememberMe": true
}
```

Backend dùng thư viện Google chính thức để kiểm tra chữ ký, `aud`, `iss`, `exp` và yêu cầu email đã được xác minh. Tài
khoản Google được nhận diện bằng claim `sub`; không dùng email hoặc dữ liệu profile do browser tự gửi làm định danh.
Response `200` dùng cùng cấu trúc token/session với đăng nhập local.

Khi đăng nhập lần đầu:

- Nếu chưa có email trong hệ thống, backend tạo user không có local password và liên kết `(google, sub)`.
- Nếu email trùng tài khoản có sẵn và là Gmail hoặc Google Workspace, backend có thể liên kết tự động vì Google có
  thẩm quyền xác minh email đó.
- Nếu email Gmail/Workspace trùng một đăng ký local chưa xác minh, backend bỏ local password chưa được xác minh, xóa
  OTP cũ, kích hoạt tài khoản bằng danh tính Google và liên kết `(google, sub)`. Cách này không để người đã khởi tạo
  đăng ký pending tiếp tục dùng mật khẩu họ biết.
- Nếu email trùng nhưng là email bên thứ ba không thuộc Gmail/Workspace, backend trả `409`; người dùng cần đăng nhập
  local và thực hiện luồng liên kết tài khoản riêng trước. Luồng liên kết từ trang tài khoản chưa thuộc task này.

| Status | Nội dung |
| --- | --- |
| `200 OK` | Token pair và thông tin người dùng. |
| `400 Bad Request` | Thiếu hoặc sai giới hạn `Credential`. |
| `401 Unauthorized` | Google ID token sai, hết hạn, sai audience hoặc email chưa xác minh. |
| `409 Conflict` | Không an toàn để tự động liên kết tài khoản đang có cùng email. |

Nếu FE dùng popup/callback rồi gọi Next.js BFF thì BFF gửi JSON như trên. Nếu sau này chuyển sang redirect mode trực tiếp
từ Google, endpoint callback phải bổ sung kiểm tra CSRF double-submit token theo hướng dẫn Google; không dùng endpoint JSON
này làm callback redirect trực tiếp.
