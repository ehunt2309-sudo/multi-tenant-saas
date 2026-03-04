namespace Platform.API.Models
{
    public class Promotion
    {
        public int PromotionID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int ClientID { get; set; }
        public Client Client { get; set; }
    }
}