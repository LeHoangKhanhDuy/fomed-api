
namespace FoMed.Api.Dtos.DoctorSchedule
{
    public class DoctorWeeklySlotDto
    {
        public long SlotId { get; set; }
        public int DoctorId { get; set; }
        public byte Weekday { get; set; }        // 1..7
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Thông tin hiển thị thêm cho FE
        public string DoctorName { get; set; } = "";
        public string? RoomName { get; set; }
    }
}
