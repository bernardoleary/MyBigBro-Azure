using System;

namespace Infostructure.MyBigBro.Domain.Factory
{
    public class DomainModelFactory
    {
        public T GetObject<T>(params object[] args)
        {
            return (T)Activator.CreateInstance(typeof(T), args);
        }
    }
}
