using System;
using System.Linq;
using Infostructure.MyBigBro.DataModel.Models;
using Microsoft.EntityFrameworkCore;

namespace Infostructure.MyBigBro.DataModel.DataAccess
{
    public class MyBigBroContext : DbContext, IMyBigBroContext
    {
        private readonly string _connectionString;

        static MyBigBroContext()
        {
            // Allow unspecified DateTime kinds to be written to PostgreSQL TIMESTAMPTZ seamlessly
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public MyBigBroContext(DbContextOptions<MyBigBroContext> options) : base(options)
        {
        }

        public MyBigBroContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrWhiteSpace(_connectionString))
            {
                optionsBuilder
                    .UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite())
                    .UseSnakeCaseNamingConvention();
            }
        }

        public DbSet<WebCam> WebCams { get; set; }
        public DbSet<CapturedImage> CapturedImages { get; set; }
        public DbSet<GeoMarker> GeoMarkers { get; set; }
        public DbSet<CapturedImageGeoMarker> CapturedImageGeoMarker { get; set; }
        public DbSet<Device> Device { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<CaptureJob> CaptureJobs { get; set; }

        public IQueryable<T> SetWrapper<T>() where T : class
        {
            return this.Set<T>();
        }

        public int SaveChangesWrapper()
        {
            return this.SaveChanges();
        }

        public new void Add<T>(T entity) where T : class
        {
            this.Entry(entity).State = EntityState.Added;
        }

        public void Modify<T>(T entity) where T : class
        {
            this.Entry(entity).State = EntityState.Modified;
        }

        public void Delete<T>(T entity) where T : class
        {
            this.Entry(entity).State = EntityState.Deleted;
        }
    }
}
