namespace Platform.API.Models
{
    public class Client
    {
        public int ClientID { get; set; }
        public string Name { get; set; }           // Spa, restaurant, etc.
        public string Industry { get; set; }       // Type of business
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }

        // Navigation
        public ICollection<User> Users { get; set; }
        public ICollection<Customer> Customers { get; set; }
        public ICollection<Promotion> Promotions { get; set; }
        public ICollection<Product> Products { get; set; }
    }
}