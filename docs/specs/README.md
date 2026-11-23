# Master Specifications

This directory contains comprehensive project-level specification documents (Master Documents).

---

## 1. Master Document Catalog

- [prd-mvp.md](./prd-mvp.md): Complete MVP Product Requirements Document (PRD / SRS) across the 4 skills (Listening & Reading, Speaking, Writing) and the Admin Portal.
- [figma-screens-master.md](./figma-screens-master.md): Detailed specification for all 44 Figma screens, Design Tokens, States, and Interactions.

---

## 2. Guidelines for AI Coding Agents

> [!IMPORTANT]
> Both documents above are very large (>130 KB).
> When implementing a specific feature, **DO NOT** load both full files into the context window.
> Use the **modularized, high-SNR** feature specifications under [features/](../features/) instead:
> - Auth & Profile: [features/auth-profile/spec.md](../features/auth-profile/spec.md)
> - Listening & Reading: [features/listening-reading/spec.md](../features/listening-reading/spec.md)
> - Speaking: [features/speaking/spec.md](../features/speaking/spec.md)
> - Writing: [features/writing/spec.md](../features/writing/spec.md)
> - Admin Portal: [features/admin-portal/spec.md](../features/admin-portal/spec.md)
