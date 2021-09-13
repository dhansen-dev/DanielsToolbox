
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DanielsToolbox.Models.CommandLine.AzureDevops
{
    public class DevOpsWikiClientCommandLine
    {
        public string PersonalAccessToken { get; init; }
        public string OrganizationName { get; init; }
        public string ProjectName { get; init; }
        public string WikiName { get; init; }
        public string ParentPageName { get; init; }

        private HttpClient _client;

        private HttpClient WikiClient => CreateClient();

        private HttpClient CreateClient()
        {
            if (_client is not null)
                return _client;

            _client = new HttpClient()
            {
                BaseAddress = new Uri($"https://dev.azure.com/{OrganizationName}/{ProjectName}/_apis/wiki/wikis/{WikiName}/"),
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.Default.GetBytes($":{PersonalAccessToken}")))
                }
            };

            return _client;
        }

        public async Task<WikiPage> GetWikiPage(string path)
        {
            var getPageResponse = await WikiClient.GetAsync("Pages/?path=" + path);

            var wikiContent = await JsonSerializer.DeserializeAsync<WikiPage>(await getPageResponse.Content.ReadAsStreamAsync(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive  = true
            });

            wikiContent.ETag = getPageResponse.Headers.ETag?.Tag;

            return wikiContent;
        }

        public async Task SaveAttachment(string name, string attachment)
        {

            var res = await WikiClient.PutAsync($"attachments?name={name}&api-version=6.0", new StringContent(attachment, Encoding.UTF8, "application/octet-stream"));

            Console.WriteLine(await res.Content.ReadAsStringAsync());
        }


        public async Task<HttpResponseMessage> CreateOrUpdatePage(string path, WikiPage page)
        {
            var pageContent = JsonSerializer.Serialize(page);

            var wikiPage = await GetWikiPage(path);

            HttpResponseMessage createOrUpdateResponse;

            if (wikiPage.ETag is string eTag)
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"Pages/?path={path}&api-version=6.0")
                {
                    Content = new StringContent(pageContent, Encoding.Default, "application/json")
                };

                request.Headers.Add("If-Match", eTag);

                createOrUpdateResponse = await WikiClient.SendAsync(request);

                createOrUpdateResponse.EnsureSuccessStatusCode();
            }
            else
            {
                createOrUpdateResponse = await WikiClient.PutAsync($"Pages/?path={path}&api-version=6.0", new StringContent(pageContent, Encoding.Default, "application/json"));

                var createPageString = await createOrUpdateResponse.Content.ReadAsStringAsync();

                createOrUpdateResponse.EnsureSuccessStatusCode();
            }

            return createOrUpdateResponse;
        }


        public static IEnumerable<Symbol> Arguments()
            => new Symbol[]
                {
                    new Argument<string>("personalaccesstoken", "Personal access token"),
                    new Argument<string>("organizationname", "Organization name"),
                    new Argument<string>("projectname", "Project name"),
                    new Argument<string>("wikiname", "Name of wiki"),
                    new Argument<string>("parentpagename", "Name of parent Page for doc")
                };
    }

    public class WikiRequest
    {
        public string PersonalAccessToken { get; init; }
        public object OrganizationName { get; internal set; }
        public object ProjectName { get; internal set; }
        public object WikiName { get; internal set; }
    }
}
