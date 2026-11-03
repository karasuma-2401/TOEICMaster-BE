# ADR-002: Không trùng nội dung giữa các câu trong đề Speaking

## Status

Proposed (chờ chủ tính năng duyệt; chưa triển khai)

## Date

2026-11-03

## Context

Một đề Speaking có 11 câu, lắp từ 7 bài tập trong ngân hàng: Part 1 và Part 2 mỗi Part 2 bài (mỗi bài 1 câu), Part 3 và Part 4 mỗi Part 1 bài (mỗi bài 3 câu), Part 5 có 1 bài (1 câu).

Hiện tại hệ thống chỉ bảo đảm một `ExerciseId` không lặp lại trong đề. Cơ chế này không phát hiện các trường hợp sau:

- 3 câu hỏi giống nhau trong cùng một bài Part 3 hoặc Part 4 (DUP-01).
- Hai bài khác mã nhưng cùng đoạn văn hoặc cùng câu hỏi (DUP-02).
- Cùng một ảnh được tải lên hai lần, nên có hai asset khác nhau (DUP-03).

Moodle cũng gặp đúng giới hạn này: một câu không xuất hiện hai lần, nhưng [hai bản sao của cùng câu vẫn lọt](https://moodle.org/mod/forum/discuss.php?d=251833). Ngành khảo thí còn có khái niệm [enemy items](https://assess.com/enemy-items/): hai câu không nên ở cùng một đề, kể cả khi không trùng chữ.

Chi tiết và bằng chứng nằm trong [báo cáo rà soát](../runbooks/speaking-audit-report.md).

## Decision

### 1. Định nghĩa "trùng" theo từng mức

| Mã | Mức | Điều kiện | Áp dụng khi |
| :--- | :--- | :--- | :--- |
| `DUPLICATE_EXERCISE` | Chặn | Cùng `ExerciseId` ở hai vị trí. | Đã có, giữ nguyên |
| `DUPLICATE_QUESTION_IN_EXERCISE` | Chặn | Hai câu của một bài Part 3 hoặc Part 4 giống nhau sau chuẩn hóa. | Lưu bài tập; kiểm tra lại khi công bố |
| `DUPLICATE_PROMPT_TEXT` | Chặn | Hai đơn vị văn bản trong đề giống nhau sau chuẩn hóa. | Công bố |
| `DUPLICATE_IMAGE` | Chặn | Hai bài Part 2 có cùng `ImageAssetId` hoặc cùng `sha256`. | Công bố |
| `SIMILAR_CONTENT` | Cảnh báo | Hai đơn vị văn bản có độ giống Jaccard theo từ ≥ 0,8. | Trước khi công bố |
| `SAME_TOPIC_IN_PART` | Cảnh báo | Hai bài cùng Part có `Topic` giống nhau sau chuẩn hóa. | Trước khi công bố |

Cảnh báo không chặn công bố. Admin vẫn có quyền quyết định vì hai đoạn thông báo có thể giống nhau về khuôn mà vẫn hợp lệ.

**Dùng lại một bài ở đề khác thì được phép.** Các lượt làm đã có snapshot riêng, và ngân hàng bài của dự án còn nhỏ. UI chỉ cần cho thấy bài đang được dùng ở bao nhiêu đề.

### 2. Đơn vị so sánh

| Part | Vị trí câu | Đơn vị |
| :--- | :--- | :--- |
| 1 | 1–2 | `passage` |
| 2 | 3–4 | Ảnh, so sánh bằng asset id và `sha256` |
| 3 | 5–7 | Từng phần tử trong `questions` |
| 4 | 8–10 | Từng phần tử trong `questions` |
| 5 | 11 | `prompt` |

Mọi đơn vị văn bản được so sánh với nhau, kể cả khác Part. Ví dụ một câu của Part 3 trùng với câu của Part 4 vẫn bị chặn. Đề có tối đa 11 đơn vị, nên so sánh từng cặp là đủ nhanh.

### 3. Quy tắc chuẩn hóa

Áp dụng theo đúng thứ tự:

1. Unicode NFKC.
2. Chuyển chữ thường bằng `ToLowerInvariant`.
3. Thay mọi ký tự không phải chữ hoặc số bằng khoảng trắng.
4. Gộp khoảng trắng liên tiếp và cắt hai đầu.

Như vậy `"What time is it?"` và `"what  time is it"` được coi là trùng.

### 4. Đặt logic ở đâu

- Tạo `Application/Speaking/Admin/SpeakingTestContentRules.cs`, chứa hàm thuần, không I/O: nhận các bài tập và dữ liệu ảnh, trả danh sách vấn đề.
- `SpeakingExerciseInputValidator`: thêm luật 3 câu hỏi khác nhau. Trả `400` với khóa `Questions`.
- `PublishSpeakingTestCommandHandler.Handle`: sau `PublishRequirements`, lấy hash ảnh rồi gọi hàm trên. Lỗi mức chặn trả `422` với khóa mới `Content`; `Exercises` và các khóa khác giữ nguyên. `PublishRequirements` giữ nguyên là hàm đồng bộ, vì việc đọc asset là I/O.
- Lấy `sha256` qua `FindMediaAssetAsync` có sẵn, tối đa 2 lần gọi cho Part 2. **Không cần đổi interface repository.**
- FE phải sửa UI-03 cùng lúc. `http.ts` hiện chỉ giữ lỗi đầu tiên của mỗi khóa, nên nếu không sửa, Admin chỉ thấy một cặp trùng mỗi lần công bố.
- **Không cần migration.** Dữ liệu cần so sánh đã có trong `content_json` và `speaking_media_assets.sha256`.

### 5. Triển khai theo hai bước

**Bước 1 (làm trước):** chỉ các mã mức chặn, kiểm tra khi lưu bài và khi công bố. Không có API mới. Lỗi hiện sau khi bấm công bố. Việc khóa bài đã chọn trong ô chọn FE tự làm được.

**Bước 2 (khi cần):** các mã mức cảnh báo, cùng endpoint `POST /api/v1/admin/speaking/tests/publish-check`. Endpoint nhận cùng body với `POST /tests`, không ghi dữ liệu và chấp nhận đề chưa đủ bài. Cảnh báo không chặn nên cần kênh riêng này. Hợp đồng nằm ở [admin-api.md](../features/speaking/admin-api.md#kiểm-tra-trước-khi-công-bố-đề-xuất).

Server giữ một nguồn logic duy nhất, nên FE không phải viết lại luật chuẩn hóa.

## Alternatives considered

### Unique index trên hash nội dung ở cấp ngân hàng

- Ưu điểm: database bảo đảm tuyệt đối.
- Nhược điểm: chặn cả trường hợp hợp lệ, cần migration, và không xử lý được trùng giữa các Part.
- Không chọn. Quy tắc này là ràng buộc theo từng đề, không phải theo toàn bộ ngân hàng.

### Chỉ kiểm tra ở FE

- Ưu điểm: nhanh, không đổi backend.
- Nhược điểm: gọi API trực tiếp vẫn lọt; dữ liệu có thể đổi giữa lúc kiểm tra và lúc công bố.
- Không chọn. Server phải là nơi quyết định cuối cùng, như các điều kiện công bố hiện có.

### Chặn luôn khi gần giống

- Ưu điểm: đề chặt chẽ hơn.
- Nhược điểm: ngưỡng chưa được hiệu chỉnh nên dễ chặn nhầm, và Admin không có cách vượt qua.
- Không chọn ở giai đoạn này.

### So sánh ảnh theo độ giống về hình (perceptual hash)

- Ưu điểm: phát hiện ảnh đã đổi cỡ hoặc nén lại.
- Nhược điểm: cần thêm thư viện xử lý ảnh, trong khi dự án không thêm package nếu không có yêu cầu.
- Để sau. `sha256` chỉ bắt được ảnh giống từng byte; đây là giới hạn đã chấp nhận.

### Danh sách enemy khai báo tay

- Ưu điểm: bắt được các cặp câu gợi ý đáp án cho nhau.
- Nhược điểm: tốn công nhập liệu, cần bảng mới.
- Để sau, khi ngân hàng đủ lớn.

## Consequences

- Prototype đã kiểm chứng bước 1: 1 file mới 61 dòng, chặn đúng các kịch bản trùng (xem [báo cáo mục 0](../runbooks/speaking-audit-report.md#0-kết-quả-kiểm-tra-lại-2026-11-03)).
- **Bộ mẫu test phải sửa cùng lúc.** `FullTestAsync` dùng chung một đoạn văn cho hai bài Part 1 và một ảnh cho hai bài Part 2, nên có 22 test đỏ. Thêm vào `SpeakingSamples` một đoạn văn thứ hai và một ảnh PNG khác byte, sửa `FullTestAsync`, thì còn 2 test (`Admin_and_learner_contract_matches_the_frontend`, `Published_images_are_served_only_through_a_published_snapshot`). Không được nới luật để test xanh.
- Bài `SAMPLE-` trên Neon có thể đang dùng chung ảnh hoặc nội dung. Đề `SAMPLE-FULL-01` không bị ảnh hưởng, nhưng tạo đề mới từ các bài đó có thể bị chặn.
- Đề đã công bố không bị ảnh hưởng vì dùng snapshot; không cần sửa dữ liệu cũ.
- Bài tập cũ có câu hỏi trùng sẽ nhận `400` ở lần sửa tiếp theo, và đề dùng bài đó sẽ không công bố được cho tới khi sửa. Lượt làm và luyện Part đang có không bị ảnh hưởng.
- Trước khi triển khai, cần chạy truy vấn chỉ đọc trong [UAT runbook](../runbooks/speaking-uat-runbook.md#5-rà-soát-dữ-liệu-hiện-có) để biết có bao nhiêu bài phải sửa.
- Giới hạn còn lại: diễn đạt khác nhau cùng ý nghĩa không bị phát hiện; ảnh đã chỉnh sửa không bị phát hiện; giữa lúc kiểm tra và lúc lưu vẫn có khoảng ngắn dữ liệu có thể đổi, mức rủi ro này được chấp nhận với số ít Admin.
- Phase sau có thể thêm cột `content_fingerprint` bằng migration mới để cảnh báo trùng trên toàn ngân hàng ngay khi tạo bài.
