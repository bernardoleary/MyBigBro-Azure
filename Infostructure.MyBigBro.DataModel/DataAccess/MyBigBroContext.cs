using System.Data.Entity;
using System.Linq;
using Infostructure.MyBigBro.DataModel.Models;

namespace Infostructure.MyBigBro.DataModel.DataAccess
{
    public class MyBigBroContext : DbContext, IMyBigBroContext
    {
        public MyBigBroContext(string connectionString) : base(connectionString) { }
        
        public DbSet<WebCam> WebCams { get; set; }
        public DbSet<CapturedImage> CapturedImages { get; set; }
        public DbSet<GeoMarker> GeoMarkers { get; set; }
        public DbSet<CapturedImageGeoMarker> CapturedImageGeoMarker { get; set; }
        public DbSet<Device> Device { get; set; }

        public IQueryable<T> SetWrapper<T>() where T : class
        {
            return this.Set<T>();
        }

        public int SaveChangesWrapper()
        {
            return this.SaveChanges();
        }

        public void Add<T>(T entity) where T : class
        {
            this.Entry(entity).State = System.Data.EntityState.Added;
        }

        public void Modify<T>(T entity) where T : class
        {
            this.Entry(entity).State = System.Data.EntityState.Modified;
        }

        public void Delete<T>(T entity) where T : class
        {
            this.Entry(entity).State = System.Data.EntityState.Deleted;
        }
    }
}
