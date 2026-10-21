# Báo cáo triển khai API Profile

Ngày thực hiện: 18/10/2026. Nhánh: `dev`.

## Phạm vi đã thực hiện

- Thêm API xem profile của tài khoản đang đăng nhập.
- Thêm API cập nhật họ tên hiển thị.
- Thêm API tải lên và đọc ảnh đại diện PNG/JPEG, lưu trong bucket MinIO riêng `profile-avatars`.
- Thêm API đổi mật khẩu bằng mật khẩu hiện tại. Sau khi đổi thành công, toàn bộ refresh session của tài khoản bị thu hồi.
- Thêm cột `users.AvatarObjectKey` (nullable, tối đa 128 ký tự) và migration EF Core `20261017172845_AddProfileAvatar`.
- Bổ sung test ứng dụng và HTTP, cập nhật fixture MediatR.

Các endpoint trong ảnh frontend `/api/v1/admin/users...` là chức năng quản trị tài khoản, không phải profile cá nhân; chúng không thuộc phạm vi thay đổi này.

## Contract cho frontend

Browser gọi các Next.js BFF route `/api/profile`, `/api/profile/password` và `/api/profile/avatar`. BFF chuyển tiếp đến
backend qua prefix `/api/v1/profile`. Các route backend dưới `/api/v1/users/me` vẫn được giữ làm alias tương thích.
Tất cả endpoint backend dưới đây yêu cầu `Authorization: Bearer <accessToken>`. Backend lấy user ID từ claim `sub` đã
xác thực; client không gửi user ID.

| Endpoint | Input | Thành công | Lỗi chính |
| --- | --- | --- | --- |
| `GET /api/v1/profile` | Không có body | `200` JSON profile | `401` khi không có phiên hợp lệ |
| `PATCH /api/v1/profile` | JSON `{ displayName }` | `200` JSON profile sau cập nhật; các trường khác giữ nguyên | `400` khi dữ liệu không hợp lệ |
| `PUT /api/v1/profile/avatar` | `multipart/form-data`, field `file` | `200` JSON profile sau cập nhật | `400` khi file không hợp lệ, `413` khi request vượt giới hạn |
| `GET /api/v1/profile/avatar` | Không có body | `200` binary PNG/JPEG/WebP | `404` khi chưa có ảnh |
| `PUT /api/v1/profile/password` | JSON `{ currentPassword, newPassword }` | `204` | `400` khi mật khẩu cũ sai hoặc dữ liệu không hợp lệ |

Alias `/api/v1/users/me` còn hỗ trợ `PUT` cập nhật `{ displayName, phone, dateOfBirth, targetScore }`,
`POST /password` có `confirmPassword` và `DELETE /avatar` cho client cũ.

JSON profile:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "displayName": "Nguyen Van A",
  "email": "learner@example.com",
  "avatarUrl": "/api/v1/users/me/avatar",
  "phone": "+84912345678",
  "dateOfBirth": "2001-06-15",
  "targetScore": 850,
  "avatarVersion": "00000000000000000000000000000000",
  "hasPassword": true
}
```

`avatarUrl` là `null` nếu chưa có ảnh. Đây là đường dẫn tương đối đến endpoint cần Bearer token; frontend cần tải ảnh bằng request có header Authorization rồi hiển thị blob/object URL. Không đưa đường dẫn này trực tiếp vào thẻ `<img>` nếu thẻ không gửi Bearer token.

`phone` nhận số Việt Nam bắt đầu bằng `0` hoặc `+84` và chín chữ số theo sau. `dateOfBirth` từ `1900-01-01` đến
ngày hiện tại. `targetScore` từ 10 đến 990 và là bội số của 5. Các trường này nhận `null`.

Ảnh đại diện giới hạn 2 MiB, chỉ nhận MIME `image/png`, `image/jpeg` hoặc `image/webp` khớp dấu hiệu file. Bucket MinIO được tạo tự động lúc upload đầu tiên và không cấu hình quyền public. Ảnh cũ được xóa sau khi bản ghi profile cập nhật thành công; lỗi dọn ảnh cũ không làm thất bại request đã thành công.

Đổi mật khẩu yêu cầu mật khẩu mới dài 8–128 ký tự và khác mật khẩu hiện tại. Tài khoản đăng nhập Google chỉ đổi được mật khẩu khi đã có mật khẩu local. Refresh token cũ không dùng được sau thay đổi; access token đã phát hành vẫn có hiệu lực đến khi hết hạn (cấu hình hiện tại là 15 phút). Frontend nên yêu cầu đăng nhập lại sau khi nhận `204`.
Endpoint đổi mật khẩu sử dụng rate limit `Credentials` có sẵn của backend; khi vượt giới hạn trả `429`.

## Triển khai kỹ thuật

- `ProfileController` chỉ chuyển request vào MediatR, dùng `[Authorize]` cho toàn bộ route.
- Application chứa query/command, validator và interface `IProfileRepository`/`IAvatarStore`.
- Infrastructure dùng EF Core `ExecuteUpdateAsync` cho tên/khóa ảnh. Đổi mật khẩu dùng conditional update theo password hash cũ, thu hồi session và commit trong một transaction.
- MinIO lưu object với khóa ngẫu nhiên dưới `avatars/<user-id>/`; PostgreSQL chỉ lưu object key. Response ảnh dùng `Cache-Control: private, no-store` và `X-Content-Type-Options: nosniff`.
- Response profile không trả password hash, normalized email, refresh token hoặc object key nội bộ.

## Kiểm chứng

- `dotnet test TOEICMaster.slnx --no-restore`: **125 pass, 0 fail, 0 skip**.
- `dotnet format TOEICMaster.slnx --verify-no-changes --no-restore`: **pass**.
- Test mới kiểm tra profile theo `sub`, yêu cầu Bearer token, upload PNG hợp lệ, từ chối nội dung giả ảnh, xác thực mật khẩu hiện tại và thu hồi session theo use case.
- Migration đã sinh và review: chỉ thêm một cột nullable, không thay đổi dữ liệu cũ.

Chưa áp dụng migration lên Neon và chưa chạy kiểm thử tích hợp với Neon/MinIO thật. Các bước triển khai còn lại:

1. Trên Neon branch thử nghiệm, dùng connection string **direct** (hostname không có `-pooler`) trong `DATABASE_CONNECTION_STRING`.
2. Chạy từ thư mục solution: `dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api`.
3. Chạy API, đăng nhập, thử GET/PATCH profile, upload/GET avatar, đổi mật khẩu và xác nhận refresh token cũ bị từ chối.
4. Sau khi xác nhận trên branch thử nghiệm, áp dụng migration vào môi trường đích theo quy trình deploy của nhóm.
