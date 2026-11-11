# Trace: danh sách user và chấm Speaking bằng AI

Ngày: 2026-11-11. Nhánh: `feat/flowspeaking`. Các thay đổi chưa được commit.

## 1. Không tải được danh sách user

### Nguyên nhân

BE thiếu endpoint mà FE gọi.

| Bước | Kết quả |
| --- | --- |
| Trang `/admin/users` gọi `getAdminUsers` | Gọi `/api/admin/users` của Next.js |
| Next.js chuyển tiếp (`src/app/api/admin/users/route.ts`) | Gọi BE `GET /api/v1/admin/users` |
| BE trước khi sửa | Trả `404`, không có controller cho route này |
| Endpoint gần nhất BE có | `GET /api/v1/accounts`, nhưng khác tham số và khác cấu trúc dữ liệu |

`/api/v1/accounts` không dùng thay được vì thiếu `role`, `status`, `emailVerified`, `authProvider`, `lastLoginAt`, `stats`, và không hỗ trợ tìm kiếm, lọc, sắp xếp. FE kiểm tra chặt cấu trúc phản hồi nên sẽ báo dữ liệu không hợp lệ.

### Đã sửa

Thêm `GET /api/v1/admin/users`, yêu cầu quyền Admin. Không có migration, không thêm package, không sửa FE.

File mới:

- `src/TOEICMaster.Api/Controllers/AdminUsersController.cs`
- `src/TOEICMaster.Application/Accounts/IAdminUserReader.cs`
- `src/TOEICMaster.Application/Accounts/Queries/GetAdminUsers/GetAdminUsersQuery.cs`
- `src/TOEICMaster.Application/Accounts/Queries/GetAdminUsers/GetAdminUsersQueryValidator.cs`
- `src/TOEICMaster.Infrastructure/Persistence/Accounts/AdminUserReader.cs`
- `tests/TOEICMaster.Tests/Api/Accounts/AdminUsersApiTests.cs`
- `tests/TOEICMaster.Tests/Infrastructure/Persistence/AdminUserReaderTests.cs`
- `tests/TOEICMaster.Tests/TestDoubles/EmptyAdminUserReader.cs`

File sửa:

- `src/TOEICMaster.Infrastructure/DependencyInjection.cs`: thêm 1 dòng đăng ký.
- `tests/TOEICMaster.Tests/Application/Common/MediatorPipelineTests.cs`: thêm 1 dòng đăng ký.
- `docs/contracts/api-contract.md`, `docs/features/auth-profile/spec.md`: mô tả endpoint.

### Cách suy ra dữ liệu

DB chưa có cột riêng cho các giá trị này, nên endpoint suy ra từ dữ liệu sẵn có:

| Trường | Cách tính |
| --- | --- |
| `status = ACTIVE` | `IsActive = true` |
| `status = PENDING` | `IsActive = false` và chưa xác thực email |
| `status = LOCKED` | `IsActive = false` và đã xác thực email |
| `authProvider` | Không có mật khẩu là `GOOGLE`, còn lại `PASSWORD` |
| `lastLoginAt` | Phiên mới nhất trong `user_sessions`, bỏ qua phiên sinh ra do refresh |
| `avatarUrl` | Luôn `null`, vì endpoint ảnh chỉ phục vụ người đang đăng nhập. FE hiển thị chữ viết tắt |

### Kiểm chứng

- `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, `dotnet build -c Release`: đạt, 0 warning.
- `dotnet test -c Release`: 382/382 pass.
- Chạy reader trên một PostgreSQL 17 tạm trong Docker với dữ liệu mẫu: tìm kiếm (kể cả ký tự `%`), lọc, sắp xếp, thống kê, phân trang, `lastLoginAt` đều đúng. Container và test tạm đã xóa.
- API đang chạy: `GET /api/v1/admin/users` không token trả `401` (trước khi sửa là `404`), route có trong OpenAPI.
- Chưa kiểm chứng: đăng nhập bằng tài khoản Admin thật và xem trang trên trình duyệt.

### Còn thiếu

FE còn gọi các endpoint sau, BE vẫn chưa có. Các nút tương ứng sẽ lỗi `404`:

- `POST /api/v1/admin/users` (tạo user)
- `GET`, `PATCH`, `DELETE /api/v1/admin/users/{id}`
- `POST`, `DELETE /api/v1/admin/users/{id}/lock`
- `POST /api/v1/admin/users/{id}/reset-password`

Domain `User` hiện chưa có khái niệm khóa tài khoản, nên `lock` cần quyết định thiết kế trước khi làm.

## 2. Chấm Speaking bằng AI chưa chạy

### Nguyên nhân

Thiếu cấu hình môi trường, không phải lỗi code. `GET /api/v1/speaking/evaluation-availability` trả `{"available":false}`.

Điều kiện `SpeakingEvaluationSettings.IsConfigured` cần đủ 4 giá trị:

| Điều kiện | Hiện trạng |
| --- | --- |
| `AzureSpeech:Enabled = true` | Thiếu, `.env` không có biến Azure nào |
| `AzureSpeech:Key` | Thiếu |
| `Gemini:Enabled = true` | Đạt |
| `Gemini:ApiKey` | Đạt, key gọi thử model `gemini-2.5-flash-lite` trả `200` |

Hệ quả: FE thấy `available = false` nên không cho chấm, và endpoint `evaluation-consent` báo "Dịch vụ chấm AI Speaking chưa được cấu hình".

Nguyên nhân thứ hai sẽ lộ ra ngay sau khi có key: máy chưa cài `ffmpeg`. Worker cần `ffmpeg` để đổi bản ghi sang WAV, nếu thiếu thì bài chấm chuyển `FAILED` với mã `AUDIO_CONVERTER_UNAVAILABLE`.

Lưu ý về tên biến: khi chạy `dotnet run`, phải dùng `AzureSpeech__Enabled`, `AzureSpeech__Key`, `AzureSpeech__Region`. Tên `AZURE_SPEECH_*` chỉ có tác dụng qua `docker-compose.override.yml`, vì `DotEnvConfiguration` không ánh xạ các tên đó.

### Cần làm để bật

1. Tạo Azure Speech resource (gói F0 miễn phí đủ để thử, region `southeastasia`), lấy Key.
2. Thêm vào `.env`:

   ```sh
   AzureSpeech__Enabled=true
   AzureSpeech__Key=<AZURE_SPEECH_KEY>
   AzureSpeech__Region=southeastasia
   ```

3. Cài `ffmpeg`: `brew install ffmpeg`.
4. Khởi động lại API, kiểm tra `evaluation-availability` trả `{"available":true}`.

Luồng Azure chưa từng được gọi thử bằng key thật (`notespeaking.md`), nên cần chấm thử một bài sau khi bật.

## 3. Ghi nhận thêm

- `/health/ready` vẫn trả `503` do SMTP: chứng chỉ TLS của `smtp.gmail.com` bị từ chối trên máy này. Đăng ký và quên mật khẩu không gửi được mail.
- Log API có lỗi `Bucket "writing-images" is not found`. Volume MinIO local chỉ có `speaking-media` và `speaking-recordings`, nên ảnh của bài Writing lưu trong DB dùng chung không có trên máy này. Lỗi này có từ trước khi đổi image MinIO.
- MinIO local đang chạy ở cổng 9100 bằng image `minio/minio:latest`, vì image Chainguard trong compose báo `exec format error` trên máy này.
