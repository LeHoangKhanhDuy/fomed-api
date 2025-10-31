using FoMed.Api.Models;

namespace FoMed.Api.Dtos.DoctorSchedule
{
    public static class DoctorWeeklySlotMapper
    {
        public static DoctorWeeklySlotDto ToDto(DoctorWeeklySlot s)
        {
            return new DoctorWeeklySlotDto
            {
                SlotId = s.SlotId,
                DoctorId = s.DoctorId,
                Weekday = s.Weekday,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Note = s.Note,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                DoctorName = "BS. " + (s.Doctor?.User?.FullName ?? ""),
                RoomName = s.Doctor?.RoomName
            };
        }
    }
}
