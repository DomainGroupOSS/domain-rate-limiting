using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.WebHost;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Domain.RateLimiting.Samples.WebApi.Controllers
{
    public class GloballyLimitedController : ApiController
    {
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }
        
        public string Get(int id)
        {
            return "value";
        }
        
        [Route("api/GloballyLimited/{id}/sub/{subid}")]
        public string Get(int id, int subid)
        {
            return "value from sub";
        }

        [HttpGet]
        [Route("api/GloballyLimited/FetchMyStuff/{id}")]
        public string FetchMyThang(int id)
        {
            return "Here is your thang";
        }

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
