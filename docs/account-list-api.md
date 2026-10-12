# API danh sách tài khoản cơ bản có phân trang

## Phạm vi đã thực hiện

Triển khai backend `GET /api/v1/accounts` theo Clean Architecture/CQRS. Không tạo màn hình frontend trong repo backend này. Đã bổ sung phân trang để frontend có thể dùng trực tiếp khi xây dựng màn hình danh sách.

API được tích hợp trên nhánh `feature/CNPMCS-18`, dựa trên `origin/Thien-User/Authentication` tại commit `22634ba`. Dùng lại entity `User`, `Role`, `UserRole`, schema `users`, `roles`, `user_roles`, migration và JWT của nhánh này. Không tạo entity/bảng `Accounts` riêng; schema tạm của lần triển khai đầu được giữ trong backup/stash, không nằm trong nhánh tích hợp.

Phạm vi đồng nhất chỉ áp dụng cho `feature/CNPMCS-18`, theo xác nhận của người thực hiện task; không sửa schema trên `dev`. Đã đối chiếu entity, EF configurations, DbContext, toàn bộ migration, các use case authentication và token service: các phần này giữ nguyên như nhánh Thien. API dùng `FullName`, `CreatedAt`, quan hệ `UserRoles` và cấu hình khóa `Jwt:Key`.

## Request và quyền truy cập

```http
GET /api/v1/accounts?pageNumber=1&pageSize=20
Authorization: Bearer <admin-access-token>
Accept: application/json
```

| Tham số | Mặc định | Điều kiện |
| --- | --- | --- |
| `pageNumber` | `1` | Số nguyên từ 1; offset `(pageNumber - 1) * pageSize` không vượt `Int32.MaxValue` |
| `pageSize` | `20` | Số nguyên từ 1 đến 100 |

Policy `AccountAdministration` yêu cầu identity đã xác thực và role claim `Admin`. Chưa xác thực trả `401`, đã xác thực nhưng thiếu role trả `403`. Dữ liệu query không hợp lệ trả `400 ValidationProblemDetails` với `errors` theo trường.

Authentication dùng JWT Bearer từ nhánh của bạn Thien. Token do `JwtTokenService` tạo chứa mảng claim `role`; `Program.cs` dùng `ClaimTypes.Role` tương ứng với claim mapping mặc định của JWT Bearer, để policy nhận đúng role `Admin`. Đăng nhập tại `POST /api/Authentication/login` bằng tài khoản đã được gán role `Admin` trong database. Migration chỉ seed các role `Admin` và `User`, không tạo user Admin. Test host có scheme giả riêng và test JWT thực do token service của nhánh này tạo; scheme/header giả không được đăng ký trong production.

## Output minh họa

Ví dụ contract `200 OK` khi database có 1 tài khoản và request được xác thực bằng tài khoản Admin. Đây là dữ liệu minh họa, không phải output thu được từ database thật:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "email": "user1@example.com",
      "fullName": "User 1",
      "roles": ["User"],
      "isActive": true,
      "createdAt": "2026-10-01T00:00:00+00:00"
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

- `items`: thông tin cơ bản gồm ID, email, họ tên, mảng `roles`, trạng thái hoạt động, ngày tạo. Bao gồm tài khoản đang hoạt động và ngừng hoạt động. Mảng role lấy từ quan hệ `user_roles`/`roles`, sắp xếp theo tên; có thể rỗng hoặc có nhiều role.
- `totalCount`: tổng số tài khoản, không chỉ số tài khoản của trang hiện tại.
- `totalPages`: làm tròn lên `totalCount / pageSize`; bằng 0 khi database rỗng.
- Trang vượt dữ liệu trả `200`, `items: []`, giữ nguyên thông tin phân trang và tổng số tài khoản.
- `hasPreviousPage`: `pageNumber > 1`; `hasNextPage`: `pageNumber < totalPages`.
- Thứ tự: `CreatedAt DESC`, sau đó `Id ASC` để cố định thứ tự khi trùng ngày tạo.
- Query chạy `CountAsync`, `Skip`/`Take` và projection tại database, dùng `AsNoTracking` và truyền cancellation token. Không tải toàn bộ bảng để phân trang trong bộ nhớ.
- Projection không lấy `PasswordHash` hoặc token; không serialize entity `User` trực tiếp.
- Count và danh sách là hai query liên tiếp; tổng số có thể thay đổi giữa hai query nếu có thao tác ghi đồng thời. Phân trang offset cũng có thể dịch chuyển giữa các request khi thêm/xóa tài khoản.

## Các thay đổi

| Layer / file | Nội dung |
| --- | --- |
| Domain `Entities/User.cs`, `Role.cs`, `UserRole.cs` | Dùng lại entity từ nhánh database/auth, không thay đổi |
| Application `Accounts/IAccountReader.cs` | Interface đọc danh sách theo trang |
| Application `Accounts/Queries/GetAccounts/` | Query, handler, DTO response và FluentValidation cho phân trang |
| Infrastructure `Persistence/Configurations/` | Dùng lại mapping `users`, `roles`, `user_roles`, không thay đổi |
| Infrastructure `Persistence/Accounts/AccountReader.cs` | Count users, sắp xếp, projection thông tin cơ bản và nhiều role, phân trang EF Core |
| Infrastructure `Persistence/ToeicDbContext.cs`, `DependencyInjection.cs` | Dùng lại DbSet Users/Roles/UserRoles, bổ sung đăng ký reader |
| Infrastructure `Persistence/Migrations/` | Dùng lại `InitialAuth`, `SeedInitialRoles` và snapshot, không tạo migration mới |
| Api `Controllers/AccountsController.cs` | Endpoint GET với response codes/OpenAPI |
| Api `Program.cs` | Policy Admin dùng JWT có sẵn; sửa role claim type phù hợp claim mapping mặc định |
| Api `TOEICMaster.Api.http` | Request mẫu danh sách và query sai |
| Tests `AccountsApiTests.cs` | Contract, phân trang, trang rỗng, giới hạn 100, offset overflow, lỗi input, 401/403 và cancellation |
| Tests `AccountsPersistenceTests.cs` | Model/migration khớp nhau, SQL phân trang users, lấy role và không lấy password hash |
| Tests `AccountsBearerApiTests.cs` | JWT do token service của nhánh Thien tạo: Admin được phép, User/Operator bị từ chối; token nhiều role vẫn được phép khi có Admin, kể cả role đầu tiên là User hoặc danh sách có role trùng |
| Tests `ApiTests.cs`, `ApplicationTests.cs`, `UnusedTestUserRepository.cs` | Cấu hình JWT giả và dependencies authentication để test hiện có vẫn khởi tạo được |
| `scripts/run-local.ps1`, `tests/LocalEnvironment.Tests.ps1` | Đọc `.env`, ánh xạ cấu hình database/Redis/MinIO/JWT, giữ ưu tiên biến môi trường, dọn credential sau khi chạy; kiểm thử PowerShell độc lập SDK |
| `.env.example`, `appsettings.json`, `docker-compose.yml` | Template JWT key, issuer/audience không nhạy cảm và ánh xạ JWT khi chạy Docker |
| README và tài liệu này | Hướng dẫn schema, contract, thay đổi và tình trạng kiểm chứng |

Không thêm package hoặc thay đổi package lock files.

## Migration và kiểm chứng

Migration được kế thừa nguyên trạng từ nhánh Thien, chưa áp dụng vào Neon hoặc database nào trong phiên này. Với máy có .NET SDK 10 phù hợp `global.json`, chạy tại thư mục repo:

```sh
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
dotnet ef migrations script --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

Sau khi kiểm tra SQL và cấu hình direct connection string của database cần cập nhật:

```sh
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

API không tự áp dụng migration và không tự tạo tài khoản mẫu. Trước khi apply, cần xác nhận database đích chưa dùng schema `Users`/`UserSessions` của nhánh authentication khác; schema của `origin/dev` và nhánh Thien hiện khác nhau, không trộn hai lịch sử migration.

`origin/dev` đã từng nhận code của nhánh Thien, sau đó revert tại `e4d507b` và triển khai module authentication/schema khác. Vì vậy merge nhánh API này vào `dev` không tự khôi phục schema Thien đã bị revert; nhóm cần thống nhất schema đích và điều chỉnh module tương ứng trước khi merge. Nhánh local hiện cố ý dùng đúng schema bạn đã xác nhận.

Tình trạng kiểm chứng trong phiên triển khai:

- Đã viết test trước implementation API, nhưng không xác nhận được bước test đỏ: lệnh test bị chặn trước khi compile do thiếu SDK.
- Đã thử `dotnet test`, `dotnet build` và `dotnet format`; tất cả bị chặn do môi trường chỉ có SDK `9.0.317`, trong khi `global.json` yêu cầu `10.0.303` hoặc phiên bản được roll-forward chấp nhận. Chưa có kết quả build/test pass.
- Migration hiện tại lấy từ nhánh Thien, không phải migration `CreateAccounts` viết thủ công trước đó; test kiểm tra snapshot và SQL đã được bổ sung nhưng chưa chạy được.
- Test HTTP dùng adapter tài khoản giả; có cả authentication scheme test và JWT Bearer thật. Chưa kiểm chứng phân trang trên PostgreSQL thật.
- Đã kiểm tra diff về whitespace bằng `git diff --check`; không có lỗi.
- Test PowerShell cho `.env` đã pass: dấu nháy, comment/export, ánh xạ database/Redis/MinIO/JWT, ưu tiên biến có sẵn, dọn biến môi trường, input sai và lỗi tiến trình.
- `scripts/run-local.ps1 -ValidateOnly` đã pass với `.env` local; không in credential và không mở kết nối database.
- Docker Engine chưa chạy; không xác nhận được runtime API bằng Docker. Máy chỉ có .NET SDK 9 nên chưa xác nhận build/test .NET pass.

## Cấu hình local và bảo toàn công việc

File `.env` local được giữ các giá trị cũ và bổ sung `JWT_KEY` ngẫu nhiên 256-bit, `JWT_ISSUER`, `JWT_AUDIENCE` để authentication của nhánh Thien khởi động được. File này vẫn bị Git và Docker build context bỏ qua. Khóa thực không có trong `.env.example` hoặc source.

Chạy từ thư mục repo:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/run-local.ps1 -ValidateOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/run-local.ps1
```

Các thay đổi trước khi tích hợp đã lưu trong Git stash có tên `Preserve account-list work before integrating Thien authentication`, đồng thời có bản sao source tại thư mục tạm `toeic-account-integration-3e897416-5cb6-4201-b49a-c2110994e66c`. Thay đổi API được quản lý trên nhánh `feature/CNPMCS-18` theo task CNPMCS-18. Nhánh này dựa trên `22634ba`, không thay đổi nhánh của bạn Thien hoặc nhánh `dev`.
