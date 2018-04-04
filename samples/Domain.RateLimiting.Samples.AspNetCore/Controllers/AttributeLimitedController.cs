using System.Collections.Generic;
using Domain.RateLimiting.Core;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Domain.RateLimiting.Samples.AspNetCore.Controllers
{
    [Route("api/[controller]")]
    [AllowedConsumptionRate(20, RateLimitUnit.PerMinute)]
    public class AttributeLimitedController : Controller
    {
        // GET: api/values
        [HttpGet]
        // Since no rate limiting policies are mentioned this will be limited
        // by globally stated limits if any
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        [AllowedConsumptionRate(25, RateLimitUnit.PerHour)]
        [AllowedConsumptionRate(15, RateLimitUnit.PerMinute)]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
