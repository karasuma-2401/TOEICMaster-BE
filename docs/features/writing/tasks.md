# Checklist Tiến độ Module TOEIC Writing

> **Tài liệu đặc tả:** [spec.md](./spec.md)  
> **Trạng thái:** Production Ready (Đang nâng cấp: Tạo đề thi độc lập & Import Excel)

---

## 1. Tiến độ nền tảng cốt lõi (Đã hoàn thành)

### Phase 1: Domain & Entities
- [x] Tạo các thực thể `WritingExercise`, `WritingTest`, `WritingAttempt`, `WritingEvaluation`.
- [x] Ràng buộc tính hợp lệ của câu trả lời theo từng Part (Part 1, 2, 3).

### Phase 2: Application (CQRS & MediatR)
- [x] Các Commands: `CreateWritingExerciseCommand`, `SubmitWritingAttemptCommand`, `GrantAiConsentCommand`.
- [x] Các Queries: `GetWritingTestsQuery`, `GetWritingAttemptQuery`, `GetWritingEvaluationQuery`.
- [x] Tích hợp FluentValidation cho payload câu hỏi và câu trả lời.

### Phase 3: Infrastructure (Persistence & AI Background Worker)
- [x] Cấu hình EF Core Mapping cho bảng writing và migrations.
- [x] Triển khai `GeminiClient` với prompt rubric TOEIC ETS.
- [x] Triển khai Background Queue Worker xử lý job đánh giá bất đồng bộ.

### Phase 4: API Presentation
- [x] Cung cấp các endpoint `/api/v1/admin/writing/*` với policy `AccountAdministration`.
- [x] Cung cấp các endpoint `/api/v1/writing/*` hỗ trợ JWT Bearer và `X-Writing-Anonymous-Session`.

### Phase 5: Verification & UAT
- [x] Chạy kiểm thử tự động xUnit (Pass 100%).
- [x] Hoàn thành checklist UAT theo [writing-uat-runbook.md](../../runbooks/writing-uat-runbook.md).

---

## 2. Hạng mục nâng cấp: Tạo Đề thi Độc lập & Import Excel

### Phase 1: Domain Refactoring
- [ ] Tách biệt quan hệ giữa `WritingTest` và `WritingExercise` (câu hỏi trong đề thi gắn trực tiếp với test, không phụ thuộc ngân hàng câu hỏi lẻ).
- [ ] Thiết lập Invariant kiểm tra tính hợp lệ của đề thi độc lập (đủ 8 câu: 5 Part 1, 2 Part 2, 1 Part 3 trước khi publish).

### Phase 2: Application (Commands & Queries)
- [ ] Cập nhật `CreateWritingTestCommand` / `UpdateWritingTestCommand` hỗ trợ nhận trực tiếp 8 câu hỏi inline.
- [ ] Xây dựng `ImportWritingTestExcelCommand` tiếp nhận file `.xlsx` và tạo Draft test kèm 8 câu hỏi.
- [ ] Xây dựng `ValidateWritingTestExcelCommand` trả về preview lỗi/dòng hợp lệ.
- [ ] Thêm query `GetWritingTestImportTemplateQuery` phân phối file Excel mẫu chuẩn 8 câu.

### Phase 3: Infrastructure (Excel Parsing & Persistence)
- [ ] Triển khai `XlsxWritingTestImportParser` đọc 4 sheet: `Manifest`, `Part1`, `Part2`, `Part3`.
- [ ] Kiểm tra tính hợp lệ của `imageAssetId` đã upload trước khi ghi DB.
- [ ] Đảm bảo cơ chế All-or-Nothing transaction khi import đề.

### Phase 4: API Presentation
- [ ] Endpoint `POST /api/v1/admin/writing/tests/import`.
- [ ] Endpoint `POST /api/v1/admin/writing/tests/imports/validate`.
- [ ] Endpoint `GET /api/v1/admin/writing/tests/import-template`.
- [ ] Đồng bộ hóa BFF proxy tại `toeicmaster-fe/src/features/writing/server/admin-writing-proxy.ts`.

### Phase 5: Frontend UI & Verification
- [ ] Cập nhật UI modal Tạo đề Writing trên Frontend hỗ trợ nhập 8 câu hỏi trực tiếp và nút bấm Import Excel.
- [ ] Viết Unit Test cho Excel Parser đề thi và Integration Test cho endpoint import.
- [ ] Kiểm chứng qua bộ lệnh chuẩn: `dotnet format`, `dotnet build`, `dotnet test`.
