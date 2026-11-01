# Tài liệu Đặc tả Gốc (Master Specifications)

Thư mục này chứa các tài liệu đặc tả toàn diện cấp dự án (Master Documents).

---

## 1. Danh mục tài liệu gốc:
- [prd-mvp.md](./prd-mvp.md): Toàn bộ Product Requirements Document (PRD / SRS) phiên bản MVP cho 4 kỹ năng Listening & Reading, Speaking, Writing và Admin Portal.
- [figma-screens-master.md](./figma-screens-master.md): Đặc tả chi tiết toàn bộ 44 màn hình Figma, Design Tokens, States và Interactions.

---

## 2. Hướng dẫn dành cho AI Coding Agents:
> [!IMPORTANT]
> Hai tài liệu trên có dung lượng rất lớn (>130 KB). Khi lập trình một tính năng cụ thể, **KHÔNG NÊN** nạp toàn bộ hai file này vào context window.
> Hãy sử dụng các bản đặc tả tính năng đã được **mô-đun hóa và tinh gọn (high SNR)** tại thư mục [features/](../features/):
> - Xác thực & Hồ sơ: [features/auth-profile/spec.md](../features/auth-profile/spec.md)
> - Listening & Reading: [features/listening-reading/spec.md](../features/listening-reading/spec.md)
> - Speaking: [features/speaking/spec.md](../features/speaking/spec.md)
> - Writing: [features/writing/spec.md](../features/writing/spec.md)
> - Quản trị Admin: [features/admin-portal/spec.md](../features/admin-portal/spec.md)
