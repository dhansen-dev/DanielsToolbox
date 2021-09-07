using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;

namespace DanielsToolbox.Models.CommandLine.AzureDevops
{
    public class UpdateDevOpsWikiPageCommandLine
    {
        public string PersonalAccessToken { get; init; }

        public string OrganizationName { get; init; }
        public string ProjectName { get; init; }
        public string WikiName { get; init; }
        public string ParentPagePath { get; init; }

        public string Content { get; set; }


        public static Command Create()
        {
            var command = new Command("update-wiki-page")
            {
                Arguments()
            };

            command.Handler = CommandHandler.Create<UpdateDevOpsWikiPageCommandLine>(async handler => await handler.UpdateWikiPage());

            return command;

        }

        public async Task UpdateWikiPage(string content = null)
        {
            var client = CreateClient();

            await CreateOrUpdatePage(ParentPagePath, content ?? Content, client);
            
        }

        private static IEnumerable<Symbol> Arguments()
            => new Symbol[]
            {
                new Argument<string>("personalaccesstoken", "Personal access token"),
                new Argument<string>("organizationname", "Organization name"),
                new Argument<string>("projectname", "Project name"),
                new Argument<string>("wikiname", "Name of wiki"),
                new Argument<string>("parentpagepath", "Name of parent Page for doc"),
                new Argument<string>("content", "Content to add or update page with")
            };


        private async Task<HttpResponseMessage> CreateOrUpdatePage(string path, string pageContent, HttpClient client)
        {
            HttpResponseMessage createOrUpdateResponse = null;

            var wikiPage = new WikiPage
            {
                Content = pageContent,
            };

            var pageContentJson = JsonSerializer.Serialize(wikiPage);

            var getPageResponse = await client.GetAsync("?path=" + path);

            var pageResponse = await getPageResponse.Content.ReadAsStringAsync();

            if (getPageResponse.IsSuccessStatusCode)
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"?path={path}&api-version=6.0")
                {
                    Content = new StringContent(pageContentJson, Encoding.Default, "application/json")
                };

                request.Headers.Add("If-Match", getPageResponse.Headers.ETag.Tag);

                createOrUpdateResponse = await client.SendAsync(request);

                createOrUpdateResponse.EnsureSuccessStatusCode();
            }
            else
            {
                createOrUpdateResponse = await client.PutAsync($"?path={path}&api-version=6.0", new StringContent(pageContent, Encoding.Default, "application/json"));

                var createPageString = await createOrUpdateResponse.Content.ReadAsStringAsync();

                createOrUpdateResponse.EnsureSuccessStatusCode();
            }

            return createOrUpdateResponse;
        }

        private HttpClient CreateClient()
            => new()
            {
                BaseAddress = new Uri($"https://dev.azure.com/{OrganizationName}/{ProjectName}/_apis/wiki/wikis/{WikiName}/pages/"),
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.Default.GetBytes($":{PersonalAccessToken}")))
                }
            };
    }
}
