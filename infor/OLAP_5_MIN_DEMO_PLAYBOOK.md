# OLAP Demo Playbook (5 phút)

Mục tiêu: demo nhanh, mượt, có câu chuyện phân tích rõ ràng bằng chuỗi thao tác `Filter -> Slice/Dice -> DrillDown -> Pivot -> RollUp`.

---

## 0:00 - 0:30 | Khởi động và chốt ngữ cảnh

**Mục tiêu nói khi demo**
- "Em sẽ đi từ bức tranh tổng quan đến chi tiết theo thời gian, sau đó đổi góc nhìn theo chiều dữ liệu và quay lại mức tổng hợp."

**Thao tác**
1. Ở `Bộ lọc dữ liệu`:
   - `Cube`: giữ `Cube4FactBanHang_3D_KH_MH_TG`
   - `Measure`: chọn `Tong Tien`
   - `Năm`: chọn `2020` (hoặc năm có dữ liệu dày nhất)
   - Bỏ trống `Mã Mặt Hàng`, `Mã Khách Hàng`, `Mã Cửa Hàng`
2. Bấm `Slice`.

**Kỳ vọng kết quả**
- Bảng/biểu đồ trả dữ liệu theo **cấp hiện tại (Năm hoặc Quý tùy trạng thái trước đó)**.
- Không có lỗi guard vì Slice đã có `Năm`.

---

## 0:30 - 1:30 | Đi từ tổng quan xuống chi tiết (DrillDown)

**Thao tác**
1. Bấm `Drill Down` lần 1 (Năm -> Quý).
2. Quan sát chart: nhận diện quý cao nhất/thấp nhất.
3. Bấm `Drill Down` lần 2 (Quý -> Tháng). Nếu có popup cảnh báo tải lớn, bấm `Tôi đã hiểu, tiếp tục`.

**Kỳ vọng kết quả**
- Level badge chuyển dần: `Năm -> Quý -> Tháng`.
- Biểu đồ có nhiều điểm hơn (tháng), thể hiện mùa vụ rõ hơn.

**Câu nói gợi ý**
- "Ở mức Quý thấy xu hướng, xuống Tháng thì thấy biến động chi tiết theo mùa."

---

## 1:30 - 2:30 | Khoanh vùng bằng Dice (nhiều chiều)

**Thao tác**
1. Trong `Bộ lọc dữ liệu`, chọn thêm:
   - `Mã Mặt Hàng`: chọn 1 mã (ví dụ phần tử đầu danh sách)
   - (Tùy chọn) `Mã Khách Hàng`: chọn thêm 1 khách hàng để bó hẹp mạnh hơn
2. Bấm `Dice`.

**Kỳ vọng kết quả**
- Dữ liệu giảm nhiễu, tập trung vào lát cắt cụ thể.
- Thời gian phản hồi nhanh hơn khi phạm vi đã hẹp.
- `Phép cuối` hiển thị `Dice`.

**Câu nói gợi ý**
- "Dice giúp kiểm tra giả thuyết rất nhanh trên một phân khúc thay vì toàn bộ tập dữ liệu."

---

## 2:30 - 3:30 | Đổi góc nhìn bằng Pivot

**Thao tác**
1. Giữ bộ lọc hiện tại.
2. Bấm `Pivot` 1 lần để đảo trục (thời gian <-> chiều phụ như mặt hàng/khách hàng/cửa hàng).
3. Nếu cần, bấm `Pivot` lần 2 để quay lại góc nhìn cũ.

**Kỳ vọng kết quả**
- Trục hàng/cột thay đổi, nhãn `Cột/Hàng` trong Action panel đổi theo.
- Dễ so sánh chéo giữa các thành viên dimension theo thời gian.

**Câu nói gợi ý**
- "Cùng một dữ liệu, chỉ cần đổi trục là câu trả lời kinh doanh có thể nhìn ra ngay."

---

## 3:30 - 4:30 | Tổng hợp lại bằng RollUp

**Thao tác**
1. Nếu đang ở `Tháng`, bấm `Roll Up` để lên `Quý`.
2. Bấm `Roll Up` lần nữa để lên `Năm`.

**Kỳ vọng kết quả**
- Giảm độ chi tiết, dễ chốt insight ở mức quản trị.
- Level badge quay về `Quý` rồi `Năm`.

**Câu nói gợi ý**
- "DrillDown để phát hiện nguyên nhân, RollUp để chốt quyết định ở mức tổng hợp."

---

## 4:30 - 5:00 | Kết thúc và chốt thông điệp

**Checklist chốt nhanh**
1. Trả bộ lọc về trạng thái chuẩn:
   - `Năm`: giữ năm demo
   - Xóa filter phụ (`Mã MH/KH/CH`) nếu muốn về toàn cảnh
2. Chọn lại `Measure` nếu cần minh họa thêm:
   - `Tong Tien` (giá trị doanh thu)
   - `So Luong Dat` (sản lượng)
3. Xuất nhanh `CSV` hoặc `Export Excel` nếu cần bàn giao kết quả.

**Thông điệp kết**
- "Với cùng một cube, bộ công cụ Filter + DrillDown/RollUp + Slice/Dice + Pivot cho phép đi từ toàn cảnh đến chi tiết và quay lại quyết định quản trị chỉ trong vài phút."

---

## Kịch bản dự phòng (khi gặp cảnh báo hoặc dữ liệu quá rộng)

- Nếu `Drill Down` bị khóa ở mức Quý:
  - Chọn `Năm` trước, rồi thử lại.
- Nếu `Dice/Pivot` cảnh báo truy vấn rộng:
  - Thêm ít nhất 1 filter phụ (`Mã MH` hoặc `Mã KH` hoặc `Mã CH`).
- Nếu biểu đồ quá nhiều series:
  - Giữ `Tong Tien` và thêm filter phụ để giảm số dòng/cột.

---

## Mẫu script nói 30 giây (đọc trực tiếp)

"Em bắt đầu bằng filter năm để cố định ngữ cảnh, sau đó slice để lấy baseline. Tiếp theo em drill down từ năm xuống quý, rồi xuống tháng để thấy biến động chi tiết. Khi cần khoanh vùng giả thuyết, em dùng dice theo sản phẩm/khách hàng. Sau đó pivot để đổi góc nhìn hàng-cột, giúp so sánh chéo nhanh hơn. Cuối cùng roll up để quay về mức tổng hợp, chốt insight quản trị."
