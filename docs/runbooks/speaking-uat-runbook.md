# Speaking UAT và checklist bàn giao

Tài liệu này chốt phạm vi **Speaking**: 11 câu, 5 Part, đề đầy đủ và luyện theo Part. Writing và L&R không thuộc checklist này.

Các dòng ghi **(Đích)** chỉ pass được sau khi triển khai ADR-002, ADR-003 hoặc [Test Builder](../features/speaking/admin-test-builder.md). Khi chạy trên phiên bản hiện tại, các dòng đó dự kiến **fail** và là bằng chứng cho [báo cáo rà soát](./speaking-audit-report.md).

## 1. Chuẩn bị an toàn

1. Tạo `.env` từ `.env.example`. Không đưa key vào source, ảnh chụp màn hình hay ticket.
2. Có PostgreSQL, Redis và MinIO hoạt động. Chỉ chạy migration lên database dùng chung khi nhóm đã duyệt.
3. Để chấm AI: bật Azure Speech và Gemini theo [AI Evaluation](../features/speaking/ai-evaluation.md), và image có `ffmpeg`.
4. Dùng HTTPS hoặc `localhost`. Trang HTTP thường không dùng được micro.
5. Có tài khoản Admin. Không bypass bằng header hay query string.
6. Khi thử Azure hoặc Gemini, chỉ dùng bản ghi không chứa thông tin cá nhân.
7. Có ít nhất 2 bài cho mỗi Part 1, 2 và 1 bài cho mỗi Part 3, 4, 5. Dữ liệu mẫu có tiền tố `SAMPLE-`. Ảnh của bài mẫu Part 2 có thể không có trong MinIO máy khác, khi đó cần tải lại ảnh.

## 2. Checklist Học viên

| Luồng | Kết quả cần có |
|---|---|
| Thư viện | Chỉ có đề `PUBLISHED`. Thẻ Part dẫn đến danh sách bài. |
| Đề đầy đủ | Đúng 11 câu theo 2/2/3/3/1. |
| Theo Part | Chỉ có câu của Part đó, đánh số lại từ 1. |
| Đồng hồ | Đúng thời gian trong `spec.md`. Tải lại không làm tăng thời gian. |
| Part 4 | 45 giây đọc, câu hỏi chưa lộ. Sau đó từng câu mới hiện. |
| Vắng lâu | Đóng tab rồi mở lại sau khi hết giờ: bài đã được chuyển pha hoặc nộp. |
| Ghi âm | Bản ghi tải lên sau mỗi câu. Có thể nghe lại khi xem bài. |
| Tải lỗi | Tắt mạng khi tải: có trạng thái lỗi và nút thử lại. |
| Consent | Nộp bài không tự gửi đến AI, chỉ gửi sau khi đồng ý. |
| AI | Đang xử lý thì cập nhật mỗi 5 giây. Rời trang rồi quay lại vẫn đúng. Lỗi không làm mất bài và có nút chấm lại. |
| Kết quả | Có nhãn điểm ước tính 0–200, có chi tiết từng câu. |
| Quyền sở hữu | Khách chỉ thấy lượt của phiên mình. Tài khoản chỉ thấy lượt của mình. Người khác nhận `404`. |
| **(Đích)** Beep | Có tiếng beep trước khi ghi. Tiếng beep không nằm trong bản ghi. |
| **(Đích)** Kiểm tra thiết bị | Đủ 3 bước. Micro tắt tiếng không qua được bước nói thử. |
| **(Đích)** Bỏ qua pha | Đề `FULL` không có nút bỏ qua pha đọc và chuẩn bị. |

## 3. Ma trận quyền micro

Chạy cả ma trận trên HTTPS, **dùng cửa sổ riêng tư hoặc đã xóa quyền của site**. Điền số hộp thoại thực tế vào bảng. Đây là cách duy nhất để xác nhận hành vi từng trình duyệt, vì các nguồn công khai về Safari đã cũ.

| Kịch bản | Chrome: Allow | Chrome: This time | Edge | Firefox | Safari macOS | Safari iOS |
|---|---|---|---|---|---|---|
| A. Cấp quyền, bắt đầu, làm hết 11 câu | | | | | | |
| B. Như A, tải lại ở câu 3 | | | | | | |
| C. Mở lại bài đã nộp | | | | | | |
| D. Từ chối quyền, thử bắt đầu `FULL` | | | | | | |
| E. Từ chối quyền, luyện Part | | | | | | |
| F. Không có micro | | | | | | |
| G. Micro đang bị ứng dụng khác chiếm | | | | | | |
| H. Rút micro ở câu 5 | | | | | | |

**Kết quả cần có sau khi triển khai ADR-003 (Đích):**

| Kịch bản | Kết quả |
|---|---|
| A | Đúng **1** hộp thoại, trước khi lượt làm được tạo. `getUserMedia` được gọi 1 lần. |
| B | Không có hộp thoại tự xuất hiện. Nếu trình duyệt đã quên quyền: thông báo đồng hồ vẫn chạy, kèm nút bật lại. |
| C | **0** hộp thoại. Không có biểu tượng micro trên tab. |
| D | Nút bắt đầu bị khóa. Không có `POST /attempts`. Hướng dẫn mở cài đặt site. |
| E | Có nút "Luyện không ghi âm". |
| F | Thông báo không tìm thấy micro, không nhắc đến cấp quyền. |
| G | Thông báo micro đang bận. |
| H | Cảnh báo ngay. Đồng hồ không dừng. Câu thiếu bản ghi được báo khi xem bài. |

Cách đếm số lần gọi: dán đoạn này vào Console trước khi bắt đầu, rồi đọc `window.__gum` khi xong.

```js
window.__gum = 0;
const original = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
navigator.mediaDevices.getUserMedia = (constraints) => {
  window.__gum += 1;
  console.log("getUserMedia", window.__gum, location.pathname);
  return original(constraints);
};
```

Đoạn này mất tác dụng sau khi tải lại trang, nên kịch bản B phải dán lại.

Điền thêm vào ma trận: phiên bản trình duyệt và hệ điều hành. Đừng coi kết quả trên một phiên bản là đúng cho mọi phiên bản.

## 4. Checklist Admin/API

| Luồng | Kết quả cần có |
|---|---|
| Ngân hàng bài | CRUD đủ 5 Part. Lỗi hiện đúng trường. Ảnh có kiểm tra định dạng và dung lượng. |
| Đề nháp | Lưu được khi thiếu bài. Học viên không thấy đề nháp. |
| Công bố | Chỉ qua khi đủ 2/2/1/1/1 và mọi bài `PUBLISHED`. |
| Snapshot | Sửa bài sau khi công bố không đổi đề và lượt làm đã có. |
| Đồng thời | Hai tab cùng sửa: tab sau nhận `409`. |
| Trùng ID | Cùng một bài ở hai vị trí bị từ chối. |
| Bảo mật | Không có quyền Admin thì không gọi được. API học viên không trả rubric hay bài mẫu khi đang làm bài. |
| **(Đích)** Trùng trong bài | Part 3 có 2 câu giống nhau, chỉ khác hoa thường: `400`. |
| **(Đích)** Trùng giữa bài | 2 bài Part 1 khác mã, cùng đoạn văn: `422`, báo Câu 1 và Câu 2. |
| **(Đích)** Trùng giữa Part | Câu Part 3 trùng câu Part 4: `422`. |
| **(Đích)** Trùng ảnh | Tải cùng ảnh hai lần cho 2 bài Part 2: `422`. |
| **(Đích)** Gần giống | Chỉ cảnh báo, vẫn công bố được sau khi xác nhận. |
| **(Đích)** Bộ chọn | Tìm được bài ngoài 100 bài mới nhất. Bài đã dùng bị khóa và ghi rõ vị trí. |
| **(Đích)** Vị trí | Chọn Bài 2, để trống Bài 1, lưu rồi mở lại: vẫn đúng vị trí. |
| **(Đích)** Không mất dữ liệu | Đóng khi có thay đổi: có hỏi xác nhận. Lưu nháp không đóng builder. |
| **(Đích)** Nhân bản | Đề công bố nhân bản được thành nháp. Đề gốc không đổi. |

## 5. Rà soát dữ liệu hiện có

Chạy trước khi bật ADR-002 để biết bài nào cần sửa. Các truy vấn **chỉ đọc**, nhưng vẫn cần được nhóm đồng ý nếu chạy trên database dùng chung. Chuẩn hóa ở đây là gần đúng so với quy tắc trong ADR.

```sql
-- Bài Part 3, 4 có câu hỏi trùng nhau
SELECT e.code, e.part, q.normalized, count(*) AS occurrences
FROM speaking_exercises e
CROSS JOIN LATERAL (
  SELECT trim(regexp_replace(lower(value), '[^[:alnum:]]+', ' ', 'g')) AS normalized
  FROM jsonb_array_elements_text(e.content_json -> 'questions')
) q
WHERE e.part IN (3, 4)
GROUP BY e.code, e.part, q.normalized
HAVING count(*) > 1;

-- Các bài khác nhau có cùng đoạn văn hoặc đề bài
SELECT e.part, array_agg(e.code) AS codes
FROM speaking_exercises e
WHERE e.part IN (1, 5) AND e.status <> 'ARCHIVED'
GROUP BY e.part, trim(regexp_replace(
  lower(coalesce(e.content_json ->> 'passage', e.content_json ->> 'prompt')),
  '[^[:alnum:]]+', ' ', 'g'))
HAVING count(*) > 1;

-- Các ảnh giống nhau đang được bài Part 2 sử dụng
SELECT a.sha256, array_agg(e.code) AS codes
FROM speaking_exercises e
JOIN speaking_media_assets a ON a.id = e.image_asset_id
WHERE e.status <> 'ARCHIVED'
GROUP BY a.sha256
HAVING count(*) > 1;
```

Tên khóa `questions`, `passage`, `prompt` theo dạng camelCase của `SpeakingJson`. Các truy vấn này **chưa được chạy** trên PostgreSQL, nên hãy thử trên database cục bộ trước.

## 6. Quyết định go/no-go

Chỉ coi là xong khi: mọi dòng không ghi (Đích) đều pass; các dòng (Đích) đã triển khai đều pass; ma trận micro đã điền cho ít nhất Chrome, Edge và một trình duyệt Safari; và một bài thử không có thông tin cá nhân trả điểm hợp lệ từ Azure và Gemini.

Các giới hạn đã chấp nhận:

- Không thể cam kết không bao giờ hỏi lại quyền sau khi tải trang, vì quyền thuộc về trình duyệt.
- Biểu tượng micro của trình duyệt sáng suốt lượt làm, kể cả trong pha chuẩn bị. Không có âm thanh nào được ghi ngoài pha trả lời.
- Điểm AI không phải điểm ETS. Trọng số chưa được hiệu chỉnh.
- Chỉ phát hiện trùng chính xác và gần giống theo từ. Không phát hiện cùng ý khác lời hoặc ảnh đã chỉnh sửa.
