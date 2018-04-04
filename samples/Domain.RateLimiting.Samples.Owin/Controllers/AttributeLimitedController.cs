using System.Collections.Generic;
using System.Web.Http;
using Domain.RateLimiting.Core;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Domain.RateLimiting.Samples.Owin.Controllers
{
    //System.Web.Http.Route("api/[controller]")]
    [AllowedConsumptionRate(20, RateLimitUnit.PerMinute)]
    public class AttributeLimitedController : ApiController
    {
        // Since no rate limiting policies are mentioned this will be limited
        // by globally stated limits if any
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }
        
        [AllowedConsumptionRate(25, RateLimitUnit.PerHour)]
        [AllowedConsumptionRate(15, RateLimitUnit.PerMinute)]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [System.Web.Http.HttpPost]
        public void Post([FromBody]string value)
        {
        }
        
        public void Put(int id, [FromBody]string value)
        {
        }
        
        public void Delete(int id)
        {
        }
    }
}
