# Backend API contract

## Quản trị đề TOEIC Listening & Reading

Nhóm endpoint `/api/v1/admin/exams`, `/api/v1/admin/exam-media` và `/api/v1/admin/score-profiles` yêu cầu policy `AccountAdministration` (role `Admin`). Phạm vi này chỉ quản trị nội dung đề. Không có endpoint làm bài, nộp bài, chấm điểm hay xem đề của học viên.

`POST /api/v1/admin/score-profiles` tạo một barem tái sử dụng. Request gồm `name` và `conversions`, trong đó mỗi conversion có `skill` (`Listening` hoặc `Reading`), `rawCorrectCount` từ 0 đến 100 và `scaledScore` từ 5 đến 495, là bội số của 5. `GET /api/v1/admin/score-profiles` trả về các profile để Admin chọn khi tạo hoặc import đề. Một profile được xem là đầy đủ khi có 101 mốc cho mỗi kỹ năng; API không tự seed barem ETS.

`POST /api/v1/admin/exams` nhận JSON:

```json
{ "code": "LR-2026-001", "title": "Practice test 1", "scoreProfileId": "<uuid>" }
```

Trả `201` với Draft revision đã có đúng 200 slot. `GET /api/v1/admin/exams?status=Draft&pageNumber=1&pageSize=20` phân trang theo trạng thái revision mới nhất. `GET /api/v1/admin/exams/{examId}/revisions/{revisionId}` trả nội dung preview đầy đủ dành cho Admin, bao gồm đáp án.

Để chỉnh Draft:

| Endpoint | Nội dung |
| --- | --- |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/questions/{questionNumber}` | JSON nội dung câu hỏi và đáp án; Part 2 chỉ nhận A–C, các Part khác A–D. Có thêm `imageRequired`; khi `true`, Publish chỉ thành công sau khi ảnh được gắn. |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/score-profile` | JSON `{ "scoreProfileId": "<uuid>" }`; chọn lại barem cho Draft. |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/blocks/{blockKey}/contexts` | JSON `{ "contexts": [...] }`, với `ordinal` liên tiếp từ 1. Context có `imageRequired`; ảnh bắt buộc phải được gắn trước Publish. |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/master-audio` | `multipart/form-data`, field `file`; `.mp3`/`.wav`, tối đa 100 MiB. |
| `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish` | Publish nếu revision đầy đủ. |
| `POST /api/v1/admin/exams/{examId}/revisions` | JSON `{ "sourceRevisionId": "<published-revision-uuid>" }`; fork published revision thành Draft mới. |

`POST /api/v1/admin/exam-media/images` nhận `multipart/form-data`, field `file`, chỉ PNG/JPEG/WebP tối đa 5 MiB. API kiểm tra signature file và trả asset UUID; client dùng UUID đó trong `ImageAssetId`, không dùng tên file hoặc URL.

### Media workspace cho đề mới

Draft mới và import chỉ có `workbook` dùng media workspace riêng. `GET revision` trả `usesMediaWorkspace`, `audioAssetId` của block và `imageRequired` của question/context. Revision cũ giữ `usesMediaWorkspace=false`, tiếp tục dùng UI/API master-audio cũ và không được migration trong phạm vi này.

1. `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents` nhận JSON `{ ownerKind, ownerKey, contentType, byteSize }`, trả `201` với `{ intentId, uploadUrl, expiresAtUtc, requiredHeaders }`. `ownerKind` là `AudioGroup`, `QuestionImage` hoặc `ContextImage`. Server chỉ cấp URL cho Draft workspace, key do server tạo, audio MP3/WAV tối đa 100 MiB và image PNG/JPEG/WebP tối đa 5 MiB.
2. Browser `PUT` bytes trực tiếp tới `uploadUrl` với `requiredHeaders`; không gửi credential storage qua FE.
3. `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents/{intentId}/complete` xác minh object tồn tại, MIME và kích thước, rồi gắn `ExamMediaAsset` vào owner; trả `201`. Intent hết hạn, đã hoàn tất hoặc Draft không còn hợp lệ trả `409`.

Publish workspace yêu cầu audio cho mọi block Listening Part 1–4 và mọi ảnh có `imageRequired=true`. Intent chưa hoàn tất được dọn sau khi hết hạn; bucket private và URL ký sẵn không được lưu vào database.

### Import một đề L&R hoàn chỉnh

`POST /api/v1/admin/exams/import` nhận `multipart/form-data` với field bắt buộc:

- `workbook`: XLSX, tối đa 10 MiB.

`masterAudio` vẫn được chấp nhận tùy chọn để giữ tương thích import legacy; import chỉ có `workbook` tạo Draft media workspace mới.

Workbook có bốn sheet bắt buộc: `Manifest`, `Blocks`, `ContextBlocks`, `Questions`. `Manifest` có `SchemaVersion=1`, `TestCode`, `Title`, `Scope=LISTENING_READING`, `ScoreProfileId`. `ContextBlocks` và `Questions` hỗ trợ cột `ImageRequired` (true/false; cột cũ không có vẫn hợp lệ). Importer đối chiếu chính xác 103 block và 200 câu theo `BlockKey` + `QuestionNumber`; không dựa vào thứ tự dòng. Lỗi workbook trả `400 ValidationProblemDetails` với key `workbook`; lỗi Master Audio trả `400 ProblemDetails`. Cả hai trường hợp đều không tạo Draft. Lỗi ghi DB sau upload thực hiện cleanup object audio tốt nhất có thể.

Thay toàn bộ placeholder nội dung, `ScoreProfileId` và `ImageAssetId` trước khi import; template cố ý không thể publish nguyên trạng.

`GET /api/v1/admin/exams/import-template` tải mẫu XLSX 200 câu, 103 block với đủ Part 1–7. Mẫu được phân phối từ API sau khi admin đăng nhập. Thay `ScoreProfileId`, các `ImageAssetId` của Part 1 và nội dung demo, rồi đính kèm Master Audio khi gọi endpoint import.

Các endpoint trong tài liệu này dùng prefix `/api/v1`. Frontend gọi backend qua Next.js route handler và cấu hình
server-side `API_BASE_URL`; không đưa URL backend hoặc credential vào browser bundle.

Contract hồ sơ người dùng dùng backend prefix `/api/v1/profile`; BFF ánh xạ nhóm này thành `/api/profile` cho browser.
Các route `/api/v1/users/me` vẫn có sẵn để tương thích với client cũ.

## Hồ sơ người dùng (User Profile)

Browser gọi các Next.js BFF route `/api/profile`, `/api/profile/password` và `/api/profile/avatar`. BFF chuyển tiếp đến backend qua prefix `/api/v1/profile`. Các route backend dưới `/api/v1/users/me` vẫn được giữ làm alias tương thích. Tất cả endpoint backend dưới đây yêu cầu `Authorization: Bearer <accessToken>`. Backend lấy user ID từ claim `sub` đã xác thực; client không gửi user ID.

| Endpoint | Input | Thành công | Lỗi chính |
| :--- | :--- | :--- | :--- |
| `GET /api/v1/profile` | Không có body | `200` JSON profile | `401` khi không có phiên hợp lệ |
| `PATCH /api/v1/profile` | JSON `{ "displayName": "Tên mới" }` | `200` JSON profile sau cập nhật | `400` khi tên trống hoặc quá 100 ký tự |
| `PUT /api/v1/profile` | JSON `{ "displayName": "Tên", "phone": "0912345678", "targetScore": 750 }` | `200` JSON profile sau cập nhật | `400` khi dữ liệu không hợp lệ |
| `PUT /api/v1/profile/avatar` | `multipart/form-data`, field `file` | `200` JSON profile sau cập nhật (hoặc `204`) | `400` khi file không hợp lệ, `413` khi vượt 2 MiB |
| `GET /api/v1/profile/avatar` | Không có body | `200` binary PNG/JPEG/WebP | `404` khi chưa có ảnh |
| `PUT /api/v1/profile/password` | JSON `{ "currentPassword": "...", "newPassword": "..." }` | `204 No Content` | `400` khi mật khẩu cũ sai hoặc dữ liệu không hợp lệ |

JSON profile:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "displayName": "Nguyen Van A",
  "email": "learner@example.com",
  "avatarUrl": "/api/v1/profile/avatar",
  "phone": "0912345678",
  "dateOfBirth": "2001-06-15",
  "targetScore": 750,
  "hasPassword": true
}
```

- `avatarUrl`, `phone`, `targetScore` là `null` nếu chưa có.
- `avatarUrl` là đường dẫn tương đối cần Bearer token.
- Ảnh đại diện giới hạn 2 MiB, định dạng `image/png`, `image/jpeg` hoặc `image/webp`.
- Đổi mật khẩu yêu cầu 8–128 ký tự và thu hồi toàn bộ refresh session cũ.

## Quản trị danh sách tài khoản (Account Administration)

Endpoint dành cho Admin xem danh sách tài khoản phân trang, yêu cầu policy `AccountAdministration` (role `Admin`).

```http
GET /api/v1/accounts?pageNumber=1&pageSize=20
Authorization: Bearer <access-token-with-Admin-role>
Accept: application/json
```

| Query | Mặc định | Điều kiện |
| :--- | :--- | :--- |
| `pageNumber` | 1 | Số nguyên từ 1 |
| `pageSize` | 20 | Số nguyên từ 1 đến 100 |

Response `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "email": "learner@example.com",
      "displayName": "Nguyen Van A",
      "isActive": true,
      "createdAtUtc": "2026-10-01T00:00:00+00:00"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

### Danh sách người dùng cho trang Admin

`GET /api/v1/admin/users`, yêu cầu policy `AccountAdministration`. Đây là endpoint trang `/admin/users` sử dụng.

| Query | Mặc định | Điều kiện |
| :--- | :--- | :--- |
| `page` | 1 | 1 đến 10000 |
| `pageSize` | 10 | 1 đến 100 |
| `q` | | Tìm trong họ tên và email, tối đa 100 ký tự |
| `role` | | `USER`, `ADMIN` |
| `status` | | `ACTIVE`, `PENDING`, `LOCKED` |
| `sort` | `createdAt:desc` | `fullName`, `createdAt` hoặc `lastLoginAt`, kèm `:asc` hoặc `:desc` |

Response `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "fullName": "Nguyen Van A",
      "email": "learner@example.com",
      "avatarUrl": null,
      "role": "USER",
      "status": "ACTIVE",
      "emailVerified": true,
      "authProvider": "PASSWORD",
      "createdAt": "2026-10-01T00:00:00+00:00",
      "lastLoginAt": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "stats": { "totalUsers": 1, "activeUsers": 1, "pendingUsers": 0, "lockedUsers": 0 }
}
```

- `status`: `ACTIVE` khi `IsActive`; `PENDING` khi chưa kích hoạt và chưa xác thực email; `LOCKED` khi chưa kích hoạt nhưng đã xác thực email.
- `authProvider`: `GOOGLE` khi tài khoản không có mật khẩu, còn lại là `PASSWORD`.
- `lastLoginAt`: thời điểm tạo phiên đăng nhập mới nhất, không tính các phiên sinh ra do refresh.
- `avatarUrl` luôn `null` vì endpoint ảnh đại diện chỉ phục vụ người đang đăng nhập.
- `stats` tính trên toàn bộ người dùng, không phụ thuộc bộ lọc.

## Giới hạn request xác thực

Các endpoint xác thực có fixed-window rate limit theo địa chỉ IP trong từng API instance:

| Nhóm | Endpoint | Giới hạn local |
| --- | --- | --- |
| Gửi email | `register`, `resend-verification`, `forgot-password` | 5 request/phút/IP |
| Credential | `verify-email`, `verify-password-reset`, `reset-password`, `login`, `google` | 10 request/phút/IP |
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

## Quên mật khẩu

Luồng đặt lại mật khẩu gồm ba bước. OTP và reset token chỉ được lưu dạng hash, dùng một lần và tách biệt hoàn toàn với
OTP kích hoạt tài khoản.

### Yêu cầu mã đặt lại mật khẩu

`POST /api/v1/auth/forgot-password`

```json
{ "email": "learner@example.com" }
```

Request hợp lệ luôn trả `202 Accepted` và không có body, kể cả email không tồn tại, tài khoản chỉ đăng nhập Google,
tài khoản chưa kích hoạt hoặc đang trong cooldown 60 giây. Chỉ tài khoản local đã kích hoạt và xác minh email mới nhận
OTP 6 chữ số. OTP hết hạn sau 10 phút và cho phép tối đa 5 lần nhập sai.

### Xác minh mã đặt lại mật khẩu

`POST /api/v1/auth/verify-password-reset`

```json
{ "email": "learner@example.com", "code": "123456" }
```

Response `200 OK`:

```json
{
  "resetToken": "<opaque-one-time-token>",
  "expiresAtUtc": "2026-10-15T12:10:00+00:00"
}
```

Response có `Cache-Control: no-store`. Xác minh thành công làm OTP không thể dùng lại; reset token sống 10 phút và chỉ
dùng được một lần. Mã sai, hết hạn, đã dùng hoặc vượt 5 lần sai trả `400 ProblemDetails` với thông báo chung.

### Đặt mật khẩu mới

`POST /api/v1/auth/reset-password`

```json
{
  "resetToken": "<opaque-one-time-token>",
  "newPassword": "new-password"
}
```

- `newPassword`: giữ nguyên khoảng trắng, từ 8–128 ký tự.
- Thành công trả `204 No Content`, dùng reset token một lần và thu hồi toàn bộ refresh session đang hoạt động của tài
  khoản. Access token đã cấp trước đó tự hết hạn theo thời gian sống tối đa 15 phút.
- Token sai, hết hạn hoặc đã dùng trả `400 ProblemDetails` với thông báo chung.
- Response có `Cache-Control: no-store`.

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

---

## Quản trị đề TOEIC Writing & Ngân hàng câu hỏi

Các endpoint `/api/v1/admin/writing/*` dùng policy `WritingAdministration` yêu cầu role `Admin`. Xem [Writing Admin API](../features/writing/admin-api.md) để biết cấu trúc dữ liệu, import Excel và ảnh.

- `/exercises`: danh sách, tạo, sửa và lưu trữ bài tập lẻ; `/exercises/imports/validate` và `/exercises/import` kiểm tra và import Excel theo Part.
- `/tests`: tạo/sửa/xóa bản nháp, publish đủ 5/2/1, lưu trữ và khôi phục đề đã công bố; `/tests/import-template`, `/tests/imports/validate` và `/tests/import` hỗ trợ import đề Excel. Xóa, lưu trữ và khôi phục yêu cầu phiên bản hiện tại.
- `/media/images`: upload ảnh Part 1; bài tham khảo `modelAnswer` là tùy chọn nội bộ và không được trả cho học viên.

## Thi thử & Luyện tập TOEIC Writing

Các endpoint `/api/v1/writing` xác định owner qua JWT hoặc header `X-Writing-Anonymous-Session`; attempt của owner khác nhận 404. Xem [Writing Attempt API](../features/writing/attempt-api.md) và [AI Evaluation](../features/writing/ai-evaluation.md).

- `GET /exercises?part=1&pageNumber=1&pageSize=20` liệt kê bài lẻ đã công bố; `POST /exercises/{id}/attempts` bắt đầu bài luyện không giới hạn thời gian.
- `GET /tests`, `GET /tests/{id}` liệt kê đề đã publish; `POST /attempts` với `{ "testId": "uuid", "mode": "FULL" }` bắt đầu đề.
- `GET /attempts`, `GET /attempts/{id}`, `PUT /attempts/{id}/answers/{position}`, `POST /attempts/{id}/next`, `POST /attempts/{id}/submit` hỗ trợ lịch sử, lưu, chuyển câu và nộp.
- `POST /attempts/{id}/evaluation-consent` chỉ gửi bài và ảnh cho AI sau khi được đồng ý; FE poll `GET /attempts/{id}`. Trạng thái gồm `CONSENT_REQUIRED`, `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, `FAILED`; gửi lại consent khi `FAILED` để thử lại.
- Guest được tạo tối đa một lượt chấm mới trong 24 giờ; retry của cùng attempt không tính quota mới. Bài mẫu không hiển thị cho học viên. Điểm AI chỉ để luyện tập.
