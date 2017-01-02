using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace Infostructure.MyBigBro.Web3
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            Bootstrapper.Initialise();
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
