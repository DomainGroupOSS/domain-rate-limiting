using System.Web.Http;
using Owin;

namespace Domain.RateLimiting.Samples.Owin
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            HttpConfiguration httpConfiguration = new HttpConfiguration();
            WebApiConfig.Register(httpConfiguration);
            FilterConfig.ConfigureRateLimiting(httpConfiguration.Filters);
        
            appBuilder.UseWebApi(httpConfiguration);
        }
    }
}