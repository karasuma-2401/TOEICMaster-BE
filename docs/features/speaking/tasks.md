# Execution Checklist: TOEIC Speaking Module

> **Tài liệu đặc tả:** [spec.md](./spec.md)  
> **Trạng thái:** Đang thực hiện. Luồng chính đã xong; các Work Package A, B, C và D chưa bắt đầu.

## Thứ tự làm

Làm theo các đợt dưới đây, không theo thứ tự chữ cái. Lý do nằm ở [báo cáo mục 0](../../runbooks/speaking-audit-report.md#0-kết-quả-kiểm-tra-lại-2026-11-03).

| Đợt | Việc | Thuộc |
| :--- | :--- | :--- |
| 1 | UI-02, UI-03, UI-04, UI-07; MIC-06, MIC-11; DUP-04; luật 3 câu hỏi khác nhau (DUP-01) | C, B, A |
| 2 | Phiên micro: B "Ưu tiên 1", thêm nút bật lại micro, đo ma trận trình duyệt | B |
| 3 | Chặn trùng ở server, sửa bộ mẫu test, rà dữ liệu. **Không** làm `publish-check` | A |
| 4 | B "Ưu tiên 2" phần còn lại | B |
| 5 | Phần còn lại của C, liên kết lịch sử lượt làm (FLOW-02) | C |
| Song song | Chuyển `ffmpeg` và biến môi trường vào file dùng chung (OPS-01) | Phase 6 |
| Để sau | `publish-check`, cảnh báo gần giống, `usedInTestCount`, D | A, C, D |

---

## Nguyên tắc thực thi cho AI
1. Chỉ sửa code Speaking: BE là các file `Speaking*`; FE là `src/features/speaking` và `src/app/speaking`. Điểm chạm file dùng chung phải tối thiểu.
2. Không sửa migration đã áp dụng. Không chạy `database update` lên Neon nếu chưa được đồng ý. Không thêm package nếu chưa được yêu cầu.
3. Hoàn thành theo thứ tự `Domain` → `Application` → `Infrastructure` → `Api` → `Tests`, rồi tới FE. Sau mỗi phase, chạy kiểm tra và đánh dấu `[x]`.
4. Kiểm tra BE: bốn lệnh trong `AGENTS.md`. Kiểm tra FE: `npm run check`.
5. Trước khi làm Work Package A, B hoặc D, xác nhận các quyết định D1–D5 trong [báo cáo rà soát](../../runbooks/speaking-audit-report.md#7-quyết-định-chờ-chủ-tính-năng) đã được duyệt.

---

## Đã hoàn thành

### Phase 1: Domain & Entities
- [x] `SpeakingExercise`, `SpeakingTest`, `SpeakingTestExercise`, `SpeakingMediaAsset`.
- [x] `SpeakingAttempt` với máy trạng thái theo pha; `SpeakingEvaluation`.
- [x] `SpeakingTiming` cố định theo format ETS.

### Phase 2: Application (CQRS & Business Logic)
- [x] Command và query cho bài tập, đề, lượt làm, bản ghi, chấm AI.
- [x] Điều kiện công bố báo từng lỗi; snapshot khi công bố.
- [x] Công thức điểm trong `SpeakingEvaluationScoring`.

### Phase 3: Infrastructure (Persistence & External Services)
- [x] Mapping EF Core và các migration Speaking.
- [x] `MinioSpeakingMediaStore`; worker xóa bản ghi của khách.
- [x] Azure Speech, Gemini, chuyển đổi audio bằng `ffmpeg`, worker chấm.

### Phase 4: API Presentation
- [x] `AdminSpeakingController`, `SpeakingAttemptsController`, giới hạn tải bản ghi.
- [x] Luyện từng bài và `evaluation-availability`.

### Phase 5: Verification & Testing
- [x] Test Domain, Application, API, nhà cung cấp AI (xem `tests/TOEICMaster.Tests`).
- [ ] Gọi thử Azure Speech và Gemini bằng key thật. `notespeaking.md` ghi chưa kiểm tra được; cần xác nhận lại.
- [ ] Hoàn thành [UAT runbook](../../runbooks/speaking-uat-runbook.md).

### Phase 6: Handoff & Review
- [ ] Thêm mục Speaking vào `contracts/api-contract.md` (file dùng chung, cần thống nhất với nhóm).
- [ ] Chuyển `ffmpeg` và biến Azure/Gemini từ `docker-compose.override.yml` (không nằm trong git) vào `Dockerfile`, `docker-compose.yml`, `.env.example` (báo cáo OPS-01; file dùng chung, cần thống nhất với nhóm).

---

## Work Package A: Không trùng nội dung ([ADR-002](../../decisions/002-speaking-test-content-uniqueness.md))

**Domain:** không đổi.

**Application**
- [ ] Tạo `Speaking/Admin/SpeakingTestContentRules.cs`: chuẩn hóa văn bản, tạo đơn vị so sánh, trả danh sách vấn đề kèm mã, mức, vị trí câu.
- [ ] `SpeakingExerciseInputValidator`: 3 câu hỏi Part 3–4 phải khác nhau sau chuẩn hóa.
- [ ] `PublishRequirements`: thêm khóa `Content` cho lỗi mức chặn. Lấy `sha256` của ảnh bằng `FindMediaAssetAsync`.
- [ ] (Để sau) Thêm query `CheckSpeakingTestPublishQuery`, truyền `CancellationToken` đầy đủ.

**Infrastructure:** không cần migration ở phase này.

**Api**
- [ ] (Để sau) `POST /api/v1/admin/speaking/tests/publish-check` trong `AdminSpeakingController`, gọi qua `ISender.Send`.

**Tests**
- [ ] Sửa bộ mẫu: `SpeakingSamples` có đoạn văn thứ hai và ảnh khác byte; sửa `FullTestAsync`, `Admin_and_learner_contract_matches_the_frontend`, `Published_images_are_served_only_through_a_published_snapshot`. Lưu ý `DetectImage` kiểm tra đuôi PNG, nên đổi byte ở giữa file thay vì nối thêm vào cuối.
- [ ] Chuẩn hóa: hoa thường, dấu câu, khoảng trắng, Unicode.
- [ ] Trùng trong một bài; giữa 2 bài Part 1; giữa Part 3 và Part 4; cùng asset; khác asset cùng `sha256`.
- [ ] Gần giống ở ngưỡng 0,79 và 0,8 chỉ khác nhau ở cảnh báo; cảnh báo không chặn công bố.
- [ ] `publish-check` không ghi dữ liệu và chấp nhận đề chưa đủ bài.
- [ ] Đề đã công bố và lượt làm cũ không đổi.

**Trước khi bật:** chạy truy vấn chỉ đọc ở [mục 5 của UAT](../../runbooks/speaking-uat-runbook.md#5-rà-soát-dữ-liệu-hiện-có).

---

## Work Package B: Phiên micro ([ADR-003](../../decisions/003-speaking-microphone-session.md))

Chỉ sửa FE.

**Ưu tiên 1: sửa lỗi xin quyền**
- [ ] Tạo `src/app/speaking/layout.tsx` và `src/features/speaking/mic/mic-session-provider.tsx`.
- [ ] Chuyển `getUserMedia`, `AudioContext` và phần đo âm lượng vào provider. Đảm bảo `acquire` idempotent, kể cả với StrictMode.
- [ ] `useVoiceRecorder` chỉ dùng stream của provider.
- [ ] `SpeakingAttemptRun`: bỏ effect xin quyền khi mount. Không đụng micro khi `SUBMITTED`. `release()` khi nộp xong.
- [ ] Giải phóng stream khi rời luồng làm bài.
- [ ] `SpeakingSession`: chỉ `POST /attempts` khi micro sẵn sàng.
- [ ] Khôi phục sau tải lại theo kết quả `permissions.query`, bọc `try/catch`.
- [ ] Màn làm bài có nút bật lại micro khi trạng thái là `idle`, kèm thông báo đồng hồ vẫn chạy.
- [ ] Trên trình duyệt thật: xác nhận layout giữ stream khi chuyển route, và bản ghi của câu cuối vẫn đủ dữ liệu.

**Ưu tiên 2: đúng PRD**
- [ ] Tách lỗi: `denied`, `no-device`, `busy`, `insecure`, `unsupported`, kèm hướng dẫn riêng.
- [ ] Kiểm tra thiết bị 3 bước: cấp quyền, nói thử 5 giây, nghe lại. Thêm chọn micro.
- [ ] Chính sách theo chế độ (D1).
- [ ] Tiếng beep bằng `OscillatorNode`; ghi âm bắt đầu sau beep.
- [ ] Phát hiện `track.ended`, `recorder.onerror`; cảnh báo `beforeunload` trong pha trả lời.
- [ ] Sửa nội dung ở `speaking-test-overview.tsx:113` về việc lưu bản ghi.
- [ ] Ẩn nút bỏ qua pha ở đề đầy đủ (D2).

**Tests**
- [ ] Mock `getUserMedia`: đúng 1 lần gọi qua kiểm tra, tạo lượt làm và chuyển route.
- [ ] Bài `SUBMITTED`: 0 lần gọi.
- [ ] Không có `POST /attempts` khi chưa có quyền ở chế độ `FULL`.
- [ ] Mỗi loại lỗi cho đúng trạng thái.
- [ ] Rời luồng thì track bị dừng.
- [ ] Đo thực tế theo [ma trận trình duyệt](../../runbooks/speaking-uat-runbook.md#3-ma-trận-quyền-micro).

---

## Work Package C: Test Builder ([thiết kế](./admin-test-builder.md))

**FE, không cần đổi BE (làm trước)**
- [ ] UI-02: giữ đúng vị trí khi lưu và khi tải lại.
- [ ] UI-01, DUP-04: bộ chọn có tìm kiếm phía server; khóa bài đã dùng và ghi rõ đang dùng ở câu nào.
- [ ] UI-03, UI-07: hiện đủ lỗi, đúng tiêu đề.
- [ ] UI-05, UI-06: hỏi khi đóng; lưu nháp không đóng builder.
- [ ] UI-10, UI-11, UI-13.
- [ ] FLOW-02: gọi `GET /attempts` và thêm danh sách lượt làm có liên kết đến `/speaking/attempts/{id}`. Trang `/history` không thuộc Speaking, nên đặt ở trang Speaking trừ khi nhóm thống nhất khác.

**Cần BE**
- [ ] UI-08: banner lỗi và sơ đồ 11 câu theo `publish-check` (cần Work Package A).
- [ ] UI-04: thêm `code` vào ProblemDetails của `409`.
- [ ] UI-09: `POST /tests/{id}/clone`, `DELETE /tests/{id}`.
- [ ] UI-12, DUP-05: thêm `usedInTestCount`.

**Tests:** các scenario trong [admin-test-builder.md](./admin-test-builder.md#5-tiêu-chí-nghiệm-thu).

---

## Work Package D: Làm sau, cần quyết định

- [ ] Gỡ đề đã công bố: cần migration mới cho `ck_speaking_tests_status` (D4).
- [ ] Audio câu hỏi Part 3–4 (D5).
- [ ] Cột `content_fingerprint` để cảnh báo trùng toàn ngân hàng.

---

## Handoff

- [ ] Cập nhật `admin-api.md`, `attempt-api.md`: chuyển phần đề xuất sang phần đã triển khai.
- [ ] Đổi trạng thái ADR-002 và ADR-003 khi được duyệt hoặc triển khai.
- [ ] Đánh dấu các dòng đã sửa trong báo cáo rà soát.
- [ ] Báo các file dùng chung cần cập nhật (báo cáo mục 8).
