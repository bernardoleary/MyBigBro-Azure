using System.Web.Http;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using Infostructure.MyBigBro.Web2.Controllers;
using Infostructure.MyBigBro.Web2.Properties;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Microsoft.Practices.Unity;

namespace Infostructure.MyBigBro.Web2
{
    public static class Bootstrapper
    {
        public static void Initialise()
        {
            var container = BuildUnityContainer();
            GlobalConfiguration.Configuration.DependencyResolver = new Unity.WebApi.UnityDependencyResolver(container);
        }

        private static IUnityContainer BuildUnityContainer()
        {
            var container = new UnityContainer();

            // Register repository.
            container.RegisterType<IMyBigBroRepository, MyBigBroRepository>(
                new InjectionConstructor(new MyBigBroContext(GetConnectionString()))); 

            // Register services.     
            container.RegisterType<ILocation, Location>();
            container.RegisterType<IStorageServiceAgent, AzureStorageServiceAgent>();
            container.RegisterType<ILocation, Location>();
            container.RegisterType<IWebCamControl, WebCamControl>();
            container.RegisterType<IGeoMarkerService, GeoMarkerService>();
            container.RegisterType<IAccountService, AccountService>();
            container.RegisterType<IFormsAuthenticationService, FormsAuthenticationService>();

            // Register Web API controllers.
            //container.RegisterType<LoginController>();
            container.RegisterType<GeoMarkersController>();
            container.RegisterType<CapturedImagesGeoMarkerController>();    
            container.RegisterType<WebCamsController>(); 

            // Return the populated container.
            return container;
        }

        public static string GetConnectionString()
        {
            // Register database components.
            var settings = new Settings();
            var connectionString = "";
#if DEBUG
            connectionString = settings.connectionStringLocal;
#else
            connectionString = settings.connectionStringAzure;
#endif
            return connectionString;
        }
    }
}