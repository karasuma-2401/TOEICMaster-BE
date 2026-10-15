# CNPMCS-18: API danh sách tài khoản có phân trang

## Nền tảng và phạm vi

Nhánh `feature/CNPMCS-18` được cập nhật theo `origin/dev` tại commit `1260874` ngày 15/10/2026. Lịch sử được giữ bằng merge, không rebase hoặc force-push; trạng thái trước khi chuyển nền được lưu ở nhánh local `backup/CNPMCS-18-before-dev-20261015`.

API sử dụng entity `User`, bảng `users`, authentication và migration của `dev`. Không thêm hoặc thay đổi entity, schema, migration hay token service. Không tạo bảng `Accounts`, `roles` hoặc `user_roles` riêng. Các tính năng đăng nhập, Google, refresh session, xác minh email, SMTP và rate limiting của `dev` được giữ nguyên.

## Request và response

```http
GET /api/v1/accounts?pageNumber=1&pageSize=20
Authorization: Bearer <access-token-with-trusted-Admin-role>
Accept: application/json
```

| Query | Mặc định | Điều kiện |
| --- | --- | --- |
| `pageNumber` | 1 | Số nguyên từ 1; offset `(pageNumber - 1) * pageSize` không vượt `Int32.MaxValue` |
| `pageSize` | 20 | Số nguyên từ 1 đến 100 |

Output dưới đây là minh họa contract, không phải dữ liệu đọc từ database thật:

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

- Dùng đúng `DisplayName` và `CreatedAtUtc` của `dev`, thay `FullName` và `CreatedAt` của bản cũ. Không trả trường `roles` vì model của `dev` chưa có role.
- Bao gồm tài khoản đang hoạt động và chưa hoạt động. Không trả password hash, normalized email, refresh token, OTP hoặc các thông tin xác thực khác.
- Sắp xếp `CreatedAtUtc DESC`, sau đó `Id ASC`. Count, projection và `Skip`/`Take` chạy tại database với `AsNoTracking` và cancellation token.
- Database rỗng trả `items: []`, `totalCount: 0`, `totalPages: 0`. Trang vượt dữ liệu vẫn trả `200` với danh sách rỗng và giữ đúng tổng số tài khoản.
- `hasPreviousPage` là `pageNumber > 1`; `hasNextPage` là `pageNumber < totalPages`.
- Count và items dùng hai query liên tiếp; thay đổi dữ liệu đồng thời có thể ảnh hưởng tổng số hoặc ranh giới trang giữa các request.

## Quyền truy cập

Policy `AccountAdministration` yêu cầu xác thực và role claim `Admin`. Thiếu hoặc sai token trả `401`; token hợp lệ nhưng thiếu Admin trả `403`. Query sai trả `400 ValidationProblemDetails` theo trường. Không lấy role từ query, request header tùy ý hoặc gán Admin mặc định cho user.

**Giới hạn hiện tại của dev:** `JwtAuthenticationTokenService` chỉ cấp identity claims, chưa có role hoặc permission. Vì vậy token từ luồng login/Google thông thường hiện nhận `403` khi gọi API danh sách. Task này giữ chặn quyền thay vì mở danh sách email cho mọi người dùng. Cần module phân quyền cấp claim Admin đáng tin cậy trước khi gọi API thành công bằng một phiên đăng nhập thật. Không thêm role vào schema hoặc thay token service trong task này.

Test Bearer gồm token thật do service của `dev` cấp (phải bị từ chối), cùng token Admin được ký bằng khóa test để kiểm tra cấu hình xác thực/policy. Token có quyền chỉ được tạo trong test host, không có endpoint production cấp hoặc tự nâng quyền Admin.

## Những thay đổi so với bản dùng nhánh Thien

| Phần | Cập nhật theo dev |
| --- | --- |
| Application DTO | `DisplayName`, `CreatedAtUtc`; bỏ `Roles` |
| EF account reader | Đọc `users`, không truy cập `UserRoles`; dùng projection chỉ lấy thông tin cơ bản |
| Authentication | Dùng `IAuthenticationTokenService`, `Jwt:SigningKey`, Google/session/email verification của dev; chỉ bổ sung policy cho API danh sách |
| Database | Giữ nguyên toàn bộ entity, mapping và migration của dev; bỏ lịch sử schema Thien khỏi cây source hiện tại |
| `.env` | Dùng `DotEnvConfiguration` có sẵn của dev, đọc trực tiếp bằng `dotnet run`; bỏ `scripts/run-local.ps1` và test PowerShell trùng chức năng |
| Tests | Chuyển test sang `Api/Accounts`, `Infrastructure/Persistence`, `TestDoubles`; dùng `Api/Common/ApiFactory` và doubles của dev |
| Dependencies | Ghim dependency đã có `System.Security.Cryptography.Pkcs` lên 10.0.13 và cập nhật 3 lock files để NuGet audit không chặn build |

Dependency `System.Security.Cryptography.Pkcs` 10.0.0 từ MimeKit trên dev bị audit chặn; bản vá 10.0.13 được xác nhận trong [Microsoft security advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx). Không tắt NuGet audit hoặc bỏ qua cảnh báo bảo mật.

## Cấu hình và kiểm chứng

Theo README và `.env.example` của dev. Các biến chính là `DATABASE_CONNECTION_STRING`, `JWT_SIGNING_KEY`, `GOOGLE_CLIENT_ID`, `EMAIL_VERIFICATION_HASH_KEY` và nhóm `SMTP_*`. File `.env` local vẫn được Git/Docker build context bỏ qua, không có credential thật trong source. `JWT_KEY` là tên cũ của nhánh Thien; thay bằng `JWT_SIGNING_KEY` khi cấu hình môi trường.

Trong `.env` local, đã bổ sung `JWT_SIGNING_KEY` từ khóa local hiện có và tạo `EMAIL_VERIFICATION_HASH_KEY` ngẫu nhiên 256-bit khi chưa có; các giá trị cũ được giữ nguyên. Chưa tự điền `GOOGLE_CLIENT_ID` hoặc credential SMTP: cần cấu hình các giá trị thật của môi trường theo README để chạy toàn bộ ứng dụng local.

```sh
dotnet tool restore
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet test TOEICMaster.slnx -c Release --no-restore
dotnet run --project src/TOEICMaster.Api
```

Kết quả kiểm chứng: restore ở locked mode và build thành công; **98/98 tests pass**, không skip; `dotnet format --verify-no-changes --no-restore` pass. Các test gồm contract, lỗi phân trang, trang rỗng, cancellation, 401/403, JWT Bearer của dev, SQL projection và snapshot khớp model, cùng suite authentication hiện có của dev.

Chưa apply migration hoặc ghi dữ liệu lên Neon; chưa kiểm chứng truy vấn phân trang bằng database thật. Test HTTP dùng adapter dữ liệu giả, test SQL dùng Npgsql để dịch query và sinh schema mà không mở kết nối.
