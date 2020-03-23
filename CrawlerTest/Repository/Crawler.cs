using CrawlerTest.Model;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using RobotsTxt;

namespace CrawlerTest.Repository
{
    public interface ICrawler
    {
        Task<List<Page>> startWithLanding(string url);
    }
    public class Crawler : ICrawler
    {
        private readonly IHostingEnvironment _environment;
        private readonly string _folder;
        private readonly string _jsonFolder;
        private List<Page> pages = new List<Page>();
        private List<string> _siteUrls = new List<string>();
        private List<string> _robotUrls = new List<string>();
        private Robots robots;
        private bool robotExits;
        private int fileName = 0;
        
        public Crawler(IHostingEnvironment environment)
        {
            _environment = environment;
            _folder = "CrawledFiles";
            _jsonFolder = "JsonFiles";
        }
        public async Task<List<Page>> startWithLanding(string url)
        {
            string pattern = @"^((https|http|www)://)?(\w+).(de|com|ch|net)/$";
            if (!Regex.IsMatch(url, pattern))
            {
                url = url + "/";
            }

            string baseurl = url;

            // Here i can also the conditions for robots.txt file, if the specific url is not allowed then skip it to crawl.
            string robotUrl = Path.Combine(url , "robots.txt");



            if (RemoteFileExists(robotUrl))
            {
                robotExits = true;
                HttpClient client = new HttpClient();
                string result = await client.GetStringAsync(robotUrl);

                robots = Robots.Load(result); // Parses the robots.txt file's content.
                // You can also use the constructor. (eg: Robots robots = new Robots(content);)

                // To check if the file allows you to request a path :
                bool canIGoThere = robots.IsPathAllowed(".netApplication", robotUrl);
            }
            else
            {
                robotExits = false;
            }

            // calling the landing page and then traverse
            await getHtmlOfPage(url, baseurl);

            // write link to json
            await writeJsonFile();

            return pages;
        }


        private async Task getHtmlOfPage(string url, string baseurl)
        {
            var httpclient = new HttpClient();
            var html = await httpclient.GetStringAsync(url);
            var documentHtml = new HtmlDocument();
            documentHtml.LoadHtml(html);
            var divs = documentHtml.DocumentNode.Descendants("div");

            // first try to pick the title from each page, but was getting some garbage data therefore used another technique
            //documentHtml.DocumentNode.Descendants("head").FirstOrDefault().Descendants("Title").FirstOrDefault().InnerText;

            //getting the name of the file from the url.
            string title = getFileName(url, baseurl);

            // if title is null then getting a hardcoded name with the incremental number
            if(title == "")
            {
                title = "file" + fileName.ToString() + ".html";
                fileName++;
            }


            //use this to store the current page details
            Page pageData = new Page
            {
                Name = title,
                Link = url,
            };

            //looping over all divs within one page
            foreach (var div in divs)
            {
                if(pageData.SubLinks == null)
                {
                    pageData.SubLinks = GetSuburls(div, baseurl);
                }
                else
                {
                    pageData.SubLinks.AddRange(GetSuburls(div, baseurl));
                }
            }

            pages.Add(pageData);

            //saving the required file with the name in the staticFiles directory
            var saveFile = await writeToFile(title, html);  //saving the index file
            

            // with the sub links of the current page, go deep into the tree.
            if(pageData.SubLinks != null)
            {
                foreach (var page in pageData.SubLinks)
                {
                    if (RemoteFileExists(page))
                    {
                        await getHtmlOfPage(page, baseurl);
                    }
                }
            }
            
        }


        // cleaning the title for the file name through regex operations
        private string getFileName(string url, string baseurl)
        {
            string name;
            string pattern = @"^((https|http|www)://)?(\w+).(de|com|ch|net)/(#)?";
            string specialCharacterPattern = @"[\s-_#]+";
            string subPage = @"(\w+)/(\w+)";
            string pickLast = @"(\w+/?)$";
            if (Regex.IsMatch(url, pattern) && url != baseurl)
            {
                name = Regex.Replace(url, pattern, "");
                name = Regex.Replace(name, specialCharacterPattern, "");
                if (Regex.IsMatch(name, subPage))
                {
                   string unmached = Regex.Replace(name, pickLast, "");
                   name = Regex.Replace(name, unmached, "");
                }
                name = Regex.Replace(name, @"/", "");
                return name + ".html";
            }
            else
            {
                return "home.html";
            }
        }

        
        // Getting the sub urls for the respective div.
        private List<string> GetSuburls(HtmlNode div, string baseUrl)
        {
            var subNodes = div.Descendants("div");
            var subLinks = div.Descendants("a"); //picking anchorTags
            var header = div.Element("header");
            if (header != null)
            {
                var headerDivs = header.Descendants("div");
                if (headerDivs != null)
                {
                    subNodes = subNodes.Concat(headerDivs);
                }
            }
            List<string> _subLinks = new List<string>();
             string value;
            if (subLinks != null)
            {
                foreach (var link in subLinks)
                {
                    var attribute = link.ChildAttributes("href").FirstOrDefault();
                    if (attribute != null)
                    {
                        value = attribute.Value;
                        if (!_siteUrls.Contains(value) && robotExits && robots.IsPathAllowed(".netApplication", value)
                            || !_siteUrls.Contains(value) && !robotExits)         // overall preventing for getting the duplicate urls.
                        {
                            if (value.Contains(baseUrl))
                            {
                                _siteUrls.Add(value);
                                _subLinks.Add(value);
                            }else if(!Regex.IsMatch(value, @"^(http|https|www)") && !Regex.IsMatch(value , @"^/$") /*!value.Contains("https") && value != "/"*/ )
                            {
                                if(Regex.IsMatch(value, @"^(/|#)")/*value.IndexOf("/") == 0*/)
                                {
                                    value = Regex.Replace(value, @"^(/|#)", "");
                                    //int last = value.Length;
                                    //value = value.Substring(1, last-1);
                                }
                                if (!_siteUrls.Contains(baseUrl + value))
                                {
                                    _siteUrls.Add(baseUrl + value);
                                    _subLinks.Add(baseUrl + value);
                                }  
                            }
                        }
                    }

                }
            }
            if(subNodes != null)
            {
                foreach (var node in subNodes)
                {
                    GetSuburls(node, baseUrl);
                }
            }
            return _subLinks;
        }


        // check if the url has some response, sometimes some urls are not working
        private bool RemoteFileExists(string url)
        {
            try
            {
                HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                request.Timeout = 10000; //set the timeout to 10 seconds
                request.Method = "HEAD"; //Get only the header information

                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    int statusCode = (int)response.StatusCode;
                    if (statusCode >= 100 && statusCode < 400) //Aloowed requests
                    {
                        return true;
                    }
                    else if (statusCode >= 500 && statusCode <= 510) //On Errors
                    {
                        return false;
                    }
                }
            }
            catch (WebException ex)
            {

                return false;

            }
            catch (Exception ex)
            {
                return false;
            }
            return false;
        }


        // write to the file in static folder
        private async Task<bool> writeToFile(string name, string data)
        {
            var rootPath = _environment.ContentRootPath;
            var filepath = Path.Combine(rootPath, _folder);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(filepath, name)))
            {
                await outputFile.WriteAsync(data);
            }
            return true;
        }

        // write to the json file
        private async Task writeJsonFile()
        {
            var rootPath = _environment.ContentRootPath;
            var filepath = Path.Combine(rootPath, _jsonFolder);
            var json = JsonConvert.SerializeObject(pages);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(filepath, "siteurl.json")))
            {
                await outputFile.WriteAsync(json);
            }
        }
    }
}
