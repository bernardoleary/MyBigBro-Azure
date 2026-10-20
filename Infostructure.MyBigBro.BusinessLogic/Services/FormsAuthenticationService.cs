using System;

namespace Infostructure.MyBigBro.BusinessLogic.Services
{
    public class FormsAuthenticationService : IFormsAuthenticationService
    {
        public string CurrentUserName { get; private set; }
        public bool IsPersistent { get; private set; }

        public void SignIn(string userName, bool createPersistentCookie)
        {
            if (String.IsNullOrEmpty(userName))
                throw new ArgumentException("Value cannot be null or empty.", "userName");

            CurrentUserName = userName;
            IsPersistent = createPersistentCookie;
        }

        public void SignOut()
        {
            CurrentUserName = null;
            IsPersistent = false;
        }
    }
}
