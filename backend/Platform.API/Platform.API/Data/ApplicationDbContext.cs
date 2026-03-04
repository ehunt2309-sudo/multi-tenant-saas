using Microsoft.EntityFrameworkCore;
using platform.API.Models;
using Platform.API.Models; // Make sure this namespace matches where your models are

namespace Platform.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Constructor
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tables / DbSets
        public DbSet<Client> Clients { get; set; }
        public DbSet<User> Users { get; set; }           // Staff/Admin
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Product> Products { get; set; }     // Optional store
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<LoyaltyPoint> LoyaltyPoints { get; set; }
    }
}