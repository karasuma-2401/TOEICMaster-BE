# Feature Specification: Cổng Quản trị Viên (Admin Portal)

> **Trạng thái:** Đã triển khai / Production Ready  
> **Ánh xạ Figma:** Màn hình 27, 28, 29, 30  
> **Giao ước API:** [contracts/api-contract.md](../../contracts/api-contract.md)

---

## 1. Mục tiêu & Phạm vi (Goal & Scope)

Cung cấp trung tâm điều hành và quản trị tài nguyên cho hệ thống TOEICMaster, dành riêng cho tài khoản có quyền `Admin`.

### Các phân hệ chính:
1. **Bảng điều khiển Tổng quan (Dashboard):** Thống kê số lượng người dùng, lượt thi, tỷ lệ hoàn thành bài thi và trạng thái hệ thống.
2. **Quản lý Thang điểm Barem (Score Profiles):**
   - Tạo và lưu trữ các bộ bảng điểm chuẩn ETS có thể tái sử dụng.
   - Bắt buộc đủ 101 mốc Listening và 101 mốc Reading (từ 0 đến 100 câu đúng, điểm quy đổi từ 5 đến 495, là bội số của 5).
3. **Thư viện Quản lý Media (Media Asset Management):**
   - Upload các file ảnh cho câu hỏi và bài đọc: chỉ chấp nhận định dạng `.png`, `.jpg`, `.jpeg`, `.webp`, tối đa 5 MiB.
   - Kiểm tra chữ ký file (magic bytes), tuyệt đối không dựa vào extension hoặc MIME header do client gửi.
   - Sinh `assetId` (UUID) ngẫu nhiên để đề thi tham chiếu; không cho phép client tự đặt URL hoặc path.
4. **Quản trị Tài khoản & Phân quyền:**
   - Xem danh sách người dùng có phân trang và quản lý trạng thái tài khoản.

---

## 2. Ràng buộc Kỹ thuật & Bảo mật (Guardrails)

- Tất cả các endpoint thuộc nhóm `/api/v1/admin/*` đều bắt buộc yêu cầu Authorization header có trusted JWT claim role `Admin` thông qua policy `AccountAdministration`.
- Không nhận role từ request header tùy tiện hoặc query params.
