using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using System.Text.Json;

using DanielsToolbox.Extensions;
using DanielsToolbox.Managers;
using DanielsToolbox.Models.CommandLine.AzureDevops;

namespace DanielsToolbox.Models.CommandLine.XRMFramework
{
    public class DocumentPluginsCommandLine
    {
        public FileInfo PluginAssemblyPath { get; init; }

        public DevOpsWikiClientCommandLine DevOpsWikiClient { get; init; }

        public static IEnumerable<Symbol> Arguments() 
            => new Symbol[]
            {
                new Argument<FileInfo>("pluginassemblypath", "Path to plugin assembly").ExistingOnly()
            };


        public static Command Create()
        {
            var command = new Command("generate-plugin-doc", "Generates documentation for specified plugin assembly")
            {
                Arguments(),
                DevOpsWikiClientCommandLine.Arguments()
            };

            command.Handler = CommandHandler.Create<DocumentPluginsCommandLine>(async handler => await handler.DocumentPlugins());

            return command;
        }

        private async Task DocumentPlugins()
        {
            var assembly = new PluginAssembly(PluginAssemblyPath.FullName);

            var parentPageResponse = await DevOpsWikiClient.CreateOrUpdatePage(DevOpsWikiClient.ParentPageName, new WikiPage { Content = DevOpsWikiClient.ParentPageName });

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parentWiki = await JsonSerializer.DeserializeAsync<WikiPage>(await parentPageResponse.Content.ReadAsStreamAsync(), options);

            var stringJson = await parentPageResponse.Content.ReadAsStringAsync();

            foreach (var plugin in assembly.Plugins)
            {
                Console.WriteLine("Generating documentation for " + plugin.TypeName);

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

                var updateChildPageResponse = await DevOpsWikiClient.CreateOrUpdatePage($"{parentWiki.Path}/{plugin.TypeName}", childWikiPage);

                var childResponse = await updateChildPageResponse.Content.ReadAsStringAsync();

                updateChildPageResponse.EnsureSuccessStatusCode();
            }


            await DevOpsWikiClient.CreateOrUpdatePage(DevOpsWikiClient.ParentPageName, parentWiki);

           

            await Task.CompletedTask;
        }
    }
}
