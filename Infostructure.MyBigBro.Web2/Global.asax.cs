// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Global.asax.cs" company="">
//   Copyright © 2014 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Web.Http;

namespace Infostructure.MyBigBro.Web2
{
    using System.Web;
    using System.Web.Optimization;
    using System.Web.Routing;

    public class Application : HttpApplication
    {
        protected void Application_Start()
        {
            Bootstrapper.Initialise();
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
