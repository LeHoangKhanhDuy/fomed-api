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
            .Where(d => d.IsActive && d.User != null) //  kiểm tra User
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

    /* ================== CHI TIẾT BÁC SĨ ================== */
    [HttpGet("details/{id:int}")]
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
