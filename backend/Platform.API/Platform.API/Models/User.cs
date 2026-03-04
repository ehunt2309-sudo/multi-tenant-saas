using platform.API.Models;
using Platform.API.Models;

public class User
{
    public int StaffID { get; set; }            // same as UserID
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }            // Admin / Staff
    public string CalendarLink { get; set; }    // Optional

    // Foreign key
    public int ClientID { get; set; }
    public Client Client { get; set; }

    // Navigation
    public ICollection<Booking> Bookings { get; set; }
    public ICollection<Schedule> Schedules { get; set; }
}