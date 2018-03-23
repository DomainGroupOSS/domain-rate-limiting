using System.Collections.Generic;
using System.Web.Http;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Domain.RateLimiting.Samples.Owin.Controllers
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

        [Route("api/globallylimited/{id}/sub/{subid}")]
        public string Get(int id, int subid)
        {
            return "value from sub";
        }

        [Route("api/globallylimited/{id}/sub/{subid}/test/{testid}")]
        public string Get(int id, int subid, int testid)
        {
            return "value from sub test";
        }

        [HttpGet]
        [Route("api/globallylimited/{id}/free")]
        public string Free(int id)
        {
            return "value";
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
