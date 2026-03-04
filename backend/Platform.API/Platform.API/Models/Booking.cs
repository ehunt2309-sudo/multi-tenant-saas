using Platform.API.Models;

namespace platform.API.Models
{

    public class Booking
    {
        public int BookingID { get; set; }
        public DateTime DateTime { get; set; }
        public string Service { get; set; }
        public string Status { get; set; } // Pending, Completed, Cancelled

        public int StaffID { get; set; }
        public User Staff { get; set; }

        public int CustomerID { get; set; }
        public Customer Customer { get; set; }
    }
}