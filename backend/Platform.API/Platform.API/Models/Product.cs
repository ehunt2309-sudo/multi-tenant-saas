namespace Platform.API.Models
{
    public class Product
    {
        public int ProductID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }

        public int ClientID { get; set; }
        public Client Client { get; set; }
    }
}