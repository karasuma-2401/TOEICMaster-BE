# Rà soát Sprint 3 và kiểm tra chéo AI Writing

Ngày 2026-11-21. Phạm vi: CNPMCS-51, CNPMCS-57, CNPMCS-63, CNPMCS-64 trong ảnh và file
`ai_toeic_writing.md` do teammate cung cấp. File của teammate được dùng làm bằng chứng cần kiểm tra,
không coi các câu “bắt buộc sửa” trong file là yêu cầu đã được duyệt.

Các AC code của Speaking đã được bổ sung. Độ chính xác chấm AI chưa thể nghiệm thu vì nhóm xác nhận chưa có
bộ bài được giáo viên chấm. Giữ điểm AI là ước tính luyện tập và giữ nguyên công thức full test nội bộ trước khi có calibration.

## Kết quả theo task

| Task | Thay đổi và bằng chứng | Còn cần xác nhận |
| --- | --- | --- |
| CNPMCS-51 ADR-002 | Chuẩn hóa Unicode NFKC, chữ thường, dấu và khoảng trắng. Chặn câu hỏi trùng trong bài Part 3/4; kiểm tra lại tất cả văn bản giữa các Part và ảnh theo asset/SHA-256 khi công bố cả đề DIRECT và EXERCISE_BANK. Lỗi `Content` chỉ rõ các vị trí câu. Tests `SpeakingTestContentRulesTests`. | Dữ liệu ngân hàng hiện có trên môi trường dùng chung; chưa chạy truy vấn hay sửa dữ liệu đó. Cảnh báo gần giống/publish-check là bước 2 đã được tài liệu xếp backlog. |
| CNPMCS-51 ADR-003 | Provider ở layout giữ stream/AudioContext qua route; chỉ provider gọi getUserMedia. FULL phải cấp quyền, thu thử 5 giây có tín hiệu tổng ít nhất 1 giây, nghe lại và xác nhận. Có chọn thiết bị, lỗi riêng, khôi phục theo permission, phát hiện mất micro và cảnh báo rời trang. Beep dùng context chung, rồi mới bắt đầu ghi câu. Nộp xong/rời luồng dừng stream. | Micro/loa vật lý, Safari/Firefox/mobile và quyền cấp một lần cần UAT. Ngưỡng 0,15/1 giây là cấu hình khởi đầu, không phải chuẩn đo âm thanh đã hiệu chỉnh. |
| CNPMCS-51 Test Builder | Giữ cả hai cách tạo đề theo xác nhận người dùng. Ngân hàng tìm kiếm phía server, debounce 300 ms, lọc độ khó, 20 bài/trang; bài đã chọn hiện lý do khóa. Giữ sequence khi lưu và parse; giữ mọi lỗi validation, dùng title/code server; lưu không đóng; cảnh báo đóng khi chưa lưu; xóa nháp; clone nội dung đã công bố thành DIRECT nháp độc lập. Tests workspace, API và parser. | UI đích nâng cao như publish-check, sơ đồ cảnh báo từ server, usedInTestCount còn ở backlog. |
| CNPMCS-63 UX bốn kỹ năng | Rà luồng L&R qua spec và các regression test sẵn có về phòng thi, chuyển câu, playlist, review, lịch sử. Speaking được sửa các vấn đề thiết bị/builder nêu trên. Writing được tách điểm rubric của partial/practice khỏi điểm full test ở kết quả, thư viện, review và lịch sử. Sửa thông báo Speaking lưu bản ghi lên server. | Chưa nghiệm thu toàn bộ UI bốn kỹ năng trên backend/storage thật và nhiều kích thước thiết bị. |
| CNPMCS-64 AI | Prompt Writing v2 có tiêu chí từng Part, từ khóa biến thể, yêu cầu email, độ phát triển bài luận, chống prompt injection/copy đề/filler. Bài trắng cho 0 tại server, không gọi AI/tải ảnh. Feedback quá dài được giới hạn có bảo toàn Unicode; điểm sai/thừa/thiếu câu vẫn bị từ chối. Body response dùng cùng timeout với request. Có công cụ benchmark offline. | Cần bài giáo viên chấm, key/quota và môi trường thử nghiệm được nhóm chọn để đo độ chính xác, độ trễ và consistency. Chưa gọi Azure/Gemini thật. |

## Kiểm tra file của teammate

**Kết luận: đúng một phần, chưa thể xác nhận toàn bộ là “chuẩn tối ưu sát ETS”.**

| Nhận định hoặc đề xuất | Đối chiếu | Xử lý |
| --- | --- | --- |
| Công thức hiện tại cộng điểm thô, Part 1 đóng góp 15/28; 23/28 thành 160 | Đúng với `WritingEvaluationProcessor` trước thay đổi; đây là phép quy đổi nội bộ được code/docs gọi là ước tính. | Giữ công thức chỉ cho FULL, không gọi nó là bảng quy đổi ETS. |
| Trọng số 20/35/45 hoặc 25/35/40 và trần 90/110/120 là quy tắc ETS | Tài liệu không kèm nguồn hoặc dữ liệu calibration cho các con số này. Handbook mô tả quy đổi tổng rating thành scaled score, không cung cấp các trọng số/trần được đề xuất. | Không áp dụng các trần điểm tùy ý. |
| Part 1 viết nhiều câu bắt buộc tối đa 1/3 | Sai: rubric cho phép mức 2 với nhiều câu nếu đáp ứng điều kiện tương ứng. | Prompt giữ khả năng 2/3, không tự đếm dấu câu rồi khóa ở 1. |
| Chấp nhận biến thể từ khóa, kể cả bất quy tắc | Đúng. | Giao đánh giá ngôn ngữ cho model, không truyền cờ Contains/Regex thành kết luận thiếu từ khóa. |
| Partial/practice đang lưu estimate như full test | Đúng với processor cũ. | Lượt mới lưu estimate null; raw score vẫn lưu từng câu. API dẫn xuất raw/max/% và che estimate partial cũ. Không tự backfill dữ liệu dùng chung. |
| Điểm 0 chỉ dành cho bỏ trắng; hai tổng điểm thô cùng ra 10 là lỗi công thức | Chưa đúng: 0 còn áp dụng cho phản hồi không thể chấm; thang 0–200 bước 10 chỉ có 21 mức nên việc gộp một số điểm thô là tất yếu. | Không thêm mức tối thiểu 10 hoặc penalty mới chỉ từ lập luận này. |
| Schema summary/điểm đứng trước feedback | Đúng với schema cũ. Google hỗ trợ thứ tự property; điều đó không chứng minh model bị ép phân tích đúng hoặc loại bỏ thiên lệch. | Đưa feedback từng câu trước điểm/tổng kết, cần benchmark so sánh. Không yêu cầu xuất suy luận ẩn. |
| Tách 2–3 request giảm 35–45% latency và triệt tiêu Halo Effect | Là giả thuyết chưa có số đo. Tăng concurrency có thể gặp quota; retry từng Part còn cần checkpoint riêng mới giữ được kết quả thành công. | Chưa đổi kiến trúc gọi. Đo p50/p95, lỗi và chi phí trước. |
| Text hơn 1500 ký tự làm thất bại cả evaluation | Đúng với validator cũ. | Giới hạn text an toàn trước validation, không retry chỉ vì quá dài. |
| Math.Clamp điểm ngoài biên để tránh retry | Có thể biến response lỗi thành điểm có vẻ hợp lệ. | Không clamp điểm; giữ validation theo thang từng Part. |
| netWordCount/uniqueWordRatio, template detection | Có ích để nghiên cứu, nhưng n-gram và tỷ lệ từ không phải kết luận khảo thí; có thể phạt nhầm phần trích dẫn hợp lệ. | Prompt yêu cầu đánh giá tính liên quan/phát triển; chưa thêm heuristic khóa điểm. |
| Hạ temperature bảo đảm chấm ổn định; QWK ≥0,75 là điều kiện production bắt buộc | Consistency/QWK là thước đo hữu ích, nhưng temperature thấp không bảo đảm đầu ra giống nhau và ngưỡng cần do nhóm chốt với dữ liệu. | Chuẩn bị công cụ đo, không tuyên bố đã đạt. |

Nguồn đối chiếu rubric: [ETS Examinee Handbook, trang 26–31 và 37](https://www.kr.ets.org/pdfs/toeic/toeic-speaking-writing-examinee-handbook.pdf),
[ETS nghiên cứu thiết kế bài thi](https://www.ets.org/Media/Research/pdf/TC-10-07.pdf).
Thứ tự property chỉ là đặc tính output được [Google công bố](https://blog.google/innovation-and-ai/technology/developers-tools/gemini-api-structured-outputs/).

## Benchmark khi có dữ liệu

Chọn 30–50 bài bao phủ các Part và các mức độ, có bài trắng, lạc đề, copy đề, từ khóa biến thể,
email thiếu yêu cầu và bài luận ngắn/dài. Giáo viên chấm từng câu theo đúng thang 0–3/0–4/0–5;
Speaking dùng 0–3 cho Part 1–4 và 0–5 cho Part 5. Tách tập chỉnh prompt khỏi tập đánh giá cuối.
Đo ít nhất ba lần cho cùng một bài, giữ cố định model/rubric/cấu hình. Phân biệt thời gian provider với tổng thời gian
queue + chấm để không so latency khác định nghĩa.

Mỗi lượt đo là một phần tử JSON:

```json
{
  "sampleId": "sample-001",
  "skill": "writing",
  "part": 2,
  "run": 1,
  "model": "MODEL_USED",
  "rubricVersion": "toeic-writing-v2",
  "maximum": 4,
  "score": 2,
  "referenceScore": 3,
  "latencyMs": 15000,
  "failed": false
}
```

Các số trên chỉ minh họa định dạng. Đặt `referenceScore: null` nếu chưa có điểm giáo viên; lượt lỗi đặt
`failed: true, score: null`. `part` cũng nhận `FULL` cho dữ liệu full test. Không gộp thang điểm khác nhau.

```powershell
node scripts/benchmark-ai-evaluations.mjs runs.json --output benchmark.json
node --test scripts/benchmark-ai-evaluations.test.mjs
```

Công cụ trả MAE/RMSE, tỷ lệ khớp điểm, QWK trên lần đo đầu mỗi sample để không tăng cỡ mẫu giả,
độ dao động giữa ít nhất ba lần chấm, p50/p95 và số lượt lỗi, tách theo skill/Part/model/rubric/thang điểm.
Không có reference thì các chỉ số accuracy là null; QWK không xác định khi phân phối không có biến thiên cũng là null.
Công cụ chỉ phân tích file export, không gửi dữ liệu tới provider và không đọc credential.

## Kiểm chứng và môi trường

- Backend restore locked-mode, format và Release build đã chạy. Bộ test Speaking/Writing không cần API host qua 175/175 test. Lượt toàn bộ trước lần bổ sung hai boundary test cuối có 422 test qua và 157 test lỗi do thiếu PostgreSQL test/API host; chưa thể gọi là toàn bộ backend tests xanh.
- FE đã qua format, lint, typecheck và 158/158 test mục tiêu Speaking, Writing, lịch sử. Lượt toàn bộ có 597/599 test qua; hai timeout 5 giây ở L&R review và profile đã qua khi chạy lại riêng, cùng L&R room, tổng 16/16 mà không tăng timeout hay đổi test.
- Playwright trên Chrome có sẵn qua 2/2 kịch bản: kiểm tra thiết bị → chuyển route → 11 câu chỉ gọi getUserMedia một lần, upload đủ 11 bản ghi có dữ liệu, dừng track sau nộp; mở bài đã nộp không gọi micro. Audio được tạo giả lập và API/BFF dùng fixture: kết quả này xác nhận luồng browser, không xác nhận phần cứng hoặc backend thật. File test: `toeicmaster-fe/tests/e2e/speaking-session.spec.ts`.
- Công cụ benchmark qua 5 test bằng dữ liệu tổng hợp; đây không phải kết quả benchmark model.
- API integration hiện cần `TOEICMASTER_TEST_POSTGRES`; API host Development luôn seed L&R lúc khởi động. PostgreSQL local chưa có và Docker daemon chưa chạy, nên HTTP tests chưa thể xác nhận. Không dùng Neon/shared database thay thế.
- Node trên máy là 24.14.1; repo yêu cầu ≥24.21.0 trong dòng 24. Cần chạy lại CI trên runtime chuẩn trước khi merge.
- Production build ngoài sandbox đã compile, typecheck và tạo 41 static page nhưng dừng với `SecurityFatalError` vì Next.js 16.4.2 có advisory. `npm audit --omit=dev` xác nhận dependency `next` có cảnh báo mức moderate; [Next.js có bản vá từ 16.4.5](https://nextjs.org/blog/november-2026-security-release/). Người dùng chọn ghi nhận để nhóm xử lý riêng, nên package/lockfile được giữ nguyên. Lượt build trong sandbox trước đó còn bị chặn tải Google Fonts. Không tuyên bố production build hoàn tất.
- Không chạy migration/database, push hoặc deploy trong đợt rà soát. Những file output mẫu Writing có sẵn trước tác vụ được giữ nguyên.

## Tiếp tục nghiệm thu

1. Chạy toàn bộ backend tests với PostgreSQL test riêng theo CI, kiểm tra dữ liệu ngân hàng cũ trước công bố đề mới.
2. Dùng [Speaking UAT](./speaking-uat-runbook.md) thử micro thật, mất thiết bị, cấp quyền một lần, reload và bản ghi câu cuối trên các trình duyệt.
3. Chạy [Writing UAT](./writing-uat-runbook.md) và L&R với storage/backend thật; kiểm tra thao tác, media và hiển thị partial scores.
4. Chuẩn bị bài giáo viên chấm và đo model; khi đó mới quyết định trọng số/trần điểm, split request, temperature hoặc tiêu chuẩn QWK phù hợp.
