﻿using System.Linq;

namespace Infostructure.MyBigBro.DataModel.DataAccess
{
    public class MyBigBroRepository : IMyBigBroRepository
    {
        private IDbContext _context;

        public MyBigBroRepository() {}

        public MyBigBroRepository(IMyBigBroContext context)
        {
            _context = context;
        }

        public IDbContext Context
        {
            get { return _context; }
            set { _context = value; }
        }

        public IQueryable<T> Set<T>() where T : class
        {
            return _context.SetWrapper<T>();
        }

        public int SaveChanges()
        {
            return _context.SaveChangesWrapper();
        }

        public void Add<T>(T entity) where T : class
        {
            _context.Add<T>(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Delete<T>(entity);
        }

        public void Modify<T>(T entity) where T : class
        {
            _context.Modify<T>(entity);
        }
    }
}
