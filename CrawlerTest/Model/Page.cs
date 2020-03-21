using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CrawlerTest.Model
{
    public class Page
    {
        public string Name { get; set; }
        public string Link { get; set; }
        public List<string> SubLinks { get; set; }
    }
}
