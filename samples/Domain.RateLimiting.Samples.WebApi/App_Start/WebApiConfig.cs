using System.Web.Http;

namespace Domain.RateLimiting.Samples.WebApi
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}"
            );

            config.Routes.MapHttpRoute(
                name: "DefaultApiWithId",
                routeTemplate: "api/{controller}/{id}"
            );
        }
    }
}
