# Quy tắc vẽ biểu đồ OLAP

Tài liệu này là nguồn chuẩn để tham chiếu trước khi tạo/chỉnh biểu đồ cho mọi truy vấn OLAP.

## 1) Các loại biểu đồ và quy tắc sử dụng

### Biểu đồ cột (Bar chart)
- Dùng khi so sánh giá trị giữa các nhóm/danh mục rời rạc, xếp hạng các mục, hoặc theo dõi thay đổi qua ít mốc thời gian.
- Quy tắc:
  - Trục Y phải bắt đầu từ `0`.
  - Tối đa `10` cột.
  - Sắp xếp theo thứ tự logic.
  - Không dùng 3D hay đổ bóng.
  - Dùng màu đồng nhất; chỉ tô nổi bật cột cần nhấn mạnh.

### Biểu đồ cột ngang (Horizontal bar chart)
- Dùng khi tên danh mục dài, so sánh từ `10` mục trở lên, hoặc xếp hạng từ cao đến thấp.
- Quy tắc:
  - Sắp xếp từ lớn đến nhỏ (trừ khi thứ tự cố định).
  - Trục X bắt đầu từ `0`.
  - Nhãn đặt bên trái cho dễ đọc.
  - Không dùng khi dữ liệu có thứ tự thời gian.

### Biểu đồ đường (Line chart)
- Dùng cho dữ liệu liên tục theo thời gian, hiển thị xu hướng tăng/giảm, hoặc so sánh nhiều chuỗi song song.
- Quy tắc:
  - Chỉ dùng khi trục X là thời gian liên tục.
  - Tối đa `4–5` đường.
  - Không ngắt trục Y tùy tiện.
  - Thêm điểm đánh dấu nếu dữ liệu ít mốc.
  - Không dùng cho dữ liệu danh mục rời rạc.

### Biểu đồ vùng (Area chart)
- Dùng khi muốn nhấn mạnh tổng lượng tích lũy theo thời gian, hoặc thể hiện phần đóng góp thành phần vào tổng thể qua thời gian (stacked area).
- Quy tắc:
  - Màu có độ trong suốt `30–50%`.
  - Stacked area không quá `4` lớp.
  - Trục Y bắt đầu từ `0`.

### Biểu đồ tròn (Pie chart)
- Dùng khi hiển thị tỷ lệ phần trăm của một tổng thể với `2–5` phần tử.
- Quy tắc:
  - Tối đa `5` lát.
  - Nhóm phần nhỏ vào `"Khác"`.
  - Luôn hiển thị nhãn `%`.
  - Bắt đầu từ vị trí `12 giờ`, theo chiều kim đồng hồ.
  - Không dùng 3D.
  - Không dùng khi cần so sánh chính xác giữa các phần.

### Biểu đồ vành khuyên (Donut chart)
- Tương tự pie chart nhưng có khoảng trống ở giữa để đặt chỉ số tổng hoặc tên chỉ số.
- Phù hợp cho dashboard.
- Quy tắc tương tự pie; không dùng chỉ để “cho đẹp hơn”.

### Biểu đồ phân tán (Scatter plot)
- Dùng khi tìm mối tương quan giữa 2 biến liên tục, hoặc phát hiện cụm và ngoại lệ.
- Quy tắc:
  - Thêm đường xu hướng nếu có tương quan rõ.
  - Dùng màu/hình dạng để phân biệt nhóm thứ 3.
  - Không nối điểm bằng đường.
  - Không dùng cho dữ liệu danh mục hoặc khi có dưới `10` điểm.

### Biểu đồ bong bóng (Bubble chart)
- Dùng khi so sánh 3 biến cùng lúc: vị trí X, vị trí Y, và kích thước bong bóng.
- Quy tắc:
  - Kích thước tỷ lệ theo **diện tích** (không phải bán kính).
  - Ghi rõ biến đại diện kích thước.
  - Tối đa `20` bong bóng.

### Biểu đồ histogram
- Dùng để hiển thị phân phối của một biến liên tục, xem dữ liệu lệch hay chuẩn.
- Quy tắc:
  - Cột liền kề, không có khoảng trắng.
  - Chọn `5–20` khoảng (bin).
  - Trục Y là tần suất.
  - Không dùng cho dữ liệu danh mục hoặc dưới `30` điểm.

### Biểu đồ hộp (Box plot)
- Dùng khi so sánh phân phối nhiều nhóm hoặc phát hiện ngoại lệ.
- Quy tắc:
  - Hiển thị đủ `min, Q1, median, Q3, max`.
  - Đánh dấu ngoại lệ riêng.
  - Không dùng khi dưới `10` điểm.

### Biểu đồ nhiệt (Heatmap)
- Dùng để hiển thị mức độ theo 2 chiều danh mục, ma trận tương quan, hoặc lịch hoạt động.
- Quy tắc:
  - Dùng thang màu tuần tự hoặc phân kỳ.
  - Thêm nhãn giá trị nếu ma trận nhỏ.
  - Màu phải thân thiện với người mù màu.

### Biểu đồ thác nước (Waterfall chart)
- Dùng để phân tích tăng/giảm từng bước để đạt giá trị cuối (P&L, dòng tiền).
- Quy tắc:
  - Màu khác nhau cho tăng, giảm, tổng.
  - Hiển thị giá trị trên mỗi cột.
  - Luôn có cột tổng cộng cuối.
  - Không dùng khi có hơn `10` bước.

## 2) Quy tắc gộp biểu đồ (Combined / Dual-axis charts)

Gộp biểu đồ là đặt từ hai loại biểu đồ trở lên trên cùng vùng vẽ, thường chung trục X, có thể có hai trục Y.

### Khi nào nên gộp biểu đồ
- Hai tập dữ liệu chia sẻ cùng trục thời gian hoặc danh mục.
- Mối quan hệ giữa chúng là thông điệp chính (tương quan/nhân quả).
- Hai chỉ số có đơn vị khác nhau cần hai trục Y.
- Không có cách rõ hơn bằng một biểu đồ đơn lẻ.

Ví dụ: cột doanh thu theo tháng + đường tỷ lệ tăng trưởng %.

### Khi nào không được gộp biểu đồ
- Hai tập dữ liệu không có liên hệ thực sự (tránh tương quan giả).
- Hai chuỗi cùng đơn vị, cùng thang đo (chung một trục Y là đủ).
- Có hơn `2` trục Y.
- Hai chuỗi không cùng nhịp thời gian/danh mục trên trục X.
- Chỉ gộp để “đẹp” hoặc tiết kiệm không gian.

### Quy tắc kỹ thuật khi gộp
- Ghi chú rõ trục Y nào thuộc dữ liệu nào.
- Màu trục Y phải khớp màu chuỗi dữ liệu tương ứng.
- Hai trục Y phải có điểm `0` cùng vị trí ngang.
- Ưu tiên hai loại biểu đồ khác nhau (ví dụ cột + đường).
- Legend phải ghi rõ tên + đơn vị từng chuỗi.

## 3) Quy tắc kết hợp biểu đồ (chi tiết)

### 3.1 Nguyên tắc nền tảng
- Trước khi gộp: nếu tách thành 2 biểu đồ giúp hiểu nhanh hơn thì **không gộp**.
- Chỉ gộp khi mối quan hệ giữa 2 tập dữ liệu là thông điệp chính.

### 3.2 Các kiểu kết hợp phổ biến

#### Cột + Đường (Bar + Line)
- Dùng cột cho giá trị tuyệt đối, đường cho tỷ lệ/chỉ số phái sinh.
- Đây là kiểu an toàn và dễ đọc nhất.

#### Cột chồng + Đường (Stacked bar + Line)
- Dùng khi cần cả cấu trúc đóng góp và chỉ số tổng thể.
- Đường phải là chỉ số độc lập, không phải tổng của cột chồng.

#### Đường + Đường trên hai trục Y
- Chỉ dùng khi đơn vị hoàn toàn khác nhau.
- Bắt buộc:
  - Hai đường khác màu rõ ràng.
  - Hai trục Y tô màu khớp với đường.
  - Điểm `0` hai trục cùng một hàng ngang.

#### Vùng + Đường (Area + Line)
- Dùng khi vùng thể hiện tổng tích lũy, đường là ngưỡng tham chiếu/mục tiêu.
- Vùng phải nhạt, trong suốt để đường nổi bật.

#### Scatter + Trendline
- Dạng kết hợp tự nhiên.
- Nên ghi rõ phương trình hoặc hệ số `R²` cho đối tượng phân tích dữ liệu.

### 3.3 Quy tắc về trục
- Hai trục Y là điểm rủi ro cao nhất khi kết hợp.
- Điểm `0` của hai trục phải thẳng hàng.
- Tỷ lệ hai trục cần tránh tạo ảo giác tương quan hoàn hảo.
- Trục trái cho dữ liệu chính; trục phải cho dữ liệu phụ.
- Không bao giờ có `3` trục Y.

### 3.4 Quy tắc về màu sắc và phân biệt thị giác
- Mỗi tập dữ liệu phải khác nhau bởi ít nhất 2 thuộc tính thị giác (không chỉ màu).
- Ví dụ: cột + đường; hoặc đường liền + đường đứt.
- Màu trục Y khớp với màu chuỗi dữ liệu.
- Legend phải có: tên, loại biểu đồ, đơn vị.

### 3.5 Quy tắc về dữ liệu
- Hai chuỗi phải có cùng độ phân giải thời gian/danh mục trên trục X.
- Không gộp tháng với quý nếu không quy đổi rõ ràng.
- Dữ liệu thiếu phải xử lý rõ ràng (nội suy/null), không để trống ngầm.

### 3.6 Quy tắc về ngữ cảnh và truyền thông
- Tiêu đề nên phản ánh mối quan hệ, không chỉ liệt kê tên chuỗi.
- Nếu có nhân quả, sắp xếp theo chiều nhân quả hoặc ghi chú rõ.
- Báo cáo kỹ thuật có thể trung lập; thuyết trình nên có thông điệp rõ.

### 3.7 Checklist trước khi gộp
- Hai chuỗi có cùng trục X không?
- Có thực sự cần hai trục Y không?
- Điểm `0` của hai trục có thẳng hàng không?
- Màu sắc/hình dạng có đủ phân biệt không?
- Legend có đủ tên và đơn vị chưa?
- Tiêu đề có truyền tải mối quan hệ không?
- Tách ra hai biểu đồ riêng có rõ hơn không?
  - Nếu có: **tách ra**.

## 4) Cam kết áp dụng trong dự án

- Trước khi tạo/chỉnh biểu đồ OLAP, luôn đối chiếu tài liệu này.
- Nếu yêu cầu người dùng mâu thuẫn quy tắc, cần phản hồi rõ ràng và đề xuất phương án an toàn hơn.
- Ưu tiên tính đúng đắn dữ liệu và khả năng đọc hiểu hơn hiệu ứng trang trí.
