# Quản lý Tính năng (Feature Specifications)

Thư mục này chứa đặc tả nghiệp vụ, API contract liên quan và tiến độ triển khai của từng phân hệ tính năng trong Backend theo phương pháp **Spec-Driven Development (SDD)**.

---

## 1. Danh mục các tính năng hiện có

| Thư mục tính năng | Mô tả nghiệp vụ | Trạng thái |
| :--- | :--- | :--- |
| **[auth-profile/](./auth-profile/spec.md)** | Đăng ký, Đăng nhập, Google OAuth, Profile cá nhân, Quản trị tài khoản | Hoàn thành |
| **[listening-reading/](./listening-reading/spec.md)** | Đề thi 200 câu L&R chuẩn ETS, Import Excel, Master Audio, Barem 990 | Hoàn thành Admin |
| **[speaking/](./speaking/spec.md)** | Bài thi Speaking 11 câu, 2-phase timer, Mic check, Chấm điểm AI Gemini | Đang phát triển |
| **[writing/](./writing/spec.md)** | Bài thi Writing 8 câu, Ngân hàng bài tập, Anonymous session, Hàng đợi chấm AI | Hoàn thành |
| **[admin-portal/](./admin-portal/spec.md)** | Dashboard, Quản lý Score Profiles, Quản lý thư viện Media Assets | Hoàn thành |

---

## 2. Cấu trúc chuẩn của một thư mục tính năng

Mỗi thư mục tính năng gồm:
```text
features/<feature-name>/
├── spec.md              # Đặc tả nghiệp vụ, API contract, ràng buộc và Acceptance Criteria
└── tasks.md             # Checklist thực thi theo từng phase Clean Architecture (tùy chọn khi đang dev)
```

---

## 3. Cách khởi tạo một tính năng mới

Khi chuẩn bị code một tính năng mới:
1. Sao chép thư mục [_template/](./_template/) sang thư mục mới:
   ```sh
   cp -r docs/features/_template docs/features/<ten-tinh-nang>
   mv docs/features/<ten-tinh-nang>/spec.template.md docs/features/<ten-tinh-nang>/spec.md
   mv docs/features/<ten-tinh-nang>/tasks.template.md docs/features/<ten-tinh-nang>/tasks.md
   ```
2. Điền thông tin vào `spec.md` và `tasks.md`.
3. Cung cấp file `spec.md` cho AI Agent để bắt đầu triển khai code với tỷ lệ tín hiệu tối đa.
