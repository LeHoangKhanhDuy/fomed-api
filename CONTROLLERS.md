# Tài liệu nhanh các API theo Controllers

Tài liệu này mô tả ngắn gọn chức năng hệ thống theo từng Controller (route, phương thức, quyền truy cập, mô tả ngắn và tham số chính). Mục tiêu là giúp hiểu nhanh API để dùng hoặc tích hợp.

**Ghi chú chung**

- **Base URL**: tất cả route được ghi như trong controller (ví dụ `api/v1/...`).
- **Quyền**: nếu controller/endpoint có attribute `[Authorize(Roles = "...")]`, tôi ghi tắt role yêu cầu.

**BillingController**

- **Base route**: `api/v1/admin/billing`
- **Roles**: `ADMIN, EMPLOYEE`
- **Endpoints**:
  - `GET /pending`: Danh sách hoá đơn chờ thanh toán (Status != 'paid' && != 'cancelled').
  - `GET /invoices`: Liệt kê hoá đơn (query: `page`, `limit`, `keyword`, `status`).
  - `GET /invoices/{invoiceId}`: Chi tiết hoá đơn theo `invoiceId` (include items, payments).
  - `POST /pay`: Ghi nhận thanh toán (body: `InvoiceId`, `Amount`, `Method`, `RefNumber?`, `Note?`).

**AuthenticationController**

- **Base route**: `api/v1/` (endpoints public)
- **Roles**: không (AllowAnonymous cho token dev)
- **Endpoints**:
  - `POST /access-token`: Tạo developer access token (dùng cho ADMIN; body: `Email`, `Password`).

**AppointmentsController**

- **Base route**: `api/v1/appointments`
- **Roles**: nhiều endpoint khác nhau (PATIENT, EMPLOYEE, ADMIN, DOCTOR)
- **Endpoints**:
  - `POST /create`: Tạo lịch khám (body: `CreateAppointmentRequest`). Roles: `PATIENT,EMPLOYEE,ADMIN`.
  - `GET /` : Lấy danh sách lịch theo ngày/bác sĩ (query: `date`, `doctorId`, `q`, `page`, `limit`). Roles: `DOCTOR,EMPLOYEE,ADMIN`.
  - `GET /patient-schedule`: Lấy lịch của bệnh nhân đăng nhập (PATIENT).

**AccountsController** (file `AccountsController.cs`)

- **Base route**: `api/v1/accounts`
- **Roles**: đa phần public / token-based
- **Endpoints chính**:
  - `POST /login-with-email`: Đăng nhập, trả JWT + refresh token (body: `LoginWithEmailRequest`).
  - `POST /refresh`: Refresh token (body: `refreshToken`).
  - `POST /register-with-email`: Đăng ký (body: `RegisterRequest`).
  - `POST /logout`: Thu hồi refresh token (body: `LogoutRequest`).
  - `POST /profile`: Lấy profile bằng token (body: `ProfileByTokenRequest` or header `Authorization: Bearer ...`).
  - `POST /update-profile`: Cập nhật profile (body gồm `Token` + các field).
  - `POST /avatar`: Upload avatar (multipart/form-data), cần auth.
  - `POST /forgot-password` & `POST /reset-password`: Quên/đặt lại mật khẩu bằng token.

**CategoryManagerController (AdminServiceCategoriesController)**

- **Base route**: `api/v1/admin/` (endpoints dùng prefix `categories`)
- **Roles**: `ADMIN`
- **Endpoints**:
  - `GET /categories`: Lấy danh sách danh mục (query: `keyword`, `isActive`).
  - `GET /categories/details/{id}`: Chi tiết.
  - `POST /categories/add`: Tạo danh mục (body: `ServiceCategoryCreateRequest`).
  - `PUT /categories/update/{id}`: Cập nhật.
  - `PATCH /categories/status/{id}`: Bật/Tắt.
  - `DELETE /categories/remove/{id}`: Xoá (chỉ khi không còn dịch vụ liên quan).

**DashboardController**

- **Base route**: `api/v1/dashboard/`
- **Roles**: `ADMIN,EMPLOYEE,DOCTOR`
- **Endpoints**:
  - `GET /visits`: Thống kê lượt khám (params: `from`, `to`, `doctorId`, `serviceId`).
  - `GET /doctors`: Thống kê bác sĩ.
  - `GET /patients`: Thống kê bệnh nhân.
  - `GET /monthly-sales`: Doanh thu theo tháng (params: `year`, `doctorId`, `serviceId`).
  - `GET /monthly-target`: So sánh với mục tiêu doanh thu.

**DoctorsController**

- **Base route**: `api/v1/doctors`
- **Roles**: nhiều endpoint public, admin-only cho phần quản trị
- **Endpoints**:
  - `GET /` (AllowAnonymous): Danh sách bác sĩ công khai.
  - `GET /details/{id}`: Chi tiết bác sĩ.
  - `GET /ratings/{id}`: Danh sách đánh giá theo bác sĩ.
  - `GET /admin/available-users`: (ADMIN) User có role DOCTOR nhưng chưa có hồ sơ.
  - `GET /admin/list`: (ADMIN) Danh sách bác sĩ đầy đủ.
  - `POST /admin/create`: (ADMIN) Tạo hồ sơ bác sĩ.
  - `PUT /admin/{id}`: (ADMIN) Cập nhật hồ sơ.
  - `POST /admin/{id}/upload-avatar`: (ADMIN) Upload ảnh override.
  - `DELETE /admin/{id}`: (ADMIN) Vô hiệu hoá.
  - `PATCH /admin/{id}/activate`: (ADMIN) Kích hoạt.
  - Schedule endpoints (`POST /schedule/{doctorId}`, `GET /schedule-week/{doctorId}`, `GET /calendar`, `PUT /schedule-update/{slotId}`, `DELETE /shedule-delete/{slotId}`) để quản lý lịch làm việc; roles: `ADMIN,DOCTOR,EMPLOYEE` (với ràng buộc DOCTOR chỉ thao tác chính mình).

**LabResultController (MyLabResultsController)**

- **Base route**: `api/v1/lab-results`
- **Roles**: `[Authorize]` (PATIENT xem của mình; ADMIN/DOCTOR có thể chỉ định patientId/patientCode)
- **Endpoints**:
  - `GET /` : Liệt kê các lab orders / kết quả theo patient (query: `patientId`, `patientCode`, `page`, `limit`).

**ServicesManagerController (ServiceCateController)**

- **Base route**: `api/v1/services`
- **Roles**: public list / admin for create/update/delete in code
- **Endpoints**:
  - `GET /`: Danh sách dịch vụ (query: `page`, `pageSize`).
  - `GET /details/{id}`: Chi tiết dịch vụ.
  - `POST /add`: Tạo dịch vụ.
  - `PUT /update/{id}`: Cập nhật dịch vụ.
  - `PATCH /status/{id}`: Bật/Tắt.
  - `DELETE /remove/{id}`: Xóa/soft-delete (nếu đang được sử dụng chuyển sang inactive).

**SpecialtiesController**

- **Base route**: `api/v1/specialties`
- **Roles**: public for main list; ADMIN for create/update/delete
- **Endpoints**:
  - `GET /`: Lấy danh sách chuyên khoa (AllowAnonymous).
  - `GET /{id}`: Chi tiết.
  - `POST /admin/create`: (ADMIN) Tạo mới.
  - `PUT /admin/{id}`: (ADMIN) Cập nhật.
  - `DELETE /admin/{id}`: (ADMIN) Vô hiệu hoá (soft-delete).
  - `PATCH /admin/{id}/activate`: (ADMIN) Kích hoạt lại.
  - `GET /admin/list`: (ADMIN) Danh sách cho admin.

**PrescriptionController**

- **Base route**: `api/v1/prescriptions`
- **Roles**: `[Authorize]` (PATIENT xem của mình; ADMIN/DOCTOR có thể xem bệnh nhân khác)
- **Endpoints**:
  - `GET /`: Danh sách đơn thuốc theo patient (query: `patientId`, `patientCode`, `page`, `limit`).
  - `GET /details/{idOrCode}`: Chi tiết đơn thuốc theo id hoặc code.

**PatientsController**

- **Base route**: `api/v1/admin/patients` (admin/employee area) và có 1 route public cho user
- **Roles**: `EMPLOYEE,ADMIN` cho hầu hết; `PATIENT` cho route `/api/v1/user/my-patient-id`
- **Endpoints**:
  - `GET /`: Lấy danh sách bệnh nhân (admin/employee) (query: `query`, `isActive`, `page`, `limit`).
  - `GET /{id}`: Chi tiết bệnh nhân.
  - `GET /by-phone`: Tìm theo số điện thoại.
  - `POST /create`: Tạo bệnh nhân.
  - `POST /upsert-by-phone`: Tạo hoặc trả về nếu SĐT tồn tại.
  - `PUT /update/{id}`: Cập nhật.
  - `PATCH /status/{id}`: Bật/Tắt.
  - `DELETE /delete/{id}`: Ẩn (soft-delete).
  - `GET /api/v1/user/my-patient-id`: (PATIENT) Lấy hoặc tự tạo patientId cho user đang đăng nhập.

**MedicinesController**

- **Base route**: `api/v1/admin/medicines`
- **Roles**: admin area (file class `MedicinesController`)
- **Endpoints**:
  - `GET /`: Danh sách thuốc (query: `page`, `limit`). Trả kèm `Stock` tính bằng `InventoryTransactions`.
  - `GET /details/{id}`: Chi tiết thuốc.
  - `POST /create`: Tạo thuốc (body: `MedicineCreateRequest`).
  - `PUT /update/{id}`: Cập nhật thuốc.
  - `PATCH /active/{id}?value=true|false`: Bật/Tắt sử dụng.
  - `DELETE /remove/{id}`: Xóa thuốc (kiểm tra ràng buộc lô, giao dịch kho, đơn thuốc trước khi xóa).
  - `POST /inventory/{id}`: Nhập/xuất/điều chỉnh kho (body: `InvAdjustRequest` có `TxnType` in/out/adjust, `LotId`, `Quantity`, ...).

**AdminController (UserManagerController)**

- **Base route**: `api/v1/admin/` (users endpoints)
- **Roles**: `ADMIN`
- **Endpoints**:
  - `GET /users`: Lấy danh sách user (query: `page`, `limit`, `keyword`, `role`).
  - `GET /user-details/{id}`: Chi tiết user.
  - `PUT /user-update/{id}`: Cập nhật roles (body: `Roles` list).
  - `PATCH /user-status/{id}`: Bật/Tắt user (body: `IsActive`).

**LookupResultController**

- **Base route**: `api/v1/lookup-result`
- **Roles**: AllowAnonymous
- **Endpoints**:
  - `POST /by-code` : Tra cứu hồ sơ theo mã (body: `Code`), trả encounter + prescription + labs.
  - `POST /by-phone`: Tra cứu hồ sơ theo số điện thoại (body: `Phone`, optional `Page`, `Limit`).

**EncounterController**

- **Base route**: `api/v1/encounters`
- **Roles**: `[Authorize]` (PATIENT, DOCTOR, ADMIN tùy thao tác)
- **Endpoints**:
  - `GET /` : Lịch sử khám bệnh theo patient (query: `patientId`/`patientCode`, `page`, `limit`). PATIENT chỉ xem mình; staff có thể chỉ định.
  - `GET /details/{codeOrId}`: Chi tiết hồ sơ (hỗ trợ code hoặc id). Kiểm tra authorize kỹ (patient chỉ xem chính mình).

**DoctorWorkspaceController**

- **Base route**: `api/doctor-workspace`
- **Roles**: `DOCTOR` (chỉ bác sĩ)
- **Endpoints** (chức năng cho workspace bác sĩ):
  - `GET /lab-tests`: Danh mục xét nghiệm.
  - `GET /medicines`: Danh mục thuốc (có tồn kho tính toán từ lots).
  - `POST /encounters/start`: Bắt đầu khám (body: `{ AppointmentId }`).
  - `POST /encounters/diagnosis`: Lưu chẩn đoán (tạo Encounter nếu chưa có).
  - `POST /encounters/lab-orders`: Tạo chỉ định xét nghiệm cho encounter.
  - `POST /encounters/prescriptions`: Tạo toa thuốc cho encounter.
  - `POST /encounters/complete`: Hoàn tất khám (đổi trạng thái appointment → `done` và tạo Invoice nếu có items có giá).

---

Hướng dẫn sử dụng nhanh

- Để gọi API cần thêm header `Authorization: Bearer <JWT>` cho tất cả endpoint `[Authorize]`.
- Admin/Employee: dùng route `api/v1/admin/...` để quản lý (users, categories, billing, medicines, patients).
- Bác sĩ (role DOCTOR): dùng `api/doctor-workspace` cho workflow khám, kê toa, cận lâm sàng.
- Bệnh nhân (role PATIENT): có thể tạo tài khoản, đăng nhập và xem lịch, đơn thuốc, kết quả xét nghiệm của chính mình.

Muốn bản chi tiết hơn cho từng endpoint (body DTO, ví dụ request/response), tôi có thể sinh file chi tiết dạng OpenAPI/Markdown theo từng Controller. Muốn tiếp tục mở rộng phần nào trước?
