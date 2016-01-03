using System.Linq;

namespace Infostructure.MyBigBro.DataModel.DataAccess
{
    public interface IMyBigBroRepository
    {
        IQueryable<T> Set<T>() where T : class;
        int SaveChanges();
        void Add<T>(T entity) where T : class;
        void Delete<T>(T entity) where T : class;
        void Modify<T>(T entity) where T : class;
    }
}
