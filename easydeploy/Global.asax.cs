using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace easydeploy
{
    public class MvcApplication : HttpApplication
    {
        private DataManager dataManagerInstance = null;

        protected void Application_Start()
        {
            TelemetryConfiguration.Active.InstrumentationKey = ConfigurationManager.AppSettings["instrumentationkey"];

            dataManagerInstance = new DataManager();

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}