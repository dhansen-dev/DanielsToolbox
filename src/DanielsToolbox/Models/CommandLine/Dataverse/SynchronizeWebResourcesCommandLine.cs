using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class SynchronizeWebResourcesCommandLine
    {

        private Dictionary<string ,int> WebResourceType = new()
        {
            { ".html", 1 },
            { ".htm", 1 },
            { ".css", 2 },
            { ".js", 3 },
            { ".xml", 4 },
            { ".png", 5 },
            { ".jpg", 6 },
            { ".jpeg", 6 },
            { ".gif", 7 },
            { ".xap", 8 },
            { ".xsl", 9 },
            { ".ico", 10 },
            { ".svg", 11 },
            { ".resx", 12 }
        };

        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; init; }
        public DirectoryInfo BaseFolder { get; init; }

        public string SolutionName { get; init;  }

        public string SearchPattern { get; init;  }

        public static IEnumerable<Symbol> Arguments()
         => new Symbol[]
         {
             new Argument<DirectoryInfo>("base-folder", "Base folder for web resources").ExistingOnly(),
             new Argument<string>("solutionName", "Name of solution containing webresources"),
             new Option<string>("--search-pattern", () => "*.*")
         };

        public static Command Create()
        {
            var command = new Command("webresource-sync")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                Arguments()
            };

            command.Handler = CommandHandler.Create<SynchronizeWebResourcesCommandLine>(async handler => await handler.SynchronizeWebResources());

            return command;
        }

        private async Task SynchronizeWebResources()
        {
            var client = DataverseServicePrincipalCommandLine.Connect();

            var solutionIdQuery = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet(true)
            };

            solutionIdQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, SolutionName);

            var solutionIdQueryResult = (await client.RetrieveMultipleAsync(solutionIdQuery));

            var solutionId = solutionIdQueryResult.Entities[0].Id;

            var localWebResources = BaseFolder.EnumerateFileSystemInfos(SearchPattern, new EnumerationOptions
            {
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            })
              .Select(webresource => new { WebresourceName = webresource.FullName[(BaseFolder.FullName.Length + 1)..], FullPath = webresource.FullName })
              .Where(webresource => !webresource.WebresourceName.StartsWith(".") && webresource.WebresourceName != null)
              .Select(webresource => new { WebResourceName = webresource.WebresourceName.Replace("\\", "/").ToLowerInvariant(), Content = Convert.ToBase64String(File.ReadAllBytes(webresource.FullPath)) })
              .ToDictionary(e => e.WebResourceName);



            var solutionComponentQuery = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet(true)
            };

            var webResourceLink = solutionComponentQuery.AddLink("solutioncomponent", "webresourceid", "objectid");
            webResourceLink.EntityAlias = "sc";
            webResourceLink.Columns.AllColumns = true;
            webResourceLink.LinkCriteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            var remoteUnpublishedWebResourcesEntities = ((RetrieveUnpublishedMultipleResponse)client.Execute(new RetrieveUnpublishedMultipleRequest
            {
                Query = solutionComponentQuery
            })).EntityCollection.Entities;

            var remoteWebResources = remoteUnpublishedWebResourcesEntities.ToDictionary(e => e.GetAttributeValue<string>("name").ToLowerInvariant());
            
            var remoteWebResourcesToDelete = remoteWebResources.Select(remote => remote.Key).Except(localWebResources.Select(local => local.Key));

            OrganizationRequestCollection requests = new();

            foreach(var webResourceToDelete in remoteWebResourcesToDelete)
            {
                Console.WriteLine("Will delete " + webResourceToDelete);

                client.Delete("webresource", remoteWebResources[webResourceToDelete].Id);
            }            

            var localWebResourcesToAdd = localWebResources.Select(local => local.Key).Except(remoteWebResources.Select(remote => remote.Key));

            foreach(var webResourceToAdd in localWebResourcesToAdd)
            {
                var webResourceId = await client.CreateAsync(new Entity("webresource")
                {
                    Attributes = {
                            { "name", webResourceToAdd },
                            { "webresourcetype", new OptionSetValue(WebResourceType[Path.GetExtension(webResourceToAdd)]) },
                            { "displayname", webResourceToAdd },
                            { "content", localWebResources[webResourceToAdd].Content }
                    }
                });

                await client.ExecuteAsync(new OrganizationRequest("AddSolutionComponent")
                 {
                     Parameters = new ParameterCollection
                {
                    { "ComponentId", webResourceId },
                    { "ComponentType", 61 },
                    { "SolutionUniqueName", SolutionName },
                    { "AddRequiredComponents", false },
                    { "DoNotIncludeSubcomponents", false }
                }
                 });
                
                Console.WriteLine("Will add " + webResourceToAdd);
            }

            var updatedWebResources = from localWebResource in localWebResources
                                      from remoteWebResource in remoteWebResources
                                      where localWebResource.Key == remoteWebResource.Key
                                      where localWebResource.Value.Content != remoteWebResource.Value.GetAttributeValue<string>("content")
                                      select new
                                      {
                                          WebresourceName = localWebResource.Value.WebResourceName,
                                          WebresourceId = remoteWebResource.Value.GetAttributeValue<Guid>("webresourceid"),
                                          UpdatedContent = localWebResource.Value.Content,
                                          RemoteDisplayName = remoteWebResource.Value.GetAttributeValue<string>("displayname")
                                      };

            foreach(var updatedWebResource in updatedWebResources)
            {
                Console.WriteLine("Will update " + updatedWebResource.WebresourceName);

                await client.UpdateAsync(new Entity("webresource", updatedWebResource.WebresourceId)
                {
                    Attributes =
                    {
                        { "content", updatedWebResource.UpdatedContent }
                    }
                });
            }
        }
    }
}
