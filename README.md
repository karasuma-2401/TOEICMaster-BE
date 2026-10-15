# TOEICMaster Backend

API của phần mềm hỗ trợ học và luyện thi TOEIC, tổ chức theo Clean Architecture và CQRS trong một ứng dụng.

**Công nghệ:** ASP.NET Core .NET 10, EF Core 10, Neon PostgreSQL, Redis, MinIO và SMTP. MediatR 14.2.0, FluentValidation 12.1.1 và AutoMapper 16.2.0 được cấu hình sẵn.

## Chuẩn bị

- Git.
- .NET SDK 10, tối thiểu 10.0.303.
- Docker Desktop đang chạy Linux containers hoặc Docker Engine có Compose v2.
- Connection string Neon do nhóm cung cấp riêng cho thành viên.

Kiểm tra môi trường:

```sh
git --version
dotnet --version
docker info
docker compose version
```

## Setup trên máy cá nhân

1. Clone repo backend và mở terminal tại thư mục repo:

    ```sh
    git clone <BACKEND_REPO_URL> toeicmaster-be
    cd toeicmaster-be
    ```

    Thay `<BACKEND_REPO_URL>` bằng URL GitHub của nhóm.

2. Tạo file cấu hình:

    Windows PowerShell:

    ```powershell
    Copy-Item .env.example .env
    ```

    macOS/Linux:

    ```sh
    cp .env.example .env
    ```

    Mở `.env`, thay `DATABASE_CONNECTION_STRING` bằng connection string Neon của nhóm, giữ dấu ngoặc kép. Đồng thời
    thay `JWT_SIGNING_KEY` bằng chuỗi ngẫu nhiên riêng của môi trường, tối thiểu 32 ký tự. Điền thêm thông tin SMTP
    của nhà cung cấp email; địa chỉ gửi phải được nhà cung cấp xác minh:

    ```dotenv
    DATABASE_CONNECTION_STRING="Host=<NEON_HOST>;Database=<DATABASE>;Username=<USER>;Password=<PASSWORD>;SSL Mode=VerifyFull;Channel Binding=Require;"
    JWT_SIGNING_KEY=<RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS>
    GOOGLE_CLIENT_ID=<GOOGLE_WEB_CLIENT_ID>.apps.googleusercontent.com
    EMAIL_VERIFICATION_HASH_KEY=<ANOTHER_RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS>
    SMTP_HOST=<EXTERNAL_SMTP_HOST>
    SMTP_PORT=587
    SMTP_SECURITY=StartTls
    SMTP_FROM_ADDRESS=<VERIFIED_SENDER_ADDRESS>
    SMTP_FROM_NAME=TOEICMaster
    SMTP_USERNAME=<SMTP_USERNAME>
    SMTP_PASSWORD=<SMTP_PASSWORD_OR_APP_PASSWORD>
    ```

    Đây là định dạng Npgsql, không phải URL `postgresql://...`. File `.env` được Git bỏ qua.

3. Khởi động Redis và MinIO:

    ```sh
    docker compose up -d redis minio
    ```

4. Khôi phục dependency và chạy API:

    ```sh
    dotnet tool restore
    dotnet restore TOEICMaster.slnx --locked-mode
    dotnet run --project src/TOEICMaster.Api
    ```

5. Kiểm tra trên trình duyệt:

    - `https://localhost:7287/scalar`: Scalar API Reference trong môi trường Development.
    - `https://localhost:7287/api/v1/system/ping`: API trả JSON.
    - `https://localhost:7287/health/ready`: trả `Healthy` khi Neon, Redis, MinIO và SMTP kết nối được.
    - `https://localhost:7287/openapi/v1.json`: đặc tả OpenAPI mà Scalar sử dụng.

Profile `https` trong `launchSettings.json` mở Scalar tự động. Nếu máy chưa tin cậy HTTPS development certificate,
chạy `dotnet dev-certs https --trust` một lần rồi khởi động lại API.

API, `dotnet ef` và Package Manager Console tự tìm file `.env` từ thư mục project/solution và ánh xạ các biến sang cấu
hình .NET. Environment variables thật của môi trường deploy vẫn có độ ưu tiên cao hơn `.env`. Giữ credential
Redis/MinIO mặc định khi setup lần đầu; không commit `.env`, mật khẩu SMTP hoặc app password. Tài khoản SMTP phải cho
phép gửi từ `SMTP_FROM_ADDRESS`.

## Chạy toàn bộ backend bằng Docker

Sau khi điền `.env`, có thể chạy API và dịch vụ phụ bằng một lệnh; cách này không cần cài .NET SDK:

```sh
docker compose up -d --build
```

API vẫn ở `http://localhost:5287`.

Nếu frontend cũng chạy Docker, tạo shared network một lần và dùng override ở cả hai repo:

```sh
docker network create toeicmaster-local
docker compose -f docker-compose.yml -f docker-compose.shared.yml up -d --build
```

Nếu network đã tồn tại, bỏ qua lệnh tạo. Frontend sẽ gọi API qua `http://backend:8080`.

## Dịch vụ local

| Dịch vụ       | Địa chỉ               | Mục đích                  |
| ------------- | --------------------- | ------------------------- |
| API (HTTPS)   | https://localhost:7287 | Backend và Scalar docs    |
| API (HTTP)    | http://localhost:5287  | Tương thích frontend local |
| Redis         | localhost:6379        | Cache                     |
| MinIO API     | http://localhost:9000 | Object storage            |
| MinIO console | http://localhost:9001 | Xem object storage        |
| PostgreSQL    | Neon                  | Database bên ngoài Docker |

Backend kết nối trực tiếp đến nhà cung cấp SMTP bên ngoài đã cấu hình. Vì vậy email do ứng dụng gửi sẽ được chuyển tới
hộp thư thật; không còn dịch vụ mail giả chạy trong Docker Compose.

Thông tin cần lấy từ nhà cung cấp mail:

- SMTP hostname và port; cấu hình mặc định của dự án là port `587` với `StartTls`.
- Username và password SMTP (một số nhà cung cấp gọi password này là SMTP key hoặc app password).
- Địa chỉ sender đã được xác minh; giá trị này phải khớp `SMTP_FROM_ADDRESS`.
- Nếu nhà cung cấp yêu cầu TLS trực tiếp trên port 465, đặt `SMTP_SECURITY=SslOnConnect` và `SMTP_PORT=465`.

Sau khi điền credential, gọi endpoint đăng ký bằng một email thật. API trả `202`; email nhận được phải chứa OTP 6 số.
Không đưa SMTP key vào source, log, screenshot hoặc file `.env.example`.

MinIO đang chạy bằng image Docker của Chainguard được ghim digest, cùng nhà cung cấp image với repo UGTM. Phần mềm server bên trong là MinIO Community; [upstream đã ngừng bảo trì](https://github.com/minio/minio). Cấu hình này phục vụ local. Trước khi triển khai production, nhóm cần chọn phương án storage có kế hoạch cập nhật và hỗ trợ phù hợp.

Xem trạng thái, log và dừng dịch vụ:

```sh
docker compose ps
docker compose logs -f backend
docker compose down
```

Khi API chạy bằng `dotnet run`, log API nằm trong terminal đó. Lệnh `down` giữ dữ liệu trong Docker volumes.

## Cấu trúc và phát triển

```text
src/
  TOEICMaster.Domain/          Entity và quy tắc nghiệp vụ
  TOEICMaster.Application/     Command/query, handler, validator, DTO và interface
  TOEICMaster.Infrastructure/  EF Core, cache, storage và SMTP
  TOEICMaster.Api/             HTTP endpoints, mapping response, xử lý lỗi và DI
tests/
  TOEICMaster.Tests/
    Architecture/              Kiểm tra dependency rule giữa các layer
    Application/               Unit test cho use case và MediatR pipeline
    Infrastructure/            Test adapter SMTP, security và configuration
    Api/                       Integration test cho HTTP contract và middleware
    TestDoubles/               Fake dùng chung, không chứa test case
.github/                      CI, publish image và template Issue
docs/                         Kiến trúc
```

[Chi tiết kiến trúc](docs/architecture.md) và [API contract](docs/api-contract.md). API đăng ký, đăng nhập local/Google,
refresh và logout đã có; nghiệp vụ TOEIC được bổ sung theo backlog.

Các endpoint auth có rate limit theo IP: gửi email 5 request/phút, kiểm tra credential 10 request/phút và thao tác
session 30 request/phút. Request vượt giới hạn nhận `429` kèm `Retry-After`; xem chi tiết trong API contract.

Controller gọi `ISender.Send` của MediatR; Application xử lý use case và chỉ tham chiếu Domain. Infrastructure triển khai interface Application để truy cập dữ liệu, cache, storage và email. Domain độc lập với framework/database.

CQRS là cách tách command (ghi/thực hiện hành động) và query (đọc); hiện vẫn dùng một database Neon. Mẫu có sẵn:

- **Query:** `System/Queries/GetSystemStatus` được gọi từ endpoint ping, giữ nguyên contract với FE.
- **Command:** `Email/Commands/SendEmail` có handler gọi `IMailSender` và validator kiểm tra dữ liệu trước khi gửi. Chưa có HTTP endpoint gửi mail.
- **Validation:** `Common/Behaviors/ValidationBehavior` gọi `ValidateAsync` trước handler; API chuyển lỗi thành `400 ValidationProblemDetails` với `errors` theo property và `traceId`.
- **Mapping:** `Api/Mapping/SystemProfile` map DTO của Application sang HTTP response. Khi thêm mapping Domain → DTO, đặt profile trong Application; DI scan cả hai assembly.

## License MediatR và AutoMapper

Bản hiện tại dùng cơ chế license của Lucky Penny Software. Theo [FAQ chính thức](https://luckypennysoftware.com/faq), môi trường development, CI/CD, staging và kiểm thử không cần license key; đồ án môn học đủ điều kiện đăng ký Community License miễn phí. Teammate có thể setup và chạy local mà chưa có key. Khi triển khai cho người dùng, đăng ký key qua [MediatR](https://mediatr.io/) hoặc [AutoMapper](https://automapper.io/) và kiểm tra điều kiện sử dụng khi phạm vi dự án thay đổi.

Nếu đã có key, đặt `MEDIATR_LICENSE_KEY` và `AUTOMAPPER_LICENSE_KEY` trong file `.env` local. Bundle key có thể dùng
cho cả hai biến nếu license bao gồm cả hai sản phẩm. Thư viện có thể ghi cảnh báo thiếu key; cấu hình không tắt cảnh
báo này. Không commit key vào repo.

## Kiểm tra code

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

Tests không cần credential Neon thật. Khi thay đổi package, chạy `dotnet restore TOEICMaster.slnx` để cập nhật lock files và commit cùng thay đổi.

## EF Core migrations

Migration `InitialUsers` tạo bảng tài khoản; `AddUserSessions` tạo bảng phiên và refresh token hash;
`AddExternalIdentities` lưu định danh từ Google; `AddEmailVerification` thêm trạng thái xác minh và challenge OTP;
`AddPasswordReset` thêm challenge OTP và reset token dùng một lần cho luồng quên mật khẩu.
Khi thay đổi schema tiếp theo:

```sh
dotnet ef migrations add <MigrationName> --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api --output-dir Persistence/Migrations
```

Trước khi cập nhật schema, đặt direct connection string của đúng branch Neon vào `DATABASE_CONNECTION_STRING` trong
`.env`; hostname migration không nên chứa `-pooler`. Sau đó chạy từ solution root:

```sh
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

Trong Package Manager Console, chọn `TOEICMaster.Infrastructure` làm Default project, giữ `TOEICMaster.Api` làm
Startup project rồi chạy `Update-Database`; startup cũng đọc cùng file `.env`. Commit migration cùng
entity/configuration. API không tự apply migration lúc khởi động.

## Xử lý lỗi thường gặp

- **Docker không kết nối được:** mở Docker Desktop, chờ Engine sẵn sàng và kiểm tra `docker info`.
- **Thiếu/sai Postgres configuration:** kiểm tra `DATABASE_CONNECTION_STRING` trong `.env` và chạy lệnh từ bên trong
  cây thư mục solution để startup tìm thấy file.
- **Readiness trả 503:** kiểm tra dịch vụ bằng `docker compose ps`, log API và kết nối Neon.
- **Cổng bị chiếm:** dừng ứng dụng đang dùng cổng trước khi chạy Compose.
- **Lỗi restore SDK:** cài .NET SDK 10 phù hợp với `global.json`.
- **Cảnh báo thiếu license MediatR/AutoMapper:** local và CI không cần key; xem mục license khi chuẩn bị triển khai cho người dùng.

## CI và làm việc nhóm

Workflow được điều chỉnh từ [UGTM](https://github.com/karasuma-2401/UGTM/tree/772b2157457bd9d4aabc316c770dfbd90ee79549), chạy CI khi PR/push vào `dev` hoặc `main`. Publish image lên GHCR chỉ chạy sau CI thành công của push từ chính repo.

Feature/fix branch mở PR vào `dev`; release qua PR từ `dev` vào `main`. Khi tạo repo GitHub, push workflow vào default branch `main` và cho phép Actions publish package bằng `GITHUB_TOKEN`. Deploy Azure sẽ cấu hình khi VPS sẵn sàng.

GitHub Actions được ghim commit SHA và dùng Node 24 cho các JavaScript actions. NuGet cache tính cả lock files; restore dùng `--locked-mode`, audit dependency gồm cả package gián tiếp. Trước khi mở API nghiệp vụ cho người dùng, hoàn thiện đăng nhập, phân quyền và kiểm thử theo các story tương ứng.

<details>
<summary>Giấy phép cấu hình tham khảo UGTM (MIT)</summary>

```text
MIT License

Copyright (c) 2026 Win Transparent Pear

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

</details>
