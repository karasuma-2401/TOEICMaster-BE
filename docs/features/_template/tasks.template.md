# Execution Checklist: [Tên tính năng]

> **Tài liệu đặc tả:** [spec.md](./spec.md)  
> **Trạng thái:** [Chưa bắt đầu / Đang thực hiện / Hoàn thành]

---

## Nguyên tắc thực thi cho AI
1. Hoàn thành từng task theo đúng thứ tự tầng kiến trúc: `Domain` -> `Application` -> `Infrastructure` -> `Api` -> `Tests`.
2. Sau mỗi task hoặc phase, chạy kiểm tra tương ứng (`dotnet build`, `dotnet test`).
3. Đánh dấu `[x]` vào từng mục đã hoàn thành.

---

## Phase 1: Domain & Entities
- [ ] Định nghĩa hoặc cập nhật Entity / Value Objects trong `Domain/Entities/`.
- [ ] Thiết lập business invariants, constructors và domain events (nếu có).
- [ ] Chạy `dotnet build TOEICMaster.slnx` kiểm tra build.

## Phase 2: Application (CQRS & Business Logic)
- [ ] Tạo Command/Query record trong `Application/<Feature>/Commands/` hoặc `Queries/`.
- [ ] Tạo DTOs và cấu hình AutoMapper profile.
- [ ] Viết FluentValidation validator kế thừa `AbstractValidator<T>`.
- [ ] Viết Handler xử lý Command/Query (`IRequestHandler<TRequest, TResponse>`).
- [ ] Đảm bảo truyền `CancellationToken` qua tất cả lời gọi bất đồng bộ.

## Phase 3: Infrastructure (Persistence & External Services)
- [ ] Cấu hình EF Core EntityTypeConfiguration (nếu có entity mới).
- [ ] Tạo migration bằng script nguồn (nếu có thay đổi schema).
- [ ] Triển khai repository hoặc external service adapters trong `Infrastructure/`.

## Phase 4: API Presentation
- [ ] Tạo hoặc cập nhật Controller trong `Api/Controllers/v1/`.
- [ ] Gọi MediatR qua `ISender.Send`.
- [ ] Khai báo Swagger attributes (`[ProducesResponseType]`, `[Authorize]`).
- [ ] Đảm bảo ánh xạ lỗi và trả về đúng format `ValidationProblemDetails`.

## Phase 5: Verification & Testing
- [ ] Viết Unit tests cho Handler và Validator trong `tests/TOEICMaster.Application.UnitTests/`.
- [ ] Viết Integration tests cho API endpoint trong `tests/TOEICMaster.Api.IntegrationTests/`.
- [ ] Chạy toàn bộ test suite:
  ```sh
  dotnet test TOEICMaster.slnx -c Release
  ```
- [ ] Kiểm tra format code:
  ```sh
  dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
  ```

## Phase 6: Handoff & Review
- [ ] Cập nhật [api-contract.md](../../contracts/api-contract.md) nếu có thay đổi hợp đồng API.
- [ ] Báo cáo tóm tắt thay đổi và các điểm cần lưu ý khi kết nối với Frontend.
