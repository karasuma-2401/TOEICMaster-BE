# Data Contracts

This directory serves as the **Single Source of Truth (SSoT)** for all data contracts between Frontend and Backend, as well as between Backend and Database.

---

## 1. Purpose
- Ensure absolute consistency in API routes, payloads, headers, status codes, and error formats.
- Prevent mismatches between Frontend and Backend on data types (Data types, Enums, Nullability).
- Provide concise, accurate context for AI to reference when generating API code or writing Integration Tests.

---

## 2. Documents in This Layer
- **API Contracts:** List of endpoints, request/response DTOs, query parameters, and validation rules.
- **Database Schema:** Table design, foreign key (FK) relationships, indexes, and enums.
- **Integration Protocols:** Formats for SSE, WebSockets, or gRPC (if applicable).

> [!NOTE]
> Whenever there is an API contract change (adding a field, changing a route, updating validation), the developer or AI **must** update the contract documentation and verify compatibility with the Frontend repo (`TOEICMaster-FE`).
