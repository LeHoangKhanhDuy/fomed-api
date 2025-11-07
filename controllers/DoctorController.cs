using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;
using FoMed.Api.Dtos.DoctorSchedule;

[ApiController]
[Route("api/v1/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorsController(FoMedContext db) => _db = db;
    private const string DOCTOR_ROLE_CODE = "DOCTOR";
    private static readonly string[] AllowedExt = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] AllowedMime = ["image/jpeg", "image/png", "image/gif", "image/webp"];
    private const long MaxFileSize = 5L * 1024 * 1024; // 5MB

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

        var baseQuery = _db.Doctors.AsNoTracking()
            .Where(d => d.IsActive
                        && d.User != null
                        && _db.UserRoles.Any(ur => ur.UserId == d.UserId && ur.Role.Code == DOCTOR_ROLE_CODE));

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(d => d.User!.FullName)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => new DoctorListItemDto
            {
                DoctorId = d.DoctorId,
                FullName = d.User!.FullName,
                Title = d.Title,
                PrimarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty!.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                // avatar override -> fallback profile
                AvatarUrl = d.AvatarUrl ?? d.User!.Profile!.AvatarUrl,
                Intro = d.Intro,
                Educations = d.Educations
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.EducationId)
                    .Select(e => new DoctorEducationDto
                    {
                        DoctorEducationId = e.EducationId,
                        DoctorId = e.DoctorId,
                        YearFrom = e.YearFrom,
                        YearTo = e.YearTo,
                        Title = e.Title ?? string.Empty,
                        Detail = e.Detail
                    }).ToList(),
                Expertises = d.Expertises
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.ExpertiseId)
                    .Select(e => new DoctorExpertiseDto
                    {
                        DoctorExpertiseId = e.ExpertiseId,
                        DoctorId = e.DoctorId,
                        Content = e.Content ?? string.Empty
                    }).ToList(),
                Achievements = d.Achievements
                    .OrderBy(a => a.SortOrder).ThenBy(a => a.AchievementId)
                    .Select(a => new DoctorAchievementDto
                    {
                        DoctorAchievementId = a.AchievementId,
                        DoctorId = a.DoctorId,
                        YearLabel = a.YearLabel,
                        Content = a.Content ?? string.Empty
                    }).ToList(),
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
        var dto = await _db.Doctors.AsNoTracking()
            .Where(d => d.IsActive && d.DoctorId == id)
            .Select(d => new DoctorDetailDto
            {
                DoctorId = d.DoctorId,
                FullName = d.User!.FullName,
                Title = d.Title,
                LicenseNo = d.LicenseNo,
                PrimarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty!.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                ExperienceNote = d.ExperienceNote,
                Intro = d.Intro,
                VisitCount = d.VisitCount,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                AvatarUrl = d.AvatarUrl ?? d.User!.Profile!.AvatarUrl,

                Educations = d.Educations
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.EducationId)
                    .Select(e => new DoctorEducationDto
                    {
                        DoctorEducationId = e.EducationId,
                        DoctorId = e.DoctorId,
                        YearFrom = e.YearFrom,
                        YearTo = e.YearTo,
                        Title = e.Title ?? string.Empty,
                        Detail = e.Detail
                    }).ToList(),

                Expertises = d.Expertises
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.ExpertiseId)
                    .Select(e => new DoctorExpertiseDto
                    {
                        DoctorExpertiseId = e.ExpertiseId,
                        DoctorId = e.DoctorId,
                        Content = e.Content ?? string.Empty
                    }).ToList(),

                Achievements = d.Achievements
                    .OrderBy(a => a.SortOrder).ThenBy(a => a.AchievementId)
                    .Select(a => new DoctorAchievementDto
                    {
                        DoctorAchievementId = a.AchievementId,
                        DoctorId = a.DoctorId,
                        YearLabel = a.YearLabel,
                        Content = a.Content ?? string.Empty
                    }).ToList(),

                WeeklySlots = d.WeeklySlots
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Weekday).ThenBy(s => s.StartTime)
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

    /* ================== ĐÁNH GIÁ THEO BÁC SĨ ================== */
    [HttpGet("ratings/{id:int}")]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Danh sách đánh giá theo bác sĩ", Description = "Phân trang các đánh giá.", Tags = new[] { "Doctors" })]
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

    /* ================== USERS (ROLE DOCTOR) CHƯA CÓ HỒ SƠ ================== */
    [HttpGet("admin/available-users")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "User có role DOCTOR chưa có hồ sơ", Description = "Chọn User khi tạo mới Doctor.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetAvailableUsersForDoctor(CancellationToken ct = default)
    {
        var availableUsers = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive
                        && _db.UserRoles.Any(ur => ur.UserId == u.UserId && ur.Role.Code == DOCTOR_ROLE_CODE)
                        && !_db.Doctors.Any(d => d.UserId == u.UserId))
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                userId = u.UserId,
                fullName = u.FullName,
                email = u.Email,
                phone = u.Phone,
                gender = u.Profile.Gender,
                dateOfBirth = u.Profile.DateOfBirth
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = $"Tìm thấy {availableUsers.Count} user có thể tạo hồ sơ bác sĩ.",
            data = availableUsers
        });
    }

    /* ================== DANH SÁCH BÁC SĨ (ADMIN) ================== */
    [HttpGet("admin/list")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Danh sách bác sĩ (Admin)", Description = "Gồm cả inactive.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctorsForAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.Doctors.AsNoTracking().AsQueryable();

        if (isActive.HasValue) query = query.Where(d => d.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(d => d.User!.FullName.ToLower().Contains(q));
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
                email = d.User!.Email,
                phone = d.User!.Phone,
                title = d.Title,
                primarySpecialtyName = d.PrimarySpecialty != null ? d.PrimarySpecialty!.Name : null,
                licenseNo = d.LicenseNo,
                roomName = d.RoomName,
                experienceYears = d.ExperienceYears,
                isActive = d.IsActive,
                avatarUrl = d.AvatarUrl ?? d.User!.Profile!.AvatarUrl,
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

    /* ================== TẠO HỒ SƠ BÁC SĨ ================== */
    [HttpPost("admin/create")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Tạo hồ sơ bác sĩ", Description = "Cho user có role DOCTOR.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> CreateDoctorProfile([FromBody] CreateDoctorProfileRequest req, CancellationToken ct = default)
    {
        if (req.UserId <= 0)
            return BadRequest(new { success = false, message = "UserId không hợp lệ." });

        var user = await _db.Users
            .Include(u => u.UserRoles!).ThenInclude(ur => ur.Role!)
            .FirstOrDefaultAsync(u => u.UserId == req.UserId, ct);

        if (user == null) return NotFound(new { success = false, message = "Không tìm thấy User." });
        if (!user.IsActive) return BadRequest(new { success = false, message = "User đang bị vô hiệu hóa." });
        if (!user.UserRoles.Any(ur => ur.Role.Code == DOCTOR_ROLE_CODE))
            return BadRequest(new { success = false, message = "User không có quyền DOCTOR." });
        if (await _db.Doctors.AnyAsync(d => d.UserId == req.UserId, ct))
            return Conflict(new { success = false, message = "User đã có hồ sơ bác sĩ." });

        if (req.PrimarySpecialtyId is > 0)
        {
            var ok = await _db.Specialties.AnyAsync(s => s.SpecialtyId == req.PrimarySpecialtyId, ct);
            if (!ok) return BadRequest(new { success = false, message = "Chuyên khoa không tồn tại." });
        }

        if (!string.IsNullOrWhiteSpace(req.Title) && req.Title.Length > 100)
            return BadRequest(new { success = false, message = "Học hàm không được vượt quá 100 ký tự." });
        if (!string.IsNullOrWhiteSpace(req.LicenseNo) && req.LicenseNo.Length > 50)
            return BadRequest(new { success = false, message = "Số CCHN không được vượt quá 50 ký tự." });
        if (!string.IsNullOrWhiteSpace(req.RoomName) && req.RoomName.Length > 100)
            return BadRequest(new { success = false, message = "Tên phòng không được vượt quá 100 ký tự." });
        if (req.ExperienceYears is < 0 or > 100)
            return BadRequest(new { success = false, message = "Năm kinh nghiệm phải từ 0–100." });
        if (!string.IsNullOrWhiteSpace(req.ExperienceNote) && req.ExperienceNote.Length > 500)
            return BadRequest(new { success = false, message = "Ghi chú kinh nghiệm tối đa 500 ký tự." });
        if (!string.IsNullOrWhiteSpace(req.Intro) && req.Intro.Length > 2000)
            return BadRequest(new { success = false, message = "Giới thiệu tối đa 2000 ký tự." });

        var doctor = new Doctor
        {
            UserId = req.UserId,
            Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim(),
            PrimarySpecialtyId = req.PrimarySpecialtyId,
            LicenseNo = string.IsNullOrWhiteSpace(req.LicenseNo) ? null : req.LicenseNo.Trim(),
            RoomName = string.IsNullOrWhiteSpace(req.RoomName) ? null : req.RoomName.Trim(),
            ExperienceYears = req.ExperienceYears,
            ExperienceNote = string.IsNullOrWhiteSpace(req.ExperienceNote) ? null : req.ExperienceNote.Trim(),
            Intro = string.IsNullOrWhiteSpace(req.Intro) ? null : req.Intro.Trim(),
            AvatarUrl = string.IsNullOrWhiteSpace(req.AvatarUrl) ? null : req.AvatarUrl.Trim(), // có thể set sẵn
            IsActive = true,
            RatingAvg = 0,
            RatingCount = 0,
            VisitCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync(ct);

        // Bulk insert edu/expertise/achievement (nếu có)
        if (req.Educations?.Count > 0)
            _db.DoctorEducations.AddRange(req.Educations.Select(e => new DoctorEducation
            {
                DoctorId = doctor.DoctorId,
                YearFrom = e.YearFrom.HasValue ? (short?)e.YearFrom.Value : null,
                YearTo = e.YearTo.HasValue ? (short?)e.YearTo.Value : null,
                Title = e.Title,
                Detail = e.Detail
            }));
        if (req.Expertises?.Count > 0)
            _db.DoctorExpertises.AddRange(req.Expertises.Select(x => new DoctorExpertise
            {
                DoctorId = doctor.DoctorId,
                Content = x.Content
            }));
        if (req.Achievements?.Count > 0)
            _db.DoctorAchievements.AddRange(req.Achievements.Select(a => new DoctorAchievement
            {
                DoctorId = doctor.DoctorId,
                YearLabel = a.YearLabel,
                Content = a.Content
            }));

        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Tạo hồ sơ bác sĩ thành công.", data = new { doctorId = doctor.DoctorId } });
    }

    /* ================== CẬP NHẬT HỒ SƠ BÁC SĨ ================== */
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Cập nhật hồ sơ bác sĩ", Description = "Cập nhật thông tin chuyên môn.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> UpdateDoctorProfile([FromRoute] int id, [FromBody] UpdateDoctorProfileRequest req, CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id, ct);
        if (doctor == null) return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });

        if (req.Title != null) doctor.Title = req.Title.Trim();
        if (req.PrimarySpecialtyId.HasValue) doctor.PrimarySpecialtyId = req.PrimarySpecialtyId > 0 ? req.PrimarySpecialtyId : null;
        if (req.LicenseNo != null) doctor.LicenseNo = req.LicenseNo.Trim();
        if (req.RoomName != null) doctor.RoomName = req.RoomName.Trim();
        if (req.ExperienceYears.HasValue) doctor.ExperienceYears = req.ExperienceYears.Value;
        if (req.ExperienceNote != null) doctor.ExperienceNote = req.ExperienceNote.Trim();
        if (req.Intro != null) doctor.Intro = req.Intro.Trim();
        if (req.IsActive.HasValue) doctor.IsActive = req.IsActive.Value;
        if (req.AvatarUrl != null) doctor.AvatarUrl = string.IsNullOrWhiteSpace(req.AvatarUrl) ? null : req.AvatarUrl.Trim();

        // Replace child collections
        _db.DoctorEducations.RemoveRange(_db.DoctorEducations.Where(e => e.DoctorId == id));
        _db.DoctorExpertises.RemoveRange(_db.DoctorExpertises.Where(e => e.DoctorId == id));
        _db.DoctorAchievements.RemoveRange(_db.DoctorAchievements.Where(e => e.DoctorId == id));

        if (req.Educations?.Count > 0)
            _db.DoctorEducations.AddRange(req.Educations.Select(e => new DoctorEducation
            {
                DoctorId = id,
                YearFrom = e.YearFrom.HasValue ? (short?)e.YearFrom.Value : null,
                YearTo = e.YearTo.HasValue ? (short?)e.YearTo.Value : null,
                Title = e.Title,
                Detail = e.Detail
            }));
        if (req.Expertises?.Count > 0)
            _db.DoctorExpertises.AddRange(req.Expertises.Select(x => new DoctorExpertise
            {
                DoctorId = id,
                Content = x.Content
            }));
        if (req.Achievements?.Count > 0)
            _db.DoctorAchievements.AddRange(req.Achievements.Select(a => new DoctorAchievement
            {
                DoctorId = id,
                YearLabel = a.YearLabel,
                Content = a.Content
            }));
        if (req.AvatarUrl is not null)
        {
            var url = req.AvatarUrl.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var _) || !(url.StartsWith("http://") || url.StartsWith("https://")))
                    return BadRequest(new { success = false, message = "AvatarUrl phải là URL http/https hợp lệ." });
                doctor.AvatarUrl = url;
            }
        }

        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Cập nhật hồ sơ bác sĩ thành công." });
    }

    /* ================== UPLOAD ẢNH ĐẠI DIỆN (OVERRIDE) ================== */
    [HttpPost("admin/{id:int}/upload-avatar")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Upload avatar bác sĩ", Description = "Lưu file vào wwwroot/uploads/doctors và set Doctor.AvatarUrl.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> UploadDoctorAvatar([FromRoute] int id, IFormFile file, CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id, ct);
        if (doctor == null) return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });

        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "Vui lòng chọn file ảnh." });
        if (file.Length > MaxFileSize) return BadRequest(new { success = false, message = "Kích thước file tối đa 5MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext)) return BadRequest(new { success = false, message = "Chỉ chấp nhận: jpg, jpeg, png, gif, webp." });

        var mime = file.ContentType.ToLowerInvariant();
        if (!AllowedMime.Contains(mime)) return BadRequest(new { success = false, message = "Loại file không hợp lệ." });

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "doctors");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        // Xoá ảnh override cũ nếu có
        if (!string.IsNullOrWhiteSpace(doctor.AvatarUrl))
        {
            var oldName = Path.GetFileName(doctor.AvatarUrl);
            var oldPath = Path.Combine(uploadDir, oldName);
            if (System.IO.File.Exists(oldPath))
            {
                try { System.IO.File.Delete(oldPath); } catch { /* ignore */ }
            }
        }

        var unique = $"doctor_{id}_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(uploadDir, unique);

        await using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        doctor.AvatarUrl = $"/uploads/doctors/{unique}";
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Trả về avatar đã resolve
        var resolved = doctor.AvatarUrl ?? (await _db.Users
            .Where(u => u.UserId == doctor.UserId)
            .Select(u => u.Profile.AvatarUrl)
            .FirstOrDefaultAsync(ct));

        return Ok(new
        {
            success = true,
            message = "Upload ảnh đại diện thành công.",
            data = new { doctorId = id, avatarUrl = resolved }
        });
    }

    /* ================== XÓA ẢNH OVERRIDE CỦA BÁC SĨ (TRỞ VỀ FALLBACK) ================== */
    [HttpDelete("admin/{id:int}/delete-avatar")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Xóa ảnh đại diện bác sĩ", Description = "Xóa ảnh override của bác sĩ (không xóa avatar profile).", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> DeleteDoctorAvatar([FromRoute] int id, CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id, ct);
        if (doctor == null) return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });

        if (string.IsNullOrWhiteSpace(doctor.AvatarUrl))
            return BadRequest(new { success = false, message = "Bác sĩ này không có ảnh override để xoá." });

        try
        {
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "doctors");
            var name = Path.GetFileName(doctor.AvatarUrl);
            var filePath = Path.Combine(uploadDir, name);
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { /* ignore */ }
            }

            doctor.AvatarUrl = null; // về fallback
            doctor.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var fallback = await _db.Users.Where(u => u.UserId == doctor.UserId)
                .Select(u => u.Profile.AvatarUrl).FirstOrDefaultAsync(ct);

            return Ok(new { success = true, message = "Đã xoá ảnh override. Đang dùng ảnh profile (fallback).", data = new { doctorId = id, avatarUrl = fallback } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Lỗi khi xóa ảnh.", error = ex.Message });
        }
    }

    /* ================== VÔ HIỆU HÓA / KÍCH HOẠT ================== */
    [HttpDelete("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Vô hiệu hóa hồ sơ bác sĩ", Description = "IsActive = false.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> DeactivateDoctorProfile([FromRoute] int id, CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FindAsync(new object[] { id }, ct);
        if (doctor == null) return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });
        if (!doctor.IsActive) return BadRequest(new { success = false, message = "Đã vô hiệu hóa rồi." });

        doctor.IsActive = false;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Đã vô hiệu hóa hồ sơ bác sĩ." });
    }

    [HttpPatch("admin/{id:int}/activate")]
    [Authorize(Roles = "ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Kích hoạt hồ sơ bác sĩ", Description = "IsActive = true.", Tags = new[] { "Doctors" })]
    public async Task<IActionResult> ActivateDoctorProfile([FromRoute] int id, CancellationToken ct = default)
    {
        var doctor = await _db.Doctors.FindAsync(new object[] { id }, ct);
        if (doctor == null) return NotFound(new { success = false, message = "Không tìm thấy bác sĩ." });
        if (doctor.IsActive) return BadRequest(new { success = false, message = "Đang hoạt động rồi." });

        doctor.IsActive = true;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Đã kích hoạt hồ sơ bác sĩ." });
    }

    // DANH SÁCH BÁC SĨ CÙNG CHUYÊN KHOA
    [HttpGet("related/{doctorId:int}")]
    [AllowAnonymous]
    [Produces("application/json")]
    public async Task<IActionResult> GetRelatedDoctors(
    [FromRoute] int doctorId,
    [FromQuery] int limit = 10,
    CancellationToken ct = default)
    {
        // 1. Lấy bác sĩ gốc để biết chuyên khoa chính
        var current = await _db.Doctors
            .AsNoTracking()
            .Include(d => d.PrimarySpecialty)
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId, ct);

        if (current == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy bác sĩ."
            });
        }

        var specialtyId = current.PrimarySpecialtyId;

        if (specialtyId == null)
        {
            return Ok(new RelatedDoctorsResponse(
                Success: true,
                Data: new List<RelatedDoctorDto>()
            ));
        }

        // 2. Query danh sách bác sĩ khác cùng chuyên khoa
        var doctors = await _db.Doctors
            .AsNoTracking()
            .Include(d => d.PrimarySpecialty)
            .Include(d => d.User)
            .Where(d =>
                d.DoctorId != doctorId &&
                d.IsActive == true &&
                d.PrimarySpecialtyId == specialtyId
            )
            .OrderByDescending(d => d.RatingAvg)
            .ThenByDescending(d => d.RatingCount)
            .Take(Math.Max(1, Math.Min(limit, 20)))
            .Select(d => new RelatedDoctorDto(
                d.DoctorId,
                d.User != null ? d.User.FullName : null,
                d.Title,
                d.AvatarUrl,
                d.PrimarySpecialtyId,
                d.PrimarySpecialty != null ? d.PrimarySpecialty.Name : null,
                d.ExperienceYears,
                d.RatingAvg,
                d.RatingCount,
                d.RoomName
            ))
            .ToListAsync(ct);

        return Ok(new RelatedDoctorsResponse(
            Success: true,
            Data: doctors
        ));
    }

    // TẠO LỊCH LÀM VIỆC CHO BÁC SĨ
    [HttpPost("schedule/{doctorId:int}")]
    [Authorize(Roles = "ADMIN,DOCTOR,EMPLOYEE")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Tạo khung giờ làm việc lặp hàng tuần cho bác sĩ",
    Description = "ADMIN, EMPLOYEE có thể tạo cho bất kỳ bác sĩ nào. DOCTOR chỉ có thể tạo cho chính mình.",
    Tags = new[] { "DoctorSchedules" }
)]
    public async Task<IActionResult> CreateWeeklySlot(
    [FromRoute] int doctorId,
    [FromBody] CreateWeeklySlotRequest req,
    CancellationToken ct = default)
    {
        // 1. Kiểm tra doctor tồn tại và active
        var doctor = await _db.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId && d.IsActive, ct);

        if (doctor == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy bác sĩ hoặc bác sĩ đang không hoạt động."
            });
        }

        // 2. Doctor chỉ tạo cho chính mình
        if (User.IsInRole("DOCTOR"))
        {
            var claimDoctorId = User.Claims
                .FirstOrDefault(c => c.Type == "DoctorId")?.Value;

            if (string.IsNullOrEmpty(claimDoctorId) || claimDoctorId != doctorId.ToString())
                return Forbid();
        }

        // 3. Validate input
        if (req.Weekday < 1 || req.Weekday > 7)
            return BadRequest(new { success = false, message = "Weekday phải từ 1 đến 7." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { success = false, message = "Giờ bắt đầu phải nhỏ hơn giờ kết thúc." });

        // 4. Check trùng khung giờ cùng Weekday
        bool overlap = await _db.DoctorWeeklySlots.AnyAsync(s =>
            s.DoctorId == doctorId &&
            s.IsActive &&
            s.Weekday == req.Weekday &&
            s.StartTime < req.EndTime &&
            s.EndTime > req.StartTime,
            ct);

        if (overlap)
        {
            return BadRequest(new
            {
                success = false,
                message = "Khung giờ này bị trùng với khung giờ đã tồn tại trong cùng ngày trong tuần."
            });
        }

        // 5. Tạo mới
        var entity = new DoctorWeeklySlot
        {
            DoctorId = doctorId,
            Weekday = req.Weekday,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            Note = req.Note,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _db.DoctorWeeklySlots.Add(entity);
        await _db.SaveChangesAsync(ct);

        // 6. Lấy lại có include để map ra dto đẹp
        var saved = await _db.DoctorWeeklySlots
            .Include(s => s.Doctor)
                .ThenInclude(d => d.User)
            .FirstAsync(s => s.SlotId == entity.SlotId, ct);

        return Ok(new
        {
            success = true,
            data = DoctorWeeklySlotMapper.ToDto(saved),
            message = "Tạo lịch làm việc hàng tuần thành công."
        });
    }

    // LẤY DANH SÁCH GIỜ LÀM VIỆC HÀNG TUẦN
    [HttpGet("schedule-week/{doctorId:int}")]
    [Authorize(Roles = "ADMIN,DOCTOR,EMPLOYEE")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Lấy danh sách khung giờ làm việc lặp hàng tuần của bác sĩ",
    Description = "Trả về theo thứ trong tuần, không expand ra ngày cụ thể",
    Tags = new[] { "DoctorSchedules" }
)]
    public async Task<IActionResult> GetWeeklySlots(
    [FromRoute] int doctorId,
    CancellationToken ct = default)
    {
        // Doctor chỉ xem của mình
        if (User.IsInRole("DOCTOR"))
        {
            var claimDoctorId = User.Claims
                .FirstOrDefault(c => c.Type == "DoctorId")?.Value;

            if (string.IsNullOrEmpty(claimDoctorId) || claimDoctorId != doctorId.ToString())
                return Forbid();
        }

        var slots = await _db.DoctorWeeklySlots
            .Include(s => s.Doctor)
                .ThenInclude(d => d.User)
            .Where(s => s.DoctorId == doctorId && s.IsActive)
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.StartTime)
            .Select(s => DoctorWeeklySlotMapper.ToDto(s))
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            data = slots
        });
    }

    // LỊCH LÀM VIỆC THEO TỪNG TUẦN
    [HttpGet("calendar")]
    [Authorize(Roles = "ADMIN,DOCTOR,EMPLOYEE")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Lịch làm việc hiển thị dạng tuần",
    Description = "Expand slot lặp theo thứ ra từng ngày cụ thể trong khoảng from..to để đổ lên calendar UI",
    Tags = new[] { "DoctorSchedules" }
    )]
    public async Task<IActionResult> GetCalendar(
    [FromQuery] DateOnly from,
    [FromQuery] DateOnly to,
    [FromQuery] int? doctorId,
    CancellationToken ct = default)
    {
        if (to < from)
            return BadRequest(new { success = false, message = "Khoảng thời gian không hợp lệ." });

        // Load tất cả bác sĩ hợp lệ (và lọc nếu FE chọn 1 bác sĩ cụ thể)
        var doctorQuery = _db.Doctors
            .Include(d => d.User)
            .AsNoTracking()
            .Where(d => d.IsActive);

        if (doctorId.HasValue && doctorId.Value > 0)
            doctorQuery = doctorQuery.Where(d => d.DoctorId == doctorId.Value);

        var doctors = await doctorQuery
            .Select(d => new
            {
                d.DoctorId,
                DoctorName = "BS. " + d.User!.FullName,
                d.RoomName
            })
            .ToListAsync(ct);

        if (!doctors.Any())
            return Ok(new { success = true, data = new List<DoctorCalendarBlockDto>() });

        var doctorIds = doctors.Select(d => d.DoctorId).ToList();

        // Lấy tất cả slot lặp của những bác sĩ đó
        var slots = await _db.DoctorWeeklySlots
            .AsNoTracking()
            .Where(s => s.IsActive && doctorIds.Contains(s.DoctorId))
            .ToListAsync(ct);

        var result = new List<DoctorCalendarBlockDto>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            // map DayOfWeek -> Weekday 1..7
            byte weekdayNumber = date.DayOfWeek switch
            {
                DayOfWeek.Monday => (byte)1,
                DayOfWeek.Tuesday => (byte)2,
                DayOfWeek.Wednesday => (byte)3,
                DayOfWeek.Thursday => (byte)4,
                DayOfWeek.Friday => (byte)5,
                DayOfWeek.Saturday => (byte)6,
                DayOfWeek.Sunday => (byte)7,
                _ => (byte)0
            };

            foreach (var s in slots.Where(x => x.Weekday == weekdayNumber))
            {
                var meta = doctors.First(d => d.DoctorId == s.DoctorId);

                result.Add(new DoctorCalendarBlockDto
                {
                    SlotId = s.SlotId,
                    DoctorId = s.DoctorId,
                    DoctorName = meta.DoctorName,
                    RoomName = meta.RoomName,

                    Date = date, // ngày thực tế trong tuần
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,

                    Note = s.Note
                });
            }
        }

        // sort cho đẹp
        result = result
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StartTime)
            .ThenBy(r => r.DoctorName)
            .ToList();

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    // CẬP NHẬT LỊCH LÀM VIỆC
    [HttpPut("schedule-update/{slotId:long}")]
    [Authorize(Roles = "ADMIN,DOCTOR,EMPLOYEE")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Cập nhật khung giờ làm việc lặp hàng tuần",
    Description = "Sửa ca trực cố định",
    Tags = new[] { "DoctorSchedules" }
    )]
    public async Task<IActionResult> UpdateWeeklySlot(
    [FromRoute] long slotId,
    [FromBody] UpdateWeeklySlotRequest req,
    CancellationToken ct = default)
    {
        var slot = await _db.DoctorWeeklySlots
            .Include(s => s.Doctor)
                .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.SlotId == slotId, ct);

        if (slot == null)
            return NotFound(new { success = false, message = "Không tìm thấy slot." });

        // Nếu user là DOCTOR thì chỉ được sửa slot của mình
        if (User.IsInRole("DOCTOR"))
        {
            var claimDoctorId = User.Claims
                .FirstOrDefault(c => c.Type == "DoctorId")?.Value;

            if (string.IsNullOrEmpty(claimDoctorId) || claimDoctorId != slot.DoctorId.ToString())
                return Forbid();
        }

        if (req.Weekday < 1 || req.Weekday > 7)
            return BadRequest(new { success = false, message = "Weekday phải từ 1 đến 7." });

        if (req.StartTime >= req.EndTime)
            return BadRequest(new { success = false, message = "Giờ bắt đầu phải nhỏ hơn giờ kết thúc." });

        // check overlap trùng giờ (ngoại trừ chính nó)
        bool overlap = await _db.DoctorWeeklySlots.AnyAsync(s =>
            s.SlotId != slotId &&
            s.DoctorId == slot.DoctorId &&
            s.IsActive &&
            s.Weekday == req.Weekday &&
            s.StartTime < req.EndTime &&
            s.EndTime > req.StartTime,
            ct);

        if (overlap)
        {
            return BadRequest(new
            {
                success = false,
                message = "Khung giờ này bị trùng với khung giờ khác."
            });
        }

        slot.Weekday = req.Weekday;
        slot.StartTime = req.StartTime;
        slot.EndTime = req.EndTime;
        slot.Note = req.Note;
        slot.IsActive = req.IsActive;
        // slot.CreatedAt giữ nguyên, không đụng

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            data = DoctorWeeklySlotMapper.ToDto(slot),
            message = "Cập nhật thành công."
        });
    }

    // VÔ HIỆU HÓA KHUNG GIỜ
    [HttpDelete("shedule-delete/{slotId:long}")]
    [Authorize(Roles = "ADMIN,DOCTOR,EMPLOYEE")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Xoá (vô hiệu hoá) khung giờ làm việc lặp hàng tuần",
    Description = "Dùng cho nút thùng rác trong calendar block",
    Tags = new[] { "DoctorSchedules" }
)]
    public async Task<IActionResult> DeleteWeeklySlot(
    [FromRoute] long slotId,
    CancellationToken ct = default)
    {
        var slot = await _db.DoctorWeeklySlots
            .Include(s => s.Doctor)
                .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.SlotId == slotId, ct);

        if (slot == null)
            return NotFound(new { success = false, message = "Không tìm thấy slot." });

        // Doctor chỉ được xoá slot của mình
        if (User.IsInRole("DOCTOR"))
        {
            var claimDoctorId = User.Claims
                .FirstOrDefault(c => c.Type == "DoctorId")?.Value;

            if (string.IsNullOrEmpty(claimDoctorId) || claimDoctorId != slot.DoctorId.ToString())
                return Forbid();
        }

        // Xoá mềm
        slot.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã vô hiệu hoá ca trực."
        });
    }

}
