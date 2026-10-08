# TOEICMaster Backend

API của phần mềm hỗ trợ học và luyện thi TOEIC, tổ chức theo Clean Architecture và CQRS trong một ứng dụng.

**Công nghệ:** ASP.NET Core .NET 10, EF Core 10, Neon PostgreSQL, Redis, MinIO và Mailpit. MediatR 14.2.0, FluentValidation 12.1.1 và AutoMapper 16.2.0 được cấu hình sẵn.

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

    Mở `.env`, thay `DATABASE_CONNECTION_STRING` bằng connection string Neon của nhóm, giữ dấu ngoặc kép:

    ```dotenv
    DATABASE_CONNECTION_STRING="Host=<NEON_HOST>;Database=<DATABASE>;Username=<USER>;Password=<PASSWORD>;SSL Mode=VerifyFull;Channel Binding=Require;"
    ```

    Đây là định dạng Npgsql, không phải URL `postgresql://...`. File `.env` được Git bỏ qua.

3. Khởi động Redis, MinIO và Mailpit:

    ```sh
    docker compose up -d redis minio mailpit
    ```

4. Lưu cùng connection string Neon vào .NET user secrets:

    ```sh
    dotnet user-secrets set "ConnectionStrings:Postgres" "<NEON_CONNECTION_STRING>" --project src/TOEICMaster.Api
    ```

    Thay `<NEON_CONNECTION_STRING>` bằng toàn bộ chuỗi nhận từ nhóm. User secrets lưu ngoài repo trên máy từng thành viên. Docker Compose đọc `.env`; `dotnet run` đọc user secrets.

5. Khôi phục dependency và chạy API:

    ```sh
    dotnet tool restore
    dotnet restore TOEICMaster.slnx --locked-mode
    dotnet run --project src/TOEICMaster.Api
    ```

6. Kiểm tra trên trình duyệt:

    - `http://localhost:5287/api/v1/system/ping`: API trả JSON.
    - `http://localhost:5287/health/ready`: trả `Healthy` khi Neon, Redis, MinIO và SMTP kết nối được.
    - `http://localhost:5287/openapi/v1.json`: đặc tả API trong môi trường Development.

Giữ credential Redis/MinIO mặc định trong `.env.example` khi setup lần đầu. Nếu đổi chúng, cập nhật cấu hình tương ứng bằng user secrets để API chạy trên host dùng cùng credential với Docker.

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
| API           | http://localhost:5287 | Backend                   |
| Redis         | localhost:6379        | Cache                     |
| MinIO API     | http://localhost:9000 | Object storage            |
| MinIO console | http://localhost:9001 | Xem object storage        |
| Mailpit UI    | http://localhost:8025 | Xem email thử nghiệm      |
| Mailpit SMTP  | localhost:1025        | Nhận email từ ứng dụng    |
| PostgreSQL    | Neon                  | Database bên ngoài Docker |

Mailpit nhận email để kiểm thử, không chuyển email tới hộp thư Internet.

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
  TOEICMaster.Tests/           Architecture, Application, API, mapping và SMTP tests
.github/                      CI, publish image và template Issue
docs/                         Kiến trúc
```

[Chi tiết kiến trúc](docs/architecture.md). Authentication, nghiệp vụ TOEIC và schema dữ liệu sẽ được bổ sung theo backlog.

Controller gọi `ISender.Send` của MediatR; Application xử lý use case và chỉ tham chiếu Domain. Infrastructure triển khai interface Application để truy cập dữ liệu, cache, storage và email. Domain độc lập với framework/database.

CQRS là cách tách command (ghi/thực hiện hành động) và query (đọc); hiện vẫn dùng một database Neon. Mẫu có sẵn:

- **Query:** `System/Queries/GetSystemStatus` được gọi từ endpoint ping, giữ nguyên contract với FE.
- **Command:** `Email/Commands/SendEmail` có handler gọi `IMailSender` và validator kiểm tra dữ liệu trước khi gửi. Chưa có HTTP endpoint gửi mail.
- **Validation:** `Common/Behaviors/ValidationBehavior` gọi `ValidateAsync` trước handler; API chuyển lỗi thành `400 ValidationProblemDetails` với `errors` theo property và `traceId`.
- **Mapping:** `Api/Mapping/SystemProfile` map DTO của Application sang HTTP response. Khi thêm mapping Domain → DTO, đặt profile trong Application; DI scan cả hai assembly.

## License MediatR và AutoMapper

Bản hiện tại dùng cơ chế license của Lucky Penny Software. Theo [FAQ chính thức](https://luckypennysoftware.com/faq), môi trường development, CI/CD, staging và kiểm thử không cần license key; đồ án môn học đủ điều kiện đăng ký Community License miễn phí. Teammate có thể setup và chạy local mà chưa có key. Khi triển khai cho người dùng, đăng ký key qua [MediatR](https://mediatr.io/) hoặc [AutoMapper](https://automapper.io/) và kiểm tra điều kiện sử dụng khi phạm vi dự án thay đổi.

Nếu đã có key, lưu ngoài repo bằng user secrets:

```sh
dotnet user-secrets set "MediatR:LicenseKey" "<MEDIATR_LICENSE_KEY>" --project src/TOEICMaster.Api
dotnet user-secrets set "AutoMapper:LicenseKey" "<AUTOMAPPER_LICENSE_KEY>" --project src/TOEICMaster.Api
```

Với Docker, đặt `MEDIATR_LICENSE_KEY` và `AUTOMAPPER_LICENSE_KEY` trong file `.env` local; Compose truyền các biến vào API. Bundle key có thể dùng cho cả hai biến nếu license bao gồm cả hai sản phẩm. Thư viện có thể ghi cảnh báo thiếu key; cấu hình không tắt cảnh báo này. Không commit key vào repo.

## Kiểm tra code

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

Tests không cần credential Neon thật. Khi thay đổi package, chạy `dotnet restore TOEICMaster.slnx` để cập nhật lock files và commit cùng thay đổi.

## EF Core migrations

Hiện chưa có entity hoặc migration. Khi thêm entity và EF configuration:

```sh
dotnet ef migrations add InitialCreate --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api --output-dir Persistence/Migrations
```

Trước khi cập nhật schema, dùng direct connection string của branch Neon cần cập nhật, thay cho hostname có `-pooler`. Cấu hình nó bằng biến môi trường `ConnectionStrings__Postgres` trong shell tạm, rồi chạy:

```sh
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

Đóng shell sau khi chạy. Commit migration cùng entity/configuration. API không tự apply migration lúc khởi động.

## Xử lý lỗi thường gặp

- **Docker không kết nối được:** mở Docker Desktop, chờ Engine sẵn sàng và kiểm tra `docker info`.
- **Thiếu Postgres configuration:** kiểm tra đã đặt user secret; nếu chạy container, kiểm tra `.env`.
- **Readiness trả 503:** kiểm tra dịch vụ bằng `docker compose ps`, log API và kết nối Neon.
- **Cổng bị chiếm:** dừng ứng dụng đang dùng cổng trước khi chạy Compose.
- **Lỗi restore SDK:** cài .NET SDK 10 phù hợp với `global.json`.
- **Cảnh báo thiếu license MediatR/AutoMapper:** local và CI không cần key; xem mục license khi chuẩn bị triển khai cho người dùng.

## CI và làm việc nhóm

Workflow được điều chỉnh từ [UGTM](https://github.com/karasuma-2401/UGTM/tree/772b2157457bd9d4aabc316c770dfbd90ee79549), chạy CI khi PR/push vào `dev` hoặc `main`. Publish image lên GHCR chỉ chạy sau CI thành công của push từ chính repo.

Feature/fix branch mở PR vào `dev`; release qua PR từ `dev` vào `main`. Khi tạo repo GitHub, push workflow vào default branch `main` và cho phép Actions publish package bằng `GITHUB_TOKEN`. Deploy Azure sẽ cấu hình khi VPS sẵn sàng.

GitHub Actions được ghim commit SHA và dùng Node 24 cho các JavaScript actions. NuGet cache tính cả lock files; restore dùng `--locked-mode`, audit dependency gồm cả package gián tiếp. Trước khi mở API nghiệp vụ cho người dùng, bổ sung authentication, phân quyền, schema và kiểm thử theo các story tương ứng.

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
