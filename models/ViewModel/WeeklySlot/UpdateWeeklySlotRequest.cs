namespace FoMed.Api.Dtos.DoctorSchedule
{
    public class UpdateWeeklySlotRequest
    {
        public byte Weekday { get; set; }          // có thể sửa đổi thứ làm việc
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; } = true; // bật/tắt ca
    }
}
