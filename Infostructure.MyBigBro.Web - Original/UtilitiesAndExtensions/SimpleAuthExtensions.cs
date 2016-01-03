using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Providers.Entities;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.Web.UtilitiesAndExtensions
{
    public class SimpleAuthExtensions
    {
        /// <summary>
        /// This method has been implemented so as we can refactor the entire application to use the Infostructure.SimpleList.Web.Service.Api class, which takes a userName and password parameter for every call.
        /// This method returns the User object for an authenticated user, whether they have come in through the service or the web front-end.
        /// There is still a bit of a "smell" about this method and some of the authetication architecture, in particular that I'm passing around unencrypted passwords, but it's tollerable for the time being.
        /// Since the API service is accessed directly, there should be no need to use this method where the user is not ASP.NET authenticated.
        /// </summary>
        /// <returns>A Infostructure.MyBigBro.DataModel.Models.User instance if authetication is successful.</returns>
        private Infostructure.MyBigBro.DataModel.Models.User GetUser(IAccountService accountService)
        {
            // Get the username and password off the query string, if they're there.
            ICredentials credentials = new Credentials
                                           {
                                               UserName = HttpContext.Current.Request.QueryString["userName"],
                                               Password = HttpContext.Current.Request.QueryString["password"]
                                           };

            // This is where we would go if we've come in via the service.
            if (credentials.UserName != null && credentials.Password != null)
                return accountService.ValidateUser(credentials);
            // This is where we go if we've come in via the web front-end, since the request will not be ASP.NET authenticated by the service.
            else if (HttpContext.Current.User.Identity.IsAuthenticated)
                return accountService.GetUser(HttpContext.Current.User.Identity.Name);
            // User has not been successfully authenticated.
            else
                return null;
        }
    }
}