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

namespace CrawlerTest.Repository
{
    public interface ICrawler
    {
        Task<List<Page>> startWithLanding(string url);
        bool RemoteFileExists(string url);

        Task<bool> writeToFile(string name, string data);
    }
    public class Crawler : ICrawler
    {
        private readonly IHostingEnvironment _environment;
        private readonly string _folder;
        private readonly string _jsonFolder;
        private List<Page> pages = new List<Page>();
        private List<string> _siteUrls = new List<string>();
        
        private int fileName = 0;
        
        public Crawler(IHostingEnvironment environment)
        {
            _environment = environment;
            _folder = "CrawledFiles";
            _jsonFolder = "JsonFiles";
        }
        public async Task<List<Page>> startWithLanding(string url)
        {
            string baseurl = url;
            if(url == "")
            {
                return pages;
            }

            // Here i can also the conditions for robots.txt file, if the specific url is not allowed then skip it to crawl.

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
            foreach (var page in pageData.SubLinks)
            {
                if (RemoteFileExists(page))
                {
                    await getHtmlOfPage(page, baseurl);
                }
            }
        }


        // cleaning the title for the file name through regex operations
        private string getFileName(string url, string baseurl)
        {
            string name;
            if(url != baseurl)
            {
                name = Regex.Replace(url, baseurl, "");
            }
            else
            {
                name = Regex.Replace(url, "https://", "");
                name = Regex.Replace(name, ".com", "");
                name = Regex.Replace(name, ".de", "");
                name = Regex.Replace(name, "[^\\w\\._]", "");
                return name + ".html";
            }
            name = Regex.Replace(url, "https://", "");
            name = Regex.Replace(name, ".com", "");
            name = Regex.Replace(name, ".de", "");
            name = Regex.Replace(url, "[^\\w\\._]", "");
            name = name.Trim();
            name = Regex.Replace(name, @"'[^']+'(?=!\w+)", string.Empty);
            name = Regex.Replace(name, @"\\", "");
            return name + ".html";
        }

        
        // Getting the sub urls for the respective div.
        private List<string> GetSuburls(HtmlNode div, string baseUrl)
        {
            var subNodes = div.Descendants("div");
            var subLinks = div.Descendants("a"); //picking anchorTags
            List<string> _subLinks = new List<string>();
             string value;
            if (subNodes != null && subLinks != null)
            {
                foreach (var link in subLinks)
                {
                    var attribute = link.ChildAttributes("href").FirstOrDefault();
                    if (attribute != null)
                    {
                        value = attribute.Value;
                        if (!_siteUrls.Contains(value))         // overall preventing for getting the duplicate urls.
                        {
                            if (value.Contains(baseUrl))
                            {
                                _siteUrls.Add(value);
                                _subLinks.Add(value);
                            }
                        }
                    }

                }
                foreach (var node in subNodes)
                {
                    Geturls(node, baseUrl);
                }
            }
            return _subLinks;
        }

        private void Geturls(HtmlNode div, string baseUrl)
        {
            var subNodes = div.Descendants("div");
            var subLinks = div.Descendants("a"); //picking anchorTags
            string value;
            if (subNodes != null && subLinks != null)
            {
                foreach (var link in subLinks)
                {
                    var attribute = link.ChildAttributes("href").FirstOrDefault();
                    if (attribute != null)
                    {
                        value = attribute.Value;
                        if (!_siteUrls.Contains(value))
                        {
                            if (value.Contains(baseUrl))
                            {
                                _siteUrls.Add(value);
                            }
                        }
                    }

                }
                foreach (var node in subNodes)
                {
                    Geturls(node, baseUrl);
                }
            }
        }


        // check if the url has some response, sometimes some urls are not working
        public bool RemoteFileExists(string url)
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
        public async Task<bool> writeToFile(string name, string data)
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
