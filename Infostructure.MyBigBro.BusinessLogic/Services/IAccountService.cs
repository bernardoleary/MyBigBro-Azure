using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.BusinessLogic.Services
{
    public interface IAccountService
    {
        IMyBigBroRepository MyBigBroRepository { get; set; }
        Infostructure.MyBigBro.DataModel.Models.User ValidateUser(ICredentials credentials);
        Infostructure.MyBigBro.DataModel.Models.User GetUser(string userName);
    }
}
