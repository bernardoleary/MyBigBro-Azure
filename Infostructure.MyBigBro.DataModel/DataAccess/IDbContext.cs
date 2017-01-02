using System.Linq;

namespace Infostructure.MyBigBro.DataModel.DataAccess
{
    public interface IDbContext
    {
        IQueryable<T> SetWrapper<T>() where T : class;
        int SaveChangesWrapper();
        void Add<T>(T entity) where T : class;
        void Modify<T>(T entity) where T : class;
        void Delete<T>(T entity) where T : class;
    }
}
