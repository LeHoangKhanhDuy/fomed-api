using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;

[ApiController]
[Route("api/v1/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorsController(FoMedContext db) => _db = db;

    /* ================== DANH SÁCH BÁC SĨ CÔNG KHAI ================== */
    [HttpGet]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Danh sách bác sĩ công khai", Tags = new[] { "Doctors" })]
    public async Task<ActionResult> GetDoctors(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        const string DOCTOR_ROLE_CODE = "DOCTOR";

        var query = _db.Doctors
            .AsNoTracking()
            .Where(d => d.IsActive && d.User != null)
            .Include(d => d.User!)
                .ThenInclude(u => u.Profile)
            .Include(d => d.User!)
                .ThenInclude(u => u.UserRoles!)
                    .ThenInclude(ur => ur.Role!)
            .Include(d => d.PrimarySpecialty)
            .Where(d => d.User!.UserRoles.Any(ur => ur.Role.Code == DOCTOR_ROLE_CODE));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(d => d.User!.FullName)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => new DoctorListItemDto
            {
                DoctorId = d.DoctorId,
                FullName = d.User!.FullName,
                Title = d.Title,
                PrimarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                AvatarUrl = d.User.Profile!.AvatarUrl
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                page,
                limit,
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ================== CHI TIẾT BÁC SĨ CÔNG KHAI ================== */
    [HttpGet("details/{id:int}")]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Chi tiết bác sĩ",
        Description = "Thông tin hồ sơ + học vấn + chuyên môn + thành tựu + lịch làm việc tuần.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctorDetail([FromRoute] int id, CancellationToken ct = default)
    {
        var dto = await _db.Doctors
            .AsNoTracking()
            .Where(d => d.IsActive && d.DoctorId == id)
            .Include(d => d.User!)
                .ThenInclude(u => u.Profile!)
            .Include(d => d.PrimarySpecialty)
            .Include(d => d.Educations)
            .Include(d => d.Expertises)
            .Include(d => d.Achievements)
            .Include(d => d.WeeklySlots)
            .Select(d => new DoctorDetailDto
            {
                DoctorId = d.DoctorId,
                FullName = d.User!.FullName,
                Title = d.Title,
                LicenseNo = d.LicenseNo,
                PrimarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                ExperienceNote = d.ExperienceNote,
                Intro = d.Intro,
                VisitCount = d.VisitCount,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                AvatarUrl = d.User.Profile!.AvatarUrl,

                Educations = d.Educations
                    .OrderBy(e => e.SortOrder)
                    .ThenBy(e => e.EducationId)
                    .Select(e => new DoctorEducationDto
                    {
                        YearFrom = e.YearFrom,
                        YearTo = e.YearTo,
                        Title = e.Title ?? string.Empty,
                        Detail = e.Detail
                    }).ToList(),

                Expertises = d.Expertises
                    .OrderBy(e => e.SortOrder)
                    .ThenBy(e => e.ExpertiseId)
                    .Select(e => new DoctorExpertiseDto
                    {
                        Content = e.Content ?? string.Empty
                    }).ToList(),

                Achievements = d.Achievements
                    .OrderBy(a => a.SortOrder)
                    .ThenBy(a => a.AchievementId)
                    .Select(a => new DoctorAchievementDto
                    {
                        YearLabel = a.YearLabel,
                        Content = a.Content ?? string.Empty
                    }).ToList(),

                WeeklySlots = d.WeeklySlots
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Weekday)
                    .ThenBy(s => s.StartTime)
                    .Select(s => new DoctorWeeklySlotDto
                    {
                        Weekday = s.Weekday,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Note = s.Note
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (dto == null)
            return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });

        return Ok(new { success = true, message = "Lấy thông tin thành công.", data = dto });
    }

    /* ================== SỐ LƯỢNG ĐÁNH GIÁ CỦA BÁC SĨ  ================== */
    [HttpGet("ratings/{id:int}")]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách đánh giá theo bác sĩ",
        Description = "Phân trang các đánh giá.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctorRatings(
        [FromRoute] int id,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var q = _db.DoctorRatings.AsNoTracking().Where(r => r.DoctorId == id);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RatingId)
            .Skip((page - 1) * limit).Take(limit)
            .Select(r => new DoctorRatingItemDto
            {
                RatingId = r.RatingId,
                Score = r.Score,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách đánh giá thành công.",
            data = new
            {
                page,
                limit,
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ================== Lấy danh sách Users có role DOCTOR chưa có hồ sơ bác sĩ  ================== */
    [HttpGet("admin/available-users")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách User có role DOCTOR chưa có hồ sơ",
        Description = "Dùng để chọn User khi tạo mới Doctor profile.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetAvailableUsersForDoctor(CancellationToken ct = default)
    {
        const string DOCTOR_ROLE_CODE = "DOCTOR";

        var availableUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Include(u => u.UserRoles!)
                .ThenInclude(ur => ur.Role!)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Code == DOCTOR_ROLE_CODE))
            .Where(u => !_db.Doctors.Any(d => d.UserId == u.UserId))
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                userId = u.UserId,
                fullName = u.FullName,
                email = u.Email,
                phone = u.Phone,
                gender = u.Gender,
                dateOfBirth = u.DateOfBirth
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = $"Tìm thấy {availableUsers.Count} user có thể tạo hồ sơ bác sĩ.",
            data = availableUsers
        });
    }

    /* ================== Danh sách tất cả bác sĩ QUẢN TRỊ   ================== */
    [HttpGet("admin/list")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách bác sĩ Quản trị",
        Description = "Hiển thị tất cả bác sĩ kể cả inactive, dùng cho quản trị.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctorsForAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.Doctors
            .AsNoTracking()
            .Include(d => d.User!)
                .ThenInclude(u => u.Profile)
            .Include(d => d.PrimarySpecialty)
            .AsQueryable();

        // Filter by IsActive
        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        // Search by name
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(d => d.User!.FullName.ToLower().Contains(searchLower));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => new
            {
                doctorId = d.DoctorId,
                userId = d.UserId,
                fullName = d.User!.FullName,
                email = d.User.Email,
                phone = d.User.Phone,
                title = d.Title,
                primarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty.Name : null,
                licenseNo = d.LicenseNo,
                roomName = d.RoomName,
                experienceYears = d.ExperienceYears,
                isActive = d.IsActive,
                avatarUrl = d.User.Profile!.AvatarUrl,
                ratingAvg = d.RatingAvg,
                ratingCount = d.RatingCount,
                visitCount = d.VisitCount,
                createdAt = d.CreatedAt,
                updatedAt = d.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách bác sĩ thành công.",
            data = new
            {
                page,
                limit,
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ================== Tạo hồ sơ bác sĩ mới   ================== */
    /* ================== Tạo hồ sơ bác sĩ mới   ================== */
    [HttpPost("admin/create")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Tạo hồ sơ bác sĩ",
        Description = "Tạo hồ sơ chuyên môn cho User có role DOCTOR.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> CreateDoctorProfile(
        [FromBody] CreateDoctorProfileRequest req,
        CancellationToken ct = default)
    {
        // Validate request
        if (req.UserId <= 0)
            return BadRequest(new { success = false, message = "UserId không hợp lệ." });

        // Check User exists and is active
        var user = await _db.Users
            .Include(u => u.UserRoles!)
                .ThenInclude(ur => ur.Role!)
            .FirstOrDefaultAsync(u => u.UserId == req.UserId, ct);

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy User với ID này." });

        if (!user.IsActive)
            return BadRequest(new { success = false, message = "User này đã bị vô hiệu hóa, không thể tạo hồ sơ bác sĩ." });

        // Check User has DOCTOR role
        const string DOCTOR_ROLE_CODE = "DOCTOR";
        if (!user.UserRoles.Any(ur => ur.Role.Code == DOCTOR_ROLE_CODE))
            return BadRequest(new { success = false, message = "User này không có quyền DOCTOR." });

        // Check if Doctor profile already exists
        if (await _db.Doctors.AnyAsync(d => d.UserId == req.UserId, ct))
            return Conflict(new { success = false, message = "User này đã có hồ sơ bác sĩ rồi." });

        // Validate PrimarySpecialty if provided
        if (req.PrimarySpecialtyId.HasValue && req.PrimarySpecialtyId.Value > 0)
        {
            var specialtyExists = await _db.Specialties
                .AnyAsync(s => s.SpecialtyId == req.PrimarySpecialtyId.Value, ct);

            if (!specialtyExists)
                return BadRequest(new { success = false, message = "Chuyên khoa không tồn tại." });
        }

        // Validate Title
        if (!string.IsNullOrWhiteSpace(req.Title) && req.Title.Length > 50)
            return BadRequest(new { success = false, message = "Học hàm không được vượt quá 50 ký tự." });

        // Validate LicenseNo
        if (!string.IsNullOrWhiteSpace(req.LicenseNo) && req.LicenseNo.Length > 50)
            return BadRequest(new { success = false, message = "Số chứng chỉ hành nghề không được vượt quá 50 ký tự." });

        // Validate RoomName
        if (!string.IsNullOrWhiteSpace(req.RoomName) && req.RoomName.Length > 100)
            return BadRequest(new { success = false, message = "Tên phòng khám không được vượt quá 100 ký tự." });

        // Validate ExperienceYears
        if (req.ExperienceYears.HasValue && (req.ExperienceYears.Value < 0 || req.ExperienceYears.Value > 100))
            return BadRequest(new { success = false, message = "Số năm kinh nghiệm phải từ 0 đến 100." });

        // Validate ExperienceNote
        if (!string.IsNullOrWhiteSpace(req.ExperienceNote) && req.ExperienceNote.Length > 500)
            return BadRequest(new { success = false, message = "Ghi chú kinh nghiệm không được vượt quá 500 ký tự." });

        // Validate Intro
        if (!string.IsNullOrWhiteSpace(req.Intro) && req.Intro.Length > 2000)
            return BadRequest(new { success = false, message = "Giới thiệu không được vượt quá 2000 ký tự." });

        // Create Doctor
        var doctor = new Doctor
        {
            UserId = req.UserId,
            Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim(),
            PrimarySpecialtyId = req.PrimarySpecialtyId,
            LicenseNo = string.IsNullOrWhiteSpace(req.LicenseNo) ? null : req.LicenseNo.Trim(),
            RoomName = string.IsNullOrWhiteSpace(req.RoomName) ? null : req.RoomName.Trim(),
            ExperienceYears = req.ExperienceYears, // ✅ SỬA TẠI ĐÂY - xóa ?? 0
            ExperienceNote = string.IsNullOrWhiteSpace(req.ExperienceNote) ? null : req.ExperienceNote.Trim(),
            Intro = string.IsNullOrWhiteSpace(req.Intro) ? null : req.Intro.Trim(),
            IsActive = true,
            RatingAvg = 0,
            RatingCount = 0,
            VisitCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Doctors.Add(doctor);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Log lỗi chi tiết
            Console.WriteLine($"DbUpdateException: {ex.Message}");
            Console.WriteLine($"InnerException: {ex.InnerException?.Message}");

            return StatusCode(500, new
            {
                success = false,
                message = "Không thể lưu hồ sơ bác sĩ vào database.",
                error = ex.InnerException?.Message ?? ex.Message
            });
        }

        return Ok(new
        {
            success = true,
            message = "Tạo hồ sơ bác sĩ thành công.",
            data = new { doctorId = doctor.DoctorId }
        });
    }

    // Cập nhật hồ sơ bác sĩ
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Cập nhật hồ sơ bác sĩ",
        Description = "Cập nhật thông tin chuyên môn của bác sĩ.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> UpdateDoctorProfile(
        [FromRoute] int id,
        [FromBody] UpdateDoctorProfileRequest req,
        CancellationToken ct = default)
    {
        // Check Doctor exists
        var doctor = await _db.Doctors.FindAsync(new object[] { id }, ct);

        if (doctor == null)
            return NotFound(new { success = false, message = "Không tìm thấy bác sĩ với ID này." });

        // Validate PrimarySpecialty if provided
        if (req.PrimarySpecialtyId.HasValue && req.PrimarySpecialtyId.Value > 0)
        {
            var specialtyExists = await _db.Specialties
                .AnyAsync(s => s.SpecialtyId == req.PrimarySpecialtyId.Value, ct);

            if (!specialtyExists)
                return BadRequest(new { success = false, message = "Chuyên khoa không tồn tại." });
        }

        // Validate Title
        if (req.Title != null && req.Title.Length > 50)
            return BadRequest(new { success = false, message = "Học hàm không được vượt quá 50 ký tự." });

        // Validate LicenseNo
        if (req.LicenseNo != null && req.LicenseNo.Length > 50)
            return BadRequest(new { success = false, message = "Số chứng chỉ hành nghề không được vượt quá 50 ký tự." });

        // Validate RoomName
        if (req.RoomName != null && req.RoomName.Length > 100)
            return BadRequest(new { success = false, message = "Tên phòng khám không được vượt quá 100 ký tự." });

        // Validate ExperienceYears
        if (req.ExperienceYears.HasValue && (req.ExperienceYears.Value < 0 || req.ExperienceYears.Value > 100))
            return BadRequest(new { success = false, message = "Số năm kinh nghiệm phải từ 0 đến 100." });

        // Validate ExperienceNote
        if (req.ExperienceNote != null && req.ExperienceNote.Length > 500)
            return BadRequest(new { success = false, message = "Ghi chú kinh nghiệm không được vượt quá 500 ký tự." });

        // Validate Intro
        if (req.Intro != null && req.Intro.Length > 2000)
            return BadRequest(new { success = false, message = "Giới thiệu không được vượt quá 2000 ký tự." });

        // Update fields (chỉ update nếu không null)
        if (req.Title != null)
            doctor.Title = req.Title.Trim();

        if (req.PrimarySpecialtyId.HasValue)
            doctor.PrimarySpecialtyId = req.PrimarySpecialtyId.Value > 0 ? req.PrimarySpecialtyId.Value : null;

        if (req.LicenseNo != null)
            doctor.LicenseNo = req.LicenseNo.Trim();

        if (req.RoomName != null)
            doctor.RoomName = req.RoomName.Trim();

        if (req.ExperienceYears.HasValue)
            doctor.ExperienceYears = req.ExperienceYears.Value;

        if (req.ExperienceNote != null)
            doctor.ExperienceNote = req.ExperienceNote.Trim();

        if (req.Intro != null)
            doctor.Intro = req.Intro.Trim();

        if (req.IsActive.HasValue)
            doctor.IsActive = req.IsActive.Value;

        doctor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Cập nhật hồ sơ bác sĩ thành công."
        });
    }

    //Vô hiệu hóa hồ sơ bác sĩ
    [HttpDelete("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Vô hiệu hóa hồ sơ bác sĩ",
        Description = "Đặt IsActive = false, bác sĩ sẽ không hiển thị công khai.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> DeactivateDoctorProfile(
        [FromRoute] int id,
        CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FindAsync(new object[] { id }, ct);

        if (doctor == null)
            return NotFound(new { success = false, message = "Không tìm thấy bác sĩ với ID này." });

        if (!doctor.IsActive)
            return BadRequest(new { success = false, message = "Hồ sơ bác sĩ này đã bị vô hiệu hóa rồi." });

        doctor.IsActive = false;
        doctor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã vô hiệu hóa hồ sơ bác sĩ thành công."
        });
    }

    //Kích hoạt lại hồ sơ bác sĩ
    [HttpPatch("admin/{id:int}/activate")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Kích hoạt lại hồ sơ bác sĩ",
        Description = "Đặt IsActive = true, bác sĩ sẽ hiển thị công khai trở lại.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> ActivateDoctorProfile(
        [FromRoute] int id,
        CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FindAsync(new object[] { id }, ct);

        if (doctor == null)
            return NotFound(new { success = false, message = "Không tìm thấy bác sĩ với ID này." });

        if (doctor.IsActive)
            return BadRequest(new { success = false, message = "Hồ sơ bác sĩ này đang hoạt động rồi." });

        doctor.IsActive = true;
        doctor.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã kích hoạt hồ sơ bác sĩ thành công."
        });
    }

    
}
