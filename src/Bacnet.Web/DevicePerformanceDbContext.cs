using System;
using Microsoft.EntityFrameworkCore;
using Bacnet.Models;

namespace Bacnet.Web
{
    public class DevicePerformanceDbContext : DbContext
    {
        public DevicePerformanceDbContext(DbContextOptions<DevicePerformanceDbContext> options) : base(options)
        { }
        public virtual DbSet<DevicePerformance> DevicePerformances { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DevicePerformance>().HasKey(x => x.Id);
            base.OnModelCreating(modelBuilder);
        }
    }

}

