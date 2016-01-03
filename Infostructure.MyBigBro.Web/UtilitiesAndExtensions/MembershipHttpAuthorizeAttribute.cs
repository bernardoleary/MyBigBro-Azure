using System.Security.Principal;
using System.Web.Security;

namespace Infostructure.MyBigBro.Web.UtilitiesAndExtensions
{
    public class MembershipHttpAuthorizeAttribute : BasicHttpAuthorizeAttribute
    {
        /// <summary>
        /// Implement to include authentication logic and create IPrincipal
        /// </summary>
        protected override bool TryCreatePrincipal(string user, string password,
            out IPrincipal principal)
        {
            principal = null;
            if (!Membership.Provider.ValidateUser(user, password))
                return false;
            string[] roles = System.Web.Security.Roles.Provider.GetRolesForUser(user);
            principal = new GenericPrincipal(new GenericIdentity(user), roles);
            return true;
        }
    }
}