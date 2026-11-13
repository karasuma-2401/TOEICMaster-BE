# AI evaluation cho TOEIC Writing

CP6 đánh giá Writing theo hàng đợi bền vững. Bài nộp và câu trả lời vẫn được lưu trước; lỗi provider, quota hoặc timeout không làm mất attempt.

## Luồng sử dụng

1. User nộp bài. Server tạo một evaluation ở trạng thái `CONSENT_REQUIRED`.
2. User đọc thông báo dữ liệu được gửi tới AI và bấm đồng ý. Chỉ lúc đó job chuyển sang `QUEUED`.
3. Background worker lấy job, gọi Gemini ở server và lưu JSON feedback đã kiểm tra.
4. FE tự tải lại trạng thái mỗi 5 giây khi job `QUEUED`, `PROCESSING` hoặc `RETRYING`.
5. Khi xong, UI gọi điểm bài thi là **điểm TOEIC Writing ước tính bởi AI (0–200, bước 10)**, không phải điểm ETS chính thức. Bài tập lẻ hiển thị điểm thô theo Part; bài mẫu không trả cho học viên.

`FAILED` có thể được User gửi chấm lại. Consent đã lưu được dùng lại; lần chấm lại dùng model và rubric đang cấu hình. Browser không tự gửi bài ra provider nếu chưa có consent lần đầu.

Để bảo vệ Free Tier, guest/anonymous session chỉ được bắt đầu một evaluation AI mới trong mỗi cửa sổ 24 giờ. Retry của **cùng attempt** không bị tính lại; User đăng nhập chưa bị áp quota trong checkpoint này.

Admin có JWT role `Admin` có thể review an toàn một evaluation tại `GET /api/v1/admin/writing/evaluations/{attemptId}`. Endpoint trả attempt snapshot cùng trạng thái/model/rubric/feedback đã lưu; không trả raw provider response hoặc key. Luồng override có audit log được để riêng cho hardening, không được dùng để thay đổi ngầm điểm AI gốc.

## Cấu hình Gemini

Không commit API key. Chép các biến dưới đây vào `.env` local hoặc secret của môi trường deploy:

```dotenv
GEMINI_ENABLED=true
GEMINI_API_KEY=your_server_side_key
GEMINI_MODEL=gemini-3.5-flash-lite
GEMINI_REQUEST_TIMEOUT_SECONDS=45
GEMINI_RUBRIC_VERSION=toeic-writing-v1
GEMINI_USE_WRITING_REFERENCE_ANSWERS=false
```

`GEMINI_ENABLED=false` là mặc định an toàn. Với cấu hình này, UI và consent flow vẫn test được nhưng job sẽ chuyển `FAILED` với thông báo dịch vụ chưa được cấu hình. API key không được trả về API User, log hay Next.js browser bundle.

Tạo database schema (không chạy lệnh này lên database dùng chung nếu chưa được nhóm duyệt):

```powershell
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

Migration của checkpoint là `AddWritingEvaluations` và `AddWritingAttemptReviewSnapshots`; chúng thêm
`writing_evaluations`, `writing_evaluation_feedback`, review snapshot bất biến của attempt và điểm rubric thô.

## Rubric, bài mẫu và quy đổi điểm

Worker yêu cầu Gemini trả một điểm thô cho từng câu theo Part: Part 1 là **0–3**, Part 2 là **0–4**,
Part 3 là **0–5**. Server kiểm tra vị trí câu, biên điểm và toàn bộ JSON trước khi lưu; response sai sẽ không
được hiển thị như kết quả hợp lệ.

Điểm hiển thị được quy đổi nội bộ từ tổng điểm thô về thang 0–200, làm tròn theo bước 10:

```text
round((tổng điểm thô / tổng điểm tối đa của attempt) × 20) × 10
```

Đây là quy đổi luyện tập minh bạch, không phải bảng quy đổi ETS độc quyền. Model answer tùy chọn và rubric được snapshot
khi tạo attempt; API học viên không trả hai dữ liệu này. Theo mặc định AI chỉ nhận rubric; chỉ bật
`GEMINI_USE_WRITING_REFERENCE_ANSWERS` sau khi nhóm đã calibration. Khi bật, bài mẫu là tham khảo, không phải đáp án duy nhất.
Thay `GEMINI_RUBRIC_VERSION` khi thay đổi cách dùng bài tham khảo để phân biệt kết quả giữa các cấu hình.
Ảnh Part 1 được gửi tới Gemini sau consent; tổng dữ liệu ảnh tối đa 12 MiB mỗi evaluation, vượt giới hạn sẽ báo lỗi an toàn.

Editor tắt `spellcheck` và autosave debounce **800ms** sau lần gõ cuối. Browser không có API đáng tin cậy để biết
chính xác nội dung paste đến từ chính editor hay một nguồn ngoài, nên không triển khai cơ chế chặn paste có thể làm
mất dữ liệu; server vẫn lưu/kiểm soát attempt, timer và ownership.

## Retry và dữ liệu lưu

- Timeout, mạng, HTTP 429 và HTTP 5xx: retry tối đa 3 lần với backoff 1, 3, 9 phút; nếu provider gửi `Retry-After` lớn hơn thì tôn trọng giá trị đó.
- Lỗi key/configuration, request bị từ chối hoặc JSON không hợp lệ sau retry: trạng thái `FAILED`, bài nộp vẫn giữ nguyên.
- Worker có lease 2 phút và optimistic concurrency (`Version`) để một job không bị hoàn thành hai lần khi có nhiều instance API.
- Chỉ lưu điểm, feedback đã chuẩn hóa, model/rubric version và mã lỗi an toàn. Không lưu raw response của provider hoặc API key.

## Calibration trước demo chính thức

Gemini là công cụ phản hồi học tập, không phải scorer ETS. Trước khi bật cho User thật, tạo bộ 30–50 bài đã được giảng viên/nguồn đáng tin chấm, gồm đủ Part 1/2/3 và các mức điểm. Với mỗi version rubric/model, ghi:

| Trường | Ví dụ |
|---|---|
| `attemptId` hoặc fixture id | `calibration-p2-07` |
| Part/mode | `PART_2` |
| Điểm kỳ vọng | `140` |
| Điểm AI | `150` |
| Chênh lệch | `+10` |
| Nhận xét reviewer | `quá ưu tiên từ vựng hơn đáp ứng yêu cầu` |

Chỉ nâng `GEMINI_RUBRIC_VERSION` sau khi review chênh lệch; lịch sử evaluation đã hoàn thành giữ model và rubric version cũ để audit.

## UAT và bật demo

Xem [Writing UAT runbook](../../runbooks/writing-uat-runbook.md) trước khi chạy migration hoặc gửi một bài test đến Gemini. Runbook
liệt kê luồng User/Admin, điều kiện an toàn cho Free Tier và các giới hạn đã được chấp nhận.
