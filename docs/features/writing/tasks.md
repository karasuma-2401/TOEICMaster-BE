# Checklist Tiến độ Module TOEIC Writing

> **Tài liệu đặc tả:** [spec.md](./spec.md)  
> **Trạng thái:** Hoàn thành & Đang vận hành (Production)

---

## Tiến độ các tầng Clean Architecture

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
