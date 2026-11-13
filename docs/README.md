# Bản đồ Tài liệu & Kiến trúc Ngữ cảnh (Documentation & Context Map)

Tài liệu trong thư mục `docs/` được tổ chức theo mô hình **Hierarchical Context Architecture** (Phân tầng ngữ cảnh) nhằm tối ưu hóa việc đọc hiểu cho lập trình viên và các AI Coding Agents (Cursor, Claude Code, Gemini, Copilot).

---

## 1. Bản đồ cấu trúc thư mục

```text
docs/
├── README.md                      # [Tầng 0] Bản đồ tài liệu tổng quan (file này)
│
├── foundations/                   # [Tầng 1] Nền tảng kiến trúc, quy chuẩn & guardrails
│   ├── README.md
│   ├── architecture.md            # Kiến trúc Clean Architecture, CQRS, MediatR, Layering
│   └── feature-development.md     # Hướng dẫn quy trình phát triển tính năng chuẩn & testing
│
├── contracts/                     # [Tầng 2] Giao ước dữ liệu (Single Source of Truth)
│   ├── README.md
│   └── api-contract.md            # Danh sách API endpoints, Request/Response payload, HTTP status
│
├── features/                      # [Tầng 3] Đặc tả nghiệp vụ tinh gọn & Kế hoạch theo tính năng (High SNR)
│   ├── README.md
│   ├── _template/                 # Mẫu chuẩn spec.template.md & tasks.template.md
│   ├── auth-profile/spec.md       # Phân hệ Xác thực, Google OAuth & Hồ sơ cá nhân
│   ├── listening-reading/spec.md  # Phân hệ đề thi TOEIC L&R 200 câu, 103 blocks, Barem 990
│   ├── speaking/spec.md           # Phân hệ bài thi Speaking 11 câu, 2-phase timer, chấm AI
│   ├── writing/                   # Phân hệ bài thi Writing 8 câu, Anonymous session & chấm AI
│   │   ├── spec.md, tasks.md, admin-api.md, attempt-api.md, ai-evaluation.md
│   └── admin-portal/spec.md       # Phân hệ Dashboard, Score Profiles & Media Assets
│
├── ui-ux/                         # [Tham chiếu UI] Hệ thống Design Tokens & Màn hình Figma
│   ├── README.md
│   ├── design-tokens.md           # Bảng màu, typography, Category theming cho 3 bài thi
│   └── screens-inventory.md       # Bảng tra cứu 44 màn hình Figma theo route & Frame ID
│
├── specs/                         # [Master Documents] Tài liệu đặc tả gốc toàn hệ thống
│   ├── README.md
│   ├── prd-mvp.md                 # Product Requirements Document (PRD/SRS) toàn diện
│   └── figma-screens-master.md    # Đặc tả chi tiết tương tác 44 màn hình Figma
│
├── decisions/                     # [Tầng 4] Quyết định kiến trúc (Architecture Decision Records - ADR)
│   └── 001-admin-lr-exam-revisions.md
│
└── runbooks/                      # [Tầng 5] Vận hành, kịch bản kiểm thử & báo cáo
    ├── README.md
    └── writing-uat-runbook.md     # Kịch bản kiểm thử UAT và checklist bàn giao module Writing
```

---

## 2. Hướng dẫn nạp ngữ cảnh cho AI (Context Routing Guide)

Khi yêu cầu AI thực hiện một nhiệm vụ, **tuyệt đối không nạp toàn bộ thư mục `docs/`**. Hãy cung cấp ngữ cảnh chọn lọc theo đúng tầng:

| Loại công việc | Tài liệu AI cần đọc trước |
| :--- | :--- |
| **Bắt đầu tính năng mới** | `features/<feature-name>/spec.md` + `contracts/api-contract.md` + `foundations/architecture.md` |
| **Viết API hoặc sửa contract** | `contracts/api-contract.md` + `foundations/feature-development.md` |
| **Sửa lỗi kiến trúc / Refactor** | `foundations/architecture.md` + `decisions/` liên quan |
| **Kiểm thử / UAT** | `features/<feature-name>/tasks.md` + `runbooks/` liên quan |
| **Đối chiếu giao diện Figma** | `ui-ux/screens-inventory.md` + `features/<feature-name>/spec.md` |

---

## 3. Quy trình phát triển tính năng (Spec-Driven Workflow)

1. **Khởi tạo:** Sao chép `docs/features/_template/` thành `docs/features/<ten-tinh-nang>/`.
2. **Soạn thảo Spec (`spec.md`):** Trích xuất yêu cầu từ `specs/prd-mvp.md`, định nghĩa rõ **In-scope**, **Out-of-scope**, và **Acceptance Criteria**.
3. **Lập Checklist (`tasks.md`):** Liệt kê các phase (Domain -> Application -> Infrastructure -> Api -> Tests).
4. **Triển khai cùng AI:** Yêu cầu AI làm việc từng task, tick `[x]` khi hoàn tất và chạy test xác nhận trước khi chuyển task kế tiếp.
