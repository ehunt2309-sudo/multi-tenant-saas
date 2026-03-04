namespace Platform.API.Models
{
    public class Schedule
    {
        public int ScheduleID { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public int StaffID { get; set; }
        public User Staff { get; set; }

        public int ClientID { get; set; }
        public Client Client { get; set; }
    }
}