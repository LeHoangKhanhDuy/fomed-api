namespace FoMed.Api.Dtos.DoctorSchedule
{
    public class DoctorCalendarBlockDto
    {
        public long SlotId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public string? RoomName { get; set; }

        public DateOnly Date { get; set; }       // ngày cụ thể trong tuần
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public string? Note { get; set; }        // ví dụ "Phòng 203"
    }
}
