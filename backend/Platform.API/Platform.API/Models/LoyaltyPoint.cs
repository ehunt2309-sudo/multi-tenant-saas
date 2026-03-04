namespace Platform.API.Models
{
    public class LoyaltyPoint
    {
        public int LoyaltyPointID { get; set; }
        public int Points { get; set; }
        public DateTime DateEarned { get; set; }

        public int CustomerID { get; set; }
        public Customer Customer { get; set; }
    }
}