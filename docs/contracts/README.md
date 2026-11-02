# Giao ước Dữ liệu (Contracts)

Thư mục này đóng vai trò là **Single Source of Truth (SSoT)** cho tất cả các giao ước dữ liệu giữa Frontend và Backend, cũng như giữa Backend và Database.

---

## 1. Mục đích
- Đảm bảo tính nhất quán tuyệt đối về API route, payload, headers, status codes và định dạng lỗi.
- Tránh việc Frontend và Backend hiểu sai lệch về kiểu dữ liệu (Data types, Enums, Nullability).
- Cung cấp ngữ cảnh cô đọng, chuẩn xác để AI đối chiếu khi sinh code API hoặc viết Integration Test.

---

## 2. Tài liệu thuộc tầng này
- **API Contracts:** Danh sách endpoint, request/response DTOs, query parameters và validation rules.
- **Database Schema:** Thiết kế bảng, quan hệ khóa ngoại (FK), chỉ mục (Indexes) và Enums.
- **Integration Protocols:** Định dạng SSE, WebSockets hoặc gRPC (nếu có).

> [!NOTE]
> Khi có thay đổi về hợp đồng API (thêm field, đổi route, đổi validation), lập trình viên hoặc AI **bắt buộc** phải cập nhật tài liệu hợp đồng và kiểm tra tính tương thích với repo Frontend (`TOEICMaster-FE`).
