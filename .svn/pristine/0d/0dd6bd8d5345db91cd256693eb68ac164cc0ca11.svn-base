using System;
using System.Linq;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.BusinessLogic.Services
{
    public class AccountService : IAccountService
    {
        private Infostructure.MyBigBro.DataModel.DataAccess.IMyBigBroRepository _myBigBroRepository = null;
        
        public IMyBigBroRepository MyBigBroRepository 
        {
            get { return _myBigBroRepository; }
            set { _myBigBroRepository = value; }
        }

        public AccountService() {}

        public AccountService(IMyBigBroRepository myBigBroRepository)
        {
            _myBigBroRepository = myBigBroRepository;
        }

        public Infostructure.MyBigBro.DataModel.Models.User ValidateUser(ICredentials credentials)
        {
            //return new DataModel.Models.User {UserName = "test1"};
            return
                _myBigBroRepository.Set<Infostructure.MyBigBro.DataModel.Models.User>()
                .Where(u => u.UserName == credentials.UserName && u.Password == credentials.Password)
                .FirstOrDefault();
        }

        public Infostructure.MyBigBro.DataModel.Models.User GetUser(string userName)
        {
            throw new NotImplementedException();
        }

        public Infostructure.MyBigBro.DataModel.Models.User CreateUser(Infostructure.MyBigBro.Domain.IUser user)
        {
            throw new NotImplementedException();
        }
    }
}
