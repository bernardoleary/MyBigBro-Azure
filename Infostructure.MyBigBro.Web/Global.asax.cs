//using System.Data.Entity;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Infostructure.MyBigBro.Web.ApiExceptions;

//using Microsoft.Practices.Unity;

namespace Infostructure.MyBigBro.Web
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void ConfigureApi(HttpConfiguration config)
        {
//            var unity = new UnityContainer();

//            // Register services.
//            unity.RegisterType<IGeoMarkerService, GeoMarkerService>();
//            unity.RegisterType<IAccountService, AccountService>();
//            unity.RegisterType<IFormsAuthenticationService, FormsAuthenticationService>();
            
//            // Register Web API controllers.
//            unity.RegisterType<IController, LoginController>();
//            unity.RegisterType<GeoMarkersController>();
//            unity.RegisterType<WebCamsController>();
//            unity.RegisterType<CapturedImagesGeoMarkerController>();

//            // Register database components.
//            var settings = new Settings();
//            var connectionString = "";
//#if DEBUG
//            connectionString = settings.connectionStringLocal;
//#else
//            connectionString = settings.connectionStringHosting;
//#endif
//            unity.RegisterType<IMyBigBroRepository, MyBigBroRepository>(
//                new InjectionConstructor(new MyBigBroContext(connectionString)));            

//            // Initialise IoC.
//            config.DependencyResolver = new IoCContainer(unity);

            // Initialization of our Unity container
            Bootstrapper.Initialise();
        } 

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            WebApiExceptionConfig.RegisterExceptions(GlobalConfiguration.Configuration);
            //ConfigureApi(GlobalConfiguration.Configuration);

            // Initialization of our Unity container
            Bootstrapper.Initialise();
            BootstrapSupport.BootstrapBundleConfig.RegisterBundles(System.Web.Optimization.BundleTable.Bundles);
            BootstrapMvcSample.ExampleLayoutsRouteConfig.RegisterRoutes(RouteTable.Routes);
            //BootstrapSupport.BootstrapBundleConfig.RegisterBundles(System.Web.Optimization.BundleTable.Bundles);
            //BootstrapMvcSample.ExampleLayoutsRouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}