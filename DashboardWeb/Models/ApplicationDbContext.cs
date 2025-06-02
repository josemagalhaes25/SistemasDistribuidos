using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DashboardWeb.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<SensorReading> SensorReadings { get; set; }
    }
}