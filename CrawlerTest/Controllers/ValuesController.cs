using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrawlerTest.Model;
using CrawlerTest.Repository;
using Microsoft.AspNetCore.Mvc;

namespace CrawlerTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly ICrawler _ICrawler;
        public ValuesController(ICrawler ICrawler)
        {
            _ICrawler = ICrawler;
        }
        // GET api/values
        [HttpGet]
        public async Task<ActionResult> Get([FromQuery] string url)
        {       
            if(url != ""){
                var response = await _ICrawler.startWithLanding(url);
                return Ok(response);
            }else
            {
                return Ok("Please pass some url to crawl" );
            }
           
            
            return Ok(new ReturnModel{ Message =  });
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
