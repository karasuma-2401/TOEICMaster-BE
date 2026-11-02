# Nền tảng Kiến trúc & Quy chuẩn (Foundations)

Thư mục này chứa các tài liệu định hình toàn bộ cấu trúc kỹ thuật, quy tắc bất biến và tiêu chuẩn code của dự án Backend.

---

## 1. Mục đích
- Thiết lập **Guardrails** kỹ thuật vững chắc để AI và các thành viên trong nhóm không vi phạm nguyên tắc kiến trúc.
- Cung cấp **Golden Samples** (mẫu chuẩn) để AI học theo (In-context Learning) khi sinh mã nguồn mới.
- Định nghĩa rõ các **Anti-patterns** (điều cấm kỵ) trong codebase.

---

## 2. Tài liệu thuộc tầng này
- **Kiến trúc hệ thống (`architecture.md`):** Phân chia 4 tầng Clean Architecture (`Domain`, `Application`, `Infrastructure`, `Api`), luồng xử lý MediatR, quy tắc dependency giữa các project.
- **Quy chuẩn code (`coding-conventions.md`):** Cấu trúc file, quy ước đặt tên C#, xử lý ngoại lệ, logging, validation và mẫu chuẩn Command/Query.
- **Quy trình phát triển tính năng (`feature-development.md`):** Hướng dẫn từng bước phát triển feature từ thiết kế đến viết xUnit test.

> [!TIP]
> Bất kỳ khi nào AI đưa ra giải pháp vi phạm kiến trúc (ví dụ: inject DbContext trực tiếp vào Controller hoặc gọi I/O trong Domain), tài liệu tầng này là cơ sở để nhắc nhở và căn chỉnh lại hành vi của AI.
