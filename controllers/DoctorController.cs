using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;

[ApiController]
[Route("api/v1/doctors")]
[AllowAnonymous] // public endpoints
public class DoctorsController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorsController(FoMedContext db) => _db = db;

    /* ================== DANH SÁCH BÁC SĨ (phân trang) ================== */
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách bác sĩ (phân trang)",
        Description = "Trả về danh sách rút gọn để render list.",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctors(
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 10,
        [FromQuery] int? specialtyId = null,   // tùy chọn: lọc theo chuyên khoa
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var q = _db.Doctors.AsNoTracking().Where(d => d.IsActive);

        if (specialtyId.HasValue && specialtyId > 0)
            q = q.Where(d => d.PrimarySpecialtyId == specialtyId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.DoctorId) // hiển thị từ id nhỏ -> lớn
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => new DoctorListItemDto
            {
                DoctorId = d.DoctorId,
                FullName = d.FullName,
                Title = d.Title,
                PrimarySpecialtyName = d.PrimarySpecialtyId != null ? d.PrimarySpecialty!.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                AvatarUrl = d.AvatarUrl
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "OK",
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

    /* ================== CHI TIẾT BÁC SĨ ================== */
    [HttpGet("details/{id:int}")]
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
                FullName = d.FullName,
                Title = d.Title,
                LicenseNo = d.LicenseNo,
                PrimarySpecialtyName = d.PrimarySpecialtyId != null ? d.PrimarySpecialty!.Name : null,
                RoomName = d.RoomName,
                ExperienceYears = d.ExperienceYears,
                ExperienceNote = d.ExperienceNote,
                Intro = d.Intro,
                VisitCount = d.VisitCount,
                RatingAvg = d.RatingAvg,
                RatingCount = d.RatingCount,
                AvatarUrl = d.AvatarUrl,

                Educations = d.Educations
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.EducationId)
                    .Select(e => new DoctorEducationDto
                    {
                        YearFrom = e.YearFrom,
                        YearTo = e.YearTo,
                        Title = e.Title,
                        Detail = e.Detail
                    }).ToList(),

                Expertises = d.Expertises
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.ExpertiseId)
                    .Select(e => new DoctorExpertiseDto { Content = e.Content })
                    .ToList(),

                Achievements = d.Achievements
                    .OrderBy(e => e.SortOrder).ThenBy(e => e.AchievementId)
                    .Select(a => new DoctorAchievementDto
                    {
                        YearLabel = a.YearLabel,
                        Content = a.Content
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

        return Ok(new { success = true, message = "OK", data = dto });
    }

    /* ================== ĐÁNH GIÁ CỦA BÁC SĨ (phân trang) ================== */
    [HttpGet("ratings/{id:int}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách đánh giá theo bác sĩ",
        Description = "Phân trang các đánh giá (score/comment).",
        Tags = new[] { "Doctors" })]
    public async Task<IActionResult> GetDoctorRatings(
        [FromRoute] int id,
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 20,
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
            message = "OK",
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
}
