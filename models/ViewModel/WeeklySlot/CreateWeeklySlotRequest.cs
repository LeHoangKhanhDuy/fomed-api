namespace FoMed.Api.Dtos.DoctorSchedule
{
    public class CreateWeeklySlotRequest
    {
        public byte Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Note { get; set; }
    }
}
