# Tổng quan chức năng FoMed API

Tài liệu này liệt kê các chức năng hiện có trong hệ thống theo yêu cầu nghiệp vụ đã nêu, đồng thời ghi nhận những yêu cầu chưa triển khai và các chức năng bổ sung ngoài yêu cầu.

## 1. Quy trình nghiệp vụ chính

1. **Tiếp nhận & đặt lịch khám**
   - Bệnh nhân đăng ký/đăng nhập qua `AuthenticationController` rồi đặt lịch online thông qua `AppointmentsController` (lưu ngày, giờ, bác sĩ, trạng thái `booked`/`waiting`/`done`).
   - Lễ tân có thể tạo lịch trực tiếp và theo dõi danh sách chờ thông qua cùng controller, đồng thời `AppointmentCleanupService` chạy nền tự động huỷ các ca `waiting` đã quá thời điểm hẹn.
2. **Khám bệnh**
   - Bác sĩ tương tác với `DoctorWorkSpaceController` để xem bệnh nhân, bắt đầu encounter, ghi triệu chứng/chẩn đoán và lưu ghi chú.
   - Có khả năng quản lý trạng thái encounter, lưu thông tin bác sĩ/bệnh nhân kèm `Encounter` record.
3. **Xét nghiệm**
   - Bác sĩ chỉ định dịch vụ xét nghiệm trong `DoctorWorkSpaceController` thông qua các API `lab-orders`, tạo `LabOrder`/`LabOrderItems`.
   - Kỹ thuật viên cập nhật kết quả chưa thấy trong controller này (có thể nằm ở module khác chưa khám phá), tuy nhiên dữ liệu đơn hàng xét nghiệm được lưu lại để bác sĩ xem.
4. **Toa thuốc & đơn thuốc**
   - Kê toa bằng `DoctorWorkSpaceController` (danh sách thuốc từ `Medicines`, lưu `EncounterPrescription` cùng dòng thuốc `PrescriptionItems`).
   - Quản lý kho thuốc được tính đơn giản bằng `Medicine.Lots` trong `DoctorWorkSpaceController`, hiển thị tồn kho khả dụng.
   - Bệnh nhân có thể xem đơn thuốc (chưa rõ API front-end, nhưng dữ liệu luôn liên kết `EncounterPrescription`).
5. **Thanh toán**
   - `DoctorWorkSpaceController` hoàn tất khám tạo hoá đơn (`Invoice`, `InvoiceItem`) gồm dịch vụ, xét nghiệm, thuốc.
   - `BillingController` điều phối danh sách hoá đơn, chi tiết, trạng thái và ghi nhận thanh toán đồng bộ với appointment/encounter (`FinalCost`, `Status`).
   - Hệ thống tính tổng chi phí và lưu `Subtotal`/`TotalAmount`; billing có thể in/tra cứu lịch sử thanh toán qua endpoint `invoices`.

## 2. Chức năng bắt buộc theo vai trò

- **Bệnh nhân**: Đăng ký/đăng nhập (`AuthenticationController`), đặt lịch (`AppointmentsController`), xem lịch sử khám/đơn thuốc/kết quả (dữ liệu liên kết `Appointments`, `EncounterPrescription`, `LabOrder`).
- **Lễ tân**: Tạo lịch trực tiếp (`AppointmentsController`), quản lý chờ khám (trạng thái `waiting` + `AppointmentCleanupService`), quản lý lịch làm việc bác sĩ (qua `DoctorWorkSpaceController` và bảng `Doctor`/`WorkSchedule`).
- **Bác sĩ**: Xem danh sách bệnh nhân hôm nay (`DoctorWorkSpaceController` + `TodayPatients` feature), ghi nhận chẩn đoán, chỉ định xét nghiệm, kê toa thuốc, hoàn tất khám với tạo hoá đơn.
- **Admin**: Quản lý người dùng/dịch vụ/thuốc thông qua các controller liên quan (`UserManagerController`, `ServicesManagerController`, `MedicineController`), xem thống kê lượt khám/doanh thu (`DashboardController`).

## 3. Chức năng nâng cao hiện có

- **Báo cáo & dashboard**: `DashboardController` cung cấp thống kê lượt khám, bác sĩ, bệnh nhân, doanh thu theo tháng và tiến độ mục tiêu (filter theo bác sĩ/dịch vụ).
- **Quản lý hoá đơn chi tiết**: `BillingController` cho phép truy vấn ca chờ, danh sách hoá đơn với lọc trạng thái/keyword, chi tiết hoá đơn đầy đủ thông tin bệnh nhân/bác sĩ, vận hành thanh toán và cập nhật trạng thái.
- **Đồng bộ trạng thái**: Khi thanh toán hoàn tất, `BillingController` cập nhật `Appointment.Status` thành `done` và `FinalCost`, đảm bảo dữ liệu báo cáo đúng.
- **Appointment cleanup**: Service nền tự huỷ các cuộc hẹn vắng mặt để giữ danh sách chờ sạch, hỗ trợ lễ tân/bác sĩ tập trung các ca thực tế.

## 4. Yêu cầu chưa triển khai (hoặc cần xác nhận thêm)

- Gửi SMS/email nhắc lịch hẹn (chưa thấy service gửi thông báo).
- Chat nội bộ giữa lễ tân – bác sĩ (không thấy module messaging).
- Quản lý nhiều chi nhánh (bảng `Clinic` hay `Branch` không rõ tồn tại).
- Tạo QR code để bệnh nhân tra cứu hồ sơ (chưa thấy endpoint tạo/scan QR).
- Báo cáo doanh thu theo ngày/tháng/năm tách biệt (dashboard chỉ có tháng và tổng, chưa có báo cáo theo ngày hoặc theo nhiều chu kỳ).
- Ghi nhận kết quả xét nghiệm từ kỹ thuật viên (chỉ có phần tạo xét nghiệm, chưa thấy phần nhập kết quả).

## 5. Tổng kết

Hệ thống đã phủ hầu hết luồng khám tiền năng, với các API rõ ràng cho bệnh nhân, lễ tân, bác sĩ và admin. Cần bổ sung phần thông báo, chat, đa chi nhánh, QR code, và luồng nhập kết quả xét nghiệm để đáp ứng toàn diện yêu cầu nâng cao. Các chức năng phụ như dashboard, billing chi tiết, và appointment cleanup là phần mở rộng bổ sung ngoài yêu cầu cơ bản nhưng mang lại giá trị vận hành.
