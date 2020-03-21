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
        }
    }
}
