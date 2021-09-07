using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;

using DanielsToolbox.Extensions;
using System.Net;

namespace DanielsToolbox.Models.CommandLine.XRMFramework
{
    public class DocumentPluginsCommandLine
    {
        public FileInfo PluginAssemblyPath { get; init; }

        public string PersonalAccessToken { get; init; }

        public string OrganizationName { get;init;  }
        public string ProjectName { get;init; }
        public string WikiName { get; init; }
        public string ParentPageName { get; init; }

        public static IEnumerable<Symbol> Arguments() 
            => new Symbol[]
            {
                new Argument<FileInfo>("pluginassemblypath", "Path to plugin assembly").ExistingOnly(),
                new Argument<string>("personalaccesstoken", "Personal access token"),
                new Argument<string>("organizationname", "Organization name"),
                new Argument<string>("projectname", "Project name"),
                new Argument<string>("wikiname", "Name of wiki"),
                new Argument<string>("parentpagename", "Name of parent Page for doc")
            };


        public static Command Create()
        {
            var command = new Command("generate-plugin-doc", "Generates documentation for specified plugin assembly")
            {
                Arguments()
            };

            command.Handler = CommandHandler.Create<DocumentPluginsCommandLine>(async handler => await handler.DocumentPlugins());

            return command;
        }

        private async Task DocumentPlugins()
        {
            var assembly = new PluginAssembly(PluginAssemblyPath.FullName);

            var client = CreateClient();

            var parentPageResponse = await CreateOrUpdatePage(ParentPageName, new WikiPage { Content = ParentPageName }, client);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parentWiki = await JsonSerializer.DeserializeAsync<WikiPage>(await parentPageResponse.Content.ReadAsStreamAsync(), options);

            var stringJson = await parentPageResponse.Content.ReadAsStringAsync();

            foreach (var plugin in assembly.Plugins)
            {

                var wikiContent = "# " + plugin.FullName + Environment.NewLine;

                wikiContent += plugin.ExtensionDescription + Environment.NewLine;

                wikiContent += "## Plugin steps" + Environment.NewLine;

                foreach (var step in plugin.PluginSteps)
                {
                    wikiContent += "### " + step.Name + Environment.NewLine;
                    wikiContent += step.Description + Environment.NewLine;
                    wikiContent += "|Property|Value|" + Environment.NewLine; 
                    wikiContent += "|--------|-----|" + Environment.NewLine; 
                    wikiContent += $"|Message|{step.Message}" + Environment.NewLine;
                    wikiContent += $"|Triggering entity|{step.TriggerOnEntity}" + Environment.NewLine;
                    wikiContent += $"|Mode|{step.Mode}" + Environment.NewLine;
                    wikiContent += $"|Stage|{step.Stage}" + Environment.NewLine;
                    wikiContent += $"|Filtering attributes|{string.Join(", ", step.FilteringAttributes ?? Enumerable.Empty<string>())}" + Environment.NewLine;
                    wikiContent += $"|Pre image|{string.Join(", ", step.EntityImages.SingleOrDefault(t => t.EntityImageType == PluginStepImage.ImageType.PreImage)?.PreEntityImageAttributes.OrderBy(t => t) ?? Enumerable.Empty<string>())}" + Environment.NewLine;
                    wikiContent += $"|Post image|{string.Join(", ", step.EntityImages.SingleOrDefault(t => t.EntityImageType == PluginStepImage.ImageType.PostImage)?.PostEntityImageAttributes.OrderBy(t => t) ?? Enumerable.Empty<string>())}" + Environment.NewLine;
                    wikiContent += $"|AsyncAutoDelete|{step.AsyncAutoDelete}" + Environment.NewLine;
                    wikiContent += $"|Rank|{step.Rank}" + Environment.NewLine;
                    wikiContent += $"|Supported deployment|{step.SupportedDeployment}" + Environment.NewLine;
                }

                var childWikiPage = new WikiPage
                {
                    Content = wikiContent
                };

                var updateChildPageResponse = await CreateOrUpdatePage($"{parentWiki.Path}/{plugin.TypeName}", childWikiPage, client);

                var childResponse = await updateChildPageResponse.Content.ReadAsStringAsync();

                updateChildPageResponse.EnsureSuccessStatusCode();
            }


            await CreateOrUpdatePage(ParentPageName, parentWiki, client);

           

            await Task.CompletedTask;
        }

        private async Task<HttpResponseMessage> CreateOrUpdatePage(string path, WikiPage page, HttpClient client)
        {
            HttpResponseMessage createOrUpdateResponse = null;

            var pageContent = JsonSerializer.Serialize(page);

            var getPageResponse = await client.GetAsync("?path=" + path);

            var pageResponse = await getPageResponse.Content.ReadAsStringAsync();

            if (getPageResponse.IsSuccessStatusCode)
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"?path={path}&api-version=6.0")
                {
                    Content = new StringContent(pageContent, Encoding.Default, "application/json")
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
