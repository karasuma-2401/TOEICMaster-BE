# Báo cáo rà soát module Speaking

> **Ngày rà soát:** 2026-11-03  
> **Phiên bản đã đọc:** BE `feature/speaking-evaluation` @ `d9a9e76`; FE `feature/speaking-part-practice` (working tree)  
> **Phạm vi:** chỉ Speaking. Không sửa code. Writing và L&R chỉ được đọc để so sánh.  
> **Phương pháp:** đọc code và test, đối chiếu với PRD, Figma và nguồn ngoài; sau đó tái hiện bằng test tự động và dựng thử phương án trên bản sao (mục 0). Chưa chạy trên trình duyệt thật, nên hành vi theo từng trình duyệt vẫn cần đo theo [UAT runbook](./speaking-uat-runbook.md).

Đường dẫn `src/TOEICMaster.*` thuộc repo BE. Đường dẫn `FE:` thuộc repo `TOEICMaster-FE`, tính từ `src/features/speaking/` nếu không ghi khác.

---

## 0. Kết quả kiểm tra lại (2026-11-03)

Code BE và FE không đổi so với lần rà soát đầu. Test tái hiện và prototype chạy trên bản sao ngoài repo, nên repo không có thay đổi code nào.

### Mức độ xác minh

| Phát hiện | Cách xác minh | Kết quả |
| :--- | :--- | :--- |
| DUP-01, DUP-02 (cả giữa các Part), DUP-03 (cùng asset, và khác asset cùng hash) | Test BE qua handler thật | Đúng: đều lưu hoặc công bố được |
| MIC-01, MIC-02 | Test FE: chạy từ màn kiểm tra sang màn làm bài | Đúng: thứ tự `getUserMedia → POST /attempts → getUserMedia`, stream đầu bị dừng |
| MIC-03 | Test FE | Đúng, kèm hiệu chỉnh ở dưới |
| MIC-04, MIC-06 | Test FE | Đúng, kèm hiệu chỉnh ở dưới |
| UI-01, UI-02, UI-03, UI-04, UI-05, UI-07, DUP-04 | Test FE | Đúng |
| MIC-05, MIC-07, MIC-08, MIC-10, MIC-11, FLOW-01, UI-06, UI-08 đến UI-13, DOC, OPS-01 | Đọc code | Đúng theo code, chưa chạy thực tế |
| MIC-09; hộp thoại có hiện lần hai không | Cần trình duyệt thật | **Chưa xác minh** |

Bộ test gốc vẫn xanh trên bản sao: BE 286 test, FE Speaking 53 test.

### Hiệu chỉnh so với lần đầu

| ID | Lần đầu | Hiệu chỉnh | Lý do |
| :--- | :--- | :--- | :--- |
| MIC-04 | Lỗi mức Cao | **Quyết định sản phẩm, mức TB** | Hành vi này là có chủ đích: có test `lets learners practise on timers when the microphone is denied`. Nó chỉ lệch PRD ở đề đầy đủ. |
| MIC-03 | "Mở lại bài đã nộp" | **Trường hợp chính là micro vẫn bật sau khi nộp bài** | Trường hợp này xảy ra ở mọi lượt làm xong. Còn mở lại chỉ qua tải trang hoặc URL, vì FE không có liên kết quay lại (FLOW-02). |
| MIC-02 | Cao | **Cao, có điều kiện** | Thứ tự sai là chắc chắn, nhưng chỉ gây mất thời gian khi hộp thoại hiện lại. Với quyền đã lưu vĩnh viễn thì thường không có hộp thoại. |
| UI-01 | Cao | **TB, thành Cao khi có trên 100 bài `PUBLISHED`** | `notespeaking.md` ghi có 11 bài mẫu, nên giới hạn chưa bị chạm. Số 270 chỉ là số trong Figma. Chưa truy vấn số bài thật trên Neon. |
| ADR-002 | Chưa nêu tác động lên test | **Làm đỏ 22 test nếu không sửa bộ mẫu** | Bộ mẫu dùng chung một đoạn văn và một ảnh cho cả hai vị trí. |
| FLOW-02 | Chưa có | **Phát hiện mới, mức TB** | Xem mục 3. |

### Dựng thử phương án

Chạy trên bản sao, chưa đưa vào repo:

| Phương án | Quy mô thực tế | Kết quả |
| :--- | :--- | :--- |
| Chặn trùng ở BE (không migration, không API mới) | 1 file mới 61 dòng, thêm khoảng 10 dòng ở 2 file | Chặn đúng cả 5 kịch bản trùng. Ban đầu 22 test đỏ. Sau khi sửa bộ mẫu và helper `FullTestAsync`, còn 2 test đỏ cần sửa riêng. |
| Một phiên micro bằng layout và provider | 1 provider 106 dòng, 1 layout, sửa 3 file | `getUserMedia` đúng 1 lần, trước `POST /attempts`. Bài đã nộp: 0 lần. Dừng stream khi nộp và khi rời luồng. FE Speaking pass, typecheck sạch. |
| Giữ đúng vị trí ô (UI-02) | Khoảng 6 dòng ở 3 file | Pass. Server đã chấp nhận đề nháp chỉ có vị trí 2. |

Prototype micro còn thiếu: nút bật lại micro ở màn làm bài khi stream chưa có (banner hiện chỉ xử lý `denied` và `unsupported`). Việc layout giữ stream khi chuyển route mới được giả lập trong test, chưa chạy trên Next.js thật.

### Thứ tự xử lý đề xuất

Xếp theo tác động so với công sức. Chi tiết nằm trong [tasks.md](../features/speaking/tasks.md).

| Đợt | Nội dung | Sửa |
| :--- | :--- | :--- |
| 1. Sửa nhanh | UI-02, UI-03, UI-04, UI-07, MIC-06, MIC-11, DUP-04, DUP-01 | FE, 1 luật validator BE |
| 2. Micro | MIC-01, MIC-02, MIC-03, rồi đo trên trình duyệt | Chỉ FE |
| 3. Chặn trùng | DUP-02, DUP-03, sửa bộ mẫu test, rà dữ liệu | BE, không migration |
| 4. Đúng PRD | MIC-05, MIC-07, MIC-08, MIC-10, D1, D2 | Chỉ FE |
| 5. Builder | UI-01, UI-05, UI-06, UI-08, UI-10 đến UI-13, FLOW-02, nhân bản đề | FE, vài endpoint nhỏ |
| Song song | OPS-01 | File dùng chung, cần nhóm thống nhất |
| Để sau | `publish-check`, cảnh báo gần giống, số đề đang dùng bài, gỡ đề | Làm khi có nhu cầu thật |

**Thay đổi so với đề xuất ban đầu:** `publish-check` không còn là điều kiện đầu tiên. `POST /publish` đã trả đủ lỗi theo khóa, nên chỉ cần thêm luật ở server và sửa UI-03 là chặn được trùng và hiện đủ lỗi. Endpoint mới chỉ cần khi muốn báo lỗi trước lúc bấm công bố hoặc có cảnh báo không chặn.

---

## 1. Kết luận

| Vấn đề được giao | Kết luận | Phương án |
| :--- | :--- | :--- |
| Câu trong đề không được trùng | Hệ thống chỉ chặn trùng **ID bài tập**. Nội dung trùng vẫn công bố được. | [ADR-002](../decisions/002-speaking-test-content-uniqueness.md) |
| Chỉ xin quyền micro một lần | Mỗi luồng làm bài gọi `getUserMedia` **hai lần**. Lần thứ hai chạy khi đồng hồ server đã chạy. Micro vẫn bật sau khi nộp bài, và tải lại màn xem lại cũng xin quyền. | [ADR-003](../decisions/003-speaking-microphone-session.md) |
| UI tạo đề bất cập | Có 13 vấn đề. Nặng nhất hiện tại là chọn bài bị nhảy vị trí khi lưu. Giới hạn 100 bài chưa bị chạm với dữ liệu mẫu. | [Thiết kế Test Builder](../features/speaking/admin-test-builder.md) |

Mức độ trong các bảng dưới được hiệu chỉnh theo mục 0:

- **Cao:** sai nghiệp vụ, mất dữ liệu, hoặc chặn thao tác chính.
- **TB:** gây nhầm lẫn hoặc tốn công, nhưng có cách đi vòng.
- **Thấp:** chưa nhất quán, chưa ảnh hưởng đến kết quả.

---

## 2. Trùng nội dung trong đề (DUP)

| ID | Mức | Hiện trạng | Bằng chứng | Hậu quả |
| :--- | :--- | :--- | :--- | :--- |
| DUP-01 | Cao | 3 câu hỏi trong một bài Part 3 hoặc Part 4 có thể giống hệt nhau. Validator chỉ kiểm tra đủ 3 câu và độ dài. | `Application/Speaking/Admin/SpeakingAdminContracts.cs:131`; FE: `admin-draft.ts:125` | Câu 5–7 hoặc 8–10 có thể là cùng một câu. |
| DUP-02 | Cao | Mọi lớp kiểm tra đều so sánh `ExerciseId`, không so sánh nội dung. Hai bài khác mã, cùng đoạn văn vẫn công bố được. | `SpeakingAdminContracts.cs:167`; `SpeakingTestCommands.cs:106`; `Infrastructure/Persistence/Configurations/SpeakingConfiguration.cs:139` | Câu 1 và câu 2 có thể đọc cùng một đoạn văn. |
| DUP-03 | TB | Tải lại cùng một ảnh tạo asset mới. `sha256` đã được lưu nhưng không được dùng để so sánh. | `SpeakingExerciseCommands.cs:95` | Câu 3 và câu 4 có thể cùng một ảnh. |
| DUP-04 | TB | Ô chọn liệt kê mọi bài của Part, kể cả bài đang dùng ở vị trí khác. Checklist chỉ báo chung, không nói vị trí nào trùng. | FE: `components/admin/test-slot-picker.tsx:74`; `publish-checklist.ts:46` | Admin chỉ biết trùng sau khi đã chọn và phải tự dò. |
| DUP-05 | Thấp | Không thấy được một bài đang nằm trong bao nhiêu đề. | DTO admin không có trường này | Hai đề có thể gần giống nhau mà không ai biết. Dùng lại giữa các đề không sai, nhưng cần nhìn thấy được. |

**Phần đang đúng, cần giữ:** validator chặn trùng `ExerciseId` và trùng vị trí; có unique index `(test_id, exercise_id)`; publish kiểm tra lại toàn bộ; đề đã công bố dùng snapshot bất biến.

---

## 3. Quyền micro và ghi âm (MIC)

| ID | Mức | Hiện trạng | Bằng chứng | Hậu quả |
| :--- | :--- | :--- | :--- | :--- |
| MIC-01 | Cao | Màn kiểm tra và màn làm bài mỗi màn tạo một `useVoiceRecorder` riêng. Khi rời màn đầu, stream bị dừng; màn sau gọi lại `getUserMedia`. `src/app/speaking/` không có `layout.tsx` để giữ stream. | FE: `components/session/speaking-session.tsx:80`; `components/session/speaking-attempt.tsx:31,48`; `hooks/use-voice-recorder.ts:127` | Có thể hiện hộp thoại quyền lần hai. Chú thích trong code chỉ ghi "usually", tức không bảo đảm. |
| MIC-02 | Cao, có điều kiện | Lượt làm được tạo trên server **trước** lần gọi thứ hai. Domain vào câu 1 ngay khi tạo. | FE: `speaking-session.tsx` (`start`); `Domain/Entities/SpeakingAttempt.cs:106` | Nếu hộp thoại hiện lại, đồng hồ đang chạy trong lúc thí sinh bấm cho phép. |
| MIC-03 | Cao | Stream không được dừng khi nộp bài, nên micro vẫn bật suốt màn xem lại. Màn này còn xin micro ngay khi mount, không xét trạng thái. | FE: `speaking-attempt.tsx:46-50` | Sau mọi lượt làm, micro vẫn bật dù không ghi âm. Tải lại màn xem lại có thể hiện hộp thoại quyền. |
| MIC-04 | TB | Nút bắt đầu vẫn bấm được khi bị từ chối quyền hoặc trình duyệt không hỗ trợ. Đây là hành vi có chủ đích, có test bảo vệ. | FE: `components/session/mic-check-panel.tsx:56-57`, `mic-check-panel.test.tsx` | Hợp lý với luyện tập, nhưng trái PRD 4.3.2.A và AC-SPEAK-01 ở đề đầy đủ. Xem D1. |
| MIC-05 | TB | Chưa có bước nói thử, đo âm lượng, nghe lại, thử loa hay chọn micro. Thanh âm lượng chỉ chạy khi đang ghi. | FE: `use-voice-recorder.ts`; `mic-check-panel.tsx` | Thiếu so với PRD 4.3.2.A và Figma 20. Micro tắt tiếng vẫn được coi là sẵn sàng. |
| MIC-06 | TB | Mọi lỗi đều thành `denied`. Trang không có HTTPS thành `unsupported`. | FE: `use-voice-recorder.ts:58` | Không có micro hoặc micro đang bận vẫn nhận hướng dẫn "cấp quyền", tức là sai cách xử lý. |
| MIC-07 | TB | Không xử lý rút micro giữa bài: không nghe `track.ended`, `MediaRecorder.onerror` hay `devicechange`. | FE: `use-voice-recorder.ts` | Thí sinh mất bản ghi mà không được báo. |
| MIC-08 | TB | Không có tiếng beep. Tìm `beep`, `oscillator`, `tiếng bíp` trong module đều không có kết quả. | FE: `src/features/speaking` | Trái PRD 4.3.2.B và spec cũ. |
| MIC-09 | Thấp | `AudioContext` tạo không qua thao tác người dùng ở màn làm bài và không gọi `resume()`. | FE: `use-voice-recorder.ts:49` | Rủi ro: thanh âm lượng có thể đứng yên. Bản ghi không bị ảnh hưởng vì `MediaRecorder` dùng trực tiếp stream. Cần đo thực tế. |
| MIC-10 | TB | Bản ghi đang ghi nằm trong bộ nhớ. Tải lại trang trong pha trả lời sẽ mất phần đó và không có cảnh báo. | FE: `use-voice-recorder.ts`, `hooks/use-recording-uploads.ts` | Mất câu trả lời, thường là không chấm được câu đó. |
| MIC-11 | Thấp | Màn giới thiệu ghi "Bản ghi chỉ lưu trong tab hiện tại", trong khi bản ghi được tải lên server. Bản ghi của khách lưu 30 ngày. | FE: `components/speaking-test-overview.tsx:113`; `SpeakingRecordingRetentionWorker.cs:14` | Thông tin về dữ liệu cá nhân bị sai. |
| FLOW-01 | TB | Có nút bỏ qua ở mọi pha, kể cả pha chuẩn bị của đề đầy đủ. `Next` không xét mode. | FE: `components/session/session-stage.tsx`; `SpeakingAttempt.cs:133` | Spec cũ cấm bỏ qua pha chuẩn bị. Cần quyết định, xem mục 7. |
| FLOW-02 | TB | FE không gọi `GET /attempts`, và chỉ `speaking-session.tsx` dẫn đến `/speaking/attempts/{id}`. Backend đã có API lịch sử. | FE: `api.ts`; tìm `speaking/attempts` trong `src` | Rời màn kết quả thì không có đường quay lại trong app để xem bài hay điểm AI, trong khi job chấm có thể chạy lâu. Trái AC-HIST-01. |

---

## 4. UI tạo đề (UI)

Phương án cho từng dòng nằm ở [admin-test-builder.md](../features/speaking/admin-test-builder.md).

| ID | Mức | Hiện trạng | Bằng chứng | Hậu quả |
| :--- | :--- | :--- | :--- | :--- |
| UI-01 | TB | Builder chỉ tải 100 bài `PUBLISHED` mới nhất của **tất cả** Part, và không có tìm kiếm. | FE: `components/admin/speaking-test-workspace.tsx:95` | Khi có trên 100 bài, bài cũ không chọn được (thành mức Cao). Figma mô tả ngân hàng 270 câu và riêng Part 1 có 85 bài. Dữ liệu mẫu hiện chưa tới mức này. |
| UI-02 | Cao | Vị trí bị dồn khi lưu: `compact()` bỏ ô trống, `sequenceNumber` lấy theo chỉ số mới, parser đọc lại bằng `push`. | FE: `speaking-test-workspace.tsx:46`; `admin-api.ts:85-88`; `parsers.ts:208-212` | Chọn "Bài 2", để trống "Bài 1", sau khi lưu bài nhảy lên "Bài 1". |
| UI-03 | TB | Chỉ giữ lỗi đầu tiên của mỗi trường, trong khi `Exercises` có thể trả nhiều lỗi. | FE: `http.ts:24-25` | Admin phải sửa rồi công bố lại nhiều lần mới thấy hết lỗi. |
| UI-04 | TB | Mọi lỗi `409` hiện chung một câu, bỏ qua thông báo chính xác của server. Câu đó còn khuyên đóng rồi mở lại. | FE: `speaking-test-workspace.tsx:53` | Không phân biệt được trùng mã và xung đột version. Làm theo thì mất phần đang sửa. |
| UI-05 | TB | "Hủy" và nút đóng thoát ngay, không hỏi khi có thay đổi chưa lưu. | FE: `speaking-test-workspace.tsx:188,193` | Mất các lựa chọn. |
| UI-06 | TB | "Lưu bản nháp" đóng builder. | FE: `components/admin/admin-speaking-tests-page.tsx` (`onSaved`) | Muốn làm tiếp phải mở lại và tải lại. |
| UI-07 | Thấp | Lỗi khi lưu nháp cũng có tiêu đề "Chưa thể công bố". | FE: `components/admin/publish-checklist-panel.tsx:58` | Hiểu sai loại lỗi. |
| UI-08 | TB | Huy hiệu `2/2` xanh chỉ đếm số ô đã chọn, trong khi checklist có thể báo chưa đạt vì bài đã rời trạng thái `PUBLISHED`. Không có lý do hay liên kết đến chỗ cần sửa. | FE: `test-slot-picker.tsx:38`; `publish-checklist.ts` | Hai tín hiệu mâu thuẫn. Figma 43 của L&R có banner bấm để cuộn đến lỗi. |
| UI-09 | TB | Đề đã công bố không thể gỡ hay nhân bản. Đề nháp không thể xóa. | `AdminSpeakingController.cs` không có các endpoint này | Đề sai phải tạo lại từ đầu và vẫn hiện cho học viên. L&R đã có cơ chế fork ([ADR-001](../decisions/001-admin-lr-exam-revisions.md)). |
| UI-10 | Thấp | Trộn đơn vị: tiêu đề ghi `2/2/3/3/1 CÂU`, ô chọn đếm theo bài `2/2/1/1/1`. | FE: `speaking-test-workspace.tsx`, `test-slot-picker.tsx` | Dễ tưởng Part 3 cần chọn 3 bài. |
| UI-11 | TB | Bảng Part 4 nhập bằng cú pháp `nhãn \| nội dung`. Giới hạn của server không được kiểm tra trước. Form chỉ báo một lỗi mỗi lần. | FE: `admin-draft.ts:36-48,110-181`; `SpeakingAdminContracts.cs` | Nhập dễ sai, lỗi chỉ hiện sau khi gửi. |
| UI-12 | TB | Form bài tập đổi được trạng thái mà không báo bài đang nằm trong đề nháp nào. | FE: `components/admin/speaking-exercise-editor.tsx` | Một đề nháp có thể mất điều kiện công bố mà không ai biết. |
| UI-13 | Thấp | Chỉ xem trước từng bài, không xem được cả đề theo thứ tự 11 câu. Part chưa có bài không có lối tắt để tạo bài. | FE: `components/admin/test-preview-panel.tsx` | Khó soát cả đề trước khi công bố. |

---

## 5. Tài liệu lệch thực tế (DOC)

| ID | Hiện trạng | Xử lý |
| :--- | :--- | :--- |
| DOC-01 | `spec.md` cũ chỉ ghi chấm bằng Gemini, trong khi thực tế dùng Azure Speech và Gemini. | Đã sửa trong `spec.md` và `ai-evaluation.md`. |
| DOC-02 | `spec.md` cũ liên kết sai `../../notespeaking.md`, và ánh xạ Figma ghi màn 19 (L&R) thay vì 23. | Đã sửa trong `spec.md`. |
| DOC-03 | `contracts/api-contract.md` chưa có mục Speaking. Handoff phía FE (23/10) bị gitignore và có trước tính năng luyện Part. | Tạm dùng `admin-api.md` và `attempt-api.md`. Xem đề xuất ở mục 8. |
| DOC-04 | PRD 5.5.1 yêu cầu cấu hình thời gian từng câu, tải audio câu hỏi và audio mẫu. Code cố định thời gian (`SpeakingTiming`), không có audio; Part 3–4 dùng nút đọc bằng giọng trình duyệt. | Giữ thời gian cố định vì đúng format ETS. Việc audio là quyết định mở, xem mục 7. |
| DOC-05 | Figma dùng `/exams/speaking/...` và `/admin/exams/speaking`, còn code dùng `/speaking/...` và `/admin/tests/speaking`. | Đã có bảng ánh xạ trong `spec.md`. Không đổi route. |
| DOC-06 | Tài liệu ghi "~20 phút", trong khi tổng đọc, chuẩn bị và trả lời trong code là 618 giây (khoảng 10,3 phút). UI cũng hiện theo số này. | Hai số đều đúng theo cách tính khác nhau: 20 phút của ETS là cả bài thi. Cần giải thích trên UI. |
| OPS-01 | `ffmpeg` và biến `AzureSpeech__*`, `Gemini__*` chỉ có trong `docker-compose.override.yml`. File này bị loại khỏi git qua `.git/info/exclude`. `Dockerfile`, `docker-compose.yml`, `docker-compose.shared.yml` và `.env.example` đều chưa có. | Chấm AI chạy được trên máy của Jin, nhưng máy khác hoặc bản triển khai từ repo sẽ không chấm được. Cần chuyển vào file dùng chung, xem mục 8. |

---

## 6. Tham chiếu đã dùng

**Trong hệ thống**

| Nguồn | Học được gì |
| :--- | :--- |
| Writing (`WritingTestCommands.cs:28-31`, `admin-writing-pages.tsx:940-957`) | Cùng mô hình chặn theo ID và cùng ô chọn không loại trừ. Vì vậy đây không phải mẫu cần sao chép cho vấn đề này. Không đề xuất sửa Writing trong phạm vi này. |
| L&R ([ADR-001](../decisions/001-admin-lr-exam-revisions.md), Figma 43) | Fork khi cần sửa đề đã công bố; banner lỗi có liên kết đến vị trí; bảng ô theo vị trí. |

**Bên ngoài**

| Nguồn | Nội dung liên quan | Độ tin cậy |
| :--- | :--- | :--- |
| [ETS: TOEIC Speaking & Writing](https://www.ets.org/toeic/about/speaking-writing.html) | 11 câu, khoảng 20 phút, thang 0–200, thời gian từng dạng. Khớp `SpeakingTiming`. | Chính thức |
| [Azure: REST cho audio ngắn](https://docs.azure.cn/en-us/ai-services/speech-service/rest-speech-to-text-short) | Pronunciation assessment giới hạn 30 giây, lý do phải cắt đoạn. | Tài liệu nhà cung cấp (bản azure.cn) |
| [MDN: getUserMedia](https://developer.mozilla.org/docs/Web/API/MediaDevices/getUserMedia) | Secure context; ý nghĩa `NotAllowedError`, `NotFoundError`, `NotReadableError`. | Chuẩn tham khảo |
| [MDN: Permissions.query](https://developer.mozilla.org/docs/Web/API/Permissions/query) | Promise bị reject khi không hỗ trợ, nên phải dùng `try/catch`. | Chuẩn tham khảo; bảng hỗ trợ cần kiểm tra lại |
| [Chrome: One-time permissions](https://developer.chrome.com/blog/one-time-permissions) | Chọn "Allow this time" thì lần sau hỏi lại; quyền có thể hết hạn về `prompt`. | Chính thức |
| [Chrome: Permissions request chip](https://developer.chrome.com/blog/permissions-chip) | Website phải tự chọn đúng thời điểm và ngữ cảnh để xin quyền. | Chính thức |
| [W3C webrtc logs (2019)](https://lists.w3.org/Archives/Public/public-webrtc-logs/2019Oct/0176.html) | Safari hỏi theo từng document, có hết hạn. | **Cũ, cần đo lại** |
| [Apple Forums (2025)](https://developer.apple.com/forums/thread/788518) | Có người báo iOS hỏi lại khi đổi route trong SPA. | **Báo cáo đơn lẻ, chưa xác nhận** |
| [Cambridge: Speaking Test Setup](https://auto-metrica.cambridgeenglish.org/metrica/systemTest.aspx) | Nói vào micro 5 giây rồi xác nhận âm lượng khi nghe lại. | Trang của đơn vị khảo thí |
| [Talview: Precheck](https://certification.talview.com/support/solutions/articles/11000130937-complete-guide-of-precheck-process-for-online-exams), [Prometric FAQ](https://www.prometric.com/files/cbla/Speaking-Platform-FAQs-for-Candidates.pdf) | Kiểm tra hệ thống trước khi vào phiên thi là thông lệ. | Nhà cung cấp |
| [Moodle forum](https://moodle.org/mod/forum/discuss.php?d=251833), [Moodle docs 2.6](https://docs.moodle.org/26/en/Building_Quiz) | Moodle không cho một câu xuất hiện hai lần, nhưng hai **bản sao** của cùng câu vẫn lọt. Đây chính là DUP-02. | Tài liệu cũ, nguyên lý vẫn đúng |
| [ASC: Enemy items](https://assess.com/enemy-items/), [FastTest](https://www.fasttestweb.com/manual/enemy-items/) | "Enemy items": hai câu không nên cùng xuất hiện trong một đề, kể cả khi không trùng chữ. | Nhà cung cấp phần mềm khảo thí |

**Chưa xác minh được:** không tìm thấy tài liệu chính thức về quy tắc kiểm tra micro của TOEFL Home Edition, Duolingo English Test và Pearson OnVUE, nên báo cáo không dựa vào các hệ thống này.

---

## 7. Quyết định chờ chủ tính năng

Tài liệu đang viết theo phương án đề xuất. Nếu chọn khác, cần cập nhật ADR và `spec.md`.

| # | Câu hỏi | Đề xuất | Lý do |
| :--- | :--- | :--- | :--- |
| D1 | Khi không có quyền micro thì xử lý thế nào? | Đề đầy đủ: chặn. Luyện Part: cho chọn rõ "Luyện không ghi âm". | Đúng AC-SPEAK-01 và vẫn giữ được việc luyện theo thời gian. |
| D2 | Có cho bỏ qua pha không? | Đề đầy đủ: bỏ nút bỏ qua ở pha đọc và chuẩn bị. Luyện Part: giữ. | Gần với phòng thi, và luyện tập vẫn linh hoạt. |
| D3 | Ngưỡng cảnh báo nội dung gần giống? | Jaccard ≥ 0,8, chỉ cảnh báo, không chặn. | Con số này là điểm khởi đầu, cần hiệu chỉnh trên dữ liệu thật. |
| D4 | Đề đã công bố bị sai thì làm gì? | Làm "nhân bản thành nháp" trước. "Gỡ đề" làm sau vì cần migration. | Nhân bản không cần đổi schema. |
| D5 | Part 3–4 có cần audio câu hỏi không? | Giữ giọng đọc của trình duyệt cho MVP, ghi nhận là khác PRD. | Audio cần thêm luồng tải lên và dữ liệu. |

---

## 8. Đề xuất sửa file dùng chung (chưa thực hiện)

Các file này dùng chung với nhóm nên chưa được sửa. Mỗi mục chỉ cần một đến hai dòng:

- `docs/README.md`: thêm vào cây thư mục các file mới của Speaking, ADR-002, ADR-003 và hai runbook.
- `docs/features/README.md`: đổi mô tả Speaking thành "Chấm AI bằng Azure Speech + Gemini".
- `docs/runbooks/README.md`: thêm liên kết đến hai runbook Speaking.
- `docs/contracts/api-contract.md`: thêm mục Speaking trỏ đến `features/speaking/admin-api.md` và `attempt-api.md`.

File cấu hình dùng chung (OPS-01), cũng chưa sửa:

- `Dockerfile`: cài `ffmpeg` trong stage runtime. Bước cài có sẵn ở `docker-compose.override.yml:22`.
- `docker-compose.yml` và `.env.example`: thêm các biến `AzureSpeech__*` và `Gemini__*` như `docker-compose.override.yml:30-35`.
