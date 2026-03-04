using platform.API.Models;

namespace Platform.API.Models
{
    public class Customer
    {
        public int CustomerID { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Gender { get; set; }
        public int Age { get; set; }
        public int Points { get; set; }

        public int ClientID { get; set; }
        public Client Client { get; set; }

        // Navigation
        public ICollection<Booking> Bookings { get; set; }
        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; }
    }
}