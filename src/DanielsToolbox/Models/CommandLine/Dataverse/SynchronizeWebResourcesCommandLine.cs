using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;


using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class SynchronizeWebResourcesCommandLine
    {
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

            var solutionComponentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet(true)
            };

            solutionComponentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 61);
            solutionComponentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

            var webResourceLink = solutionComponentQuery.AddLink("webresource", "objectid", "webresourceid");
            webResourceLink.EntityAlias = "webresource";
            webResourceLink.Columns.AllColumns = true;

            var remoteWebResourcesEntities = (await client.RetrieveMultipleAsync(solutionComponentQuery)).Entities;
            
            var remoteWebResources = remoteWebResourcesEntities.ToDictionary(e => ((string)(e.GetAttributeValue<AliasedValue>("webresource.name")).Value).ToLowerInvariant());
            
            var remoteWebResourcesToDelete = remoteWebResources.Select(remote => remote.Key).Except(localWebResources.Select(local => local.Key));

            foreach(var webResourceToDelete in remoteWebResourcesToDelete)
            {
                Console.WriteLine("Wil delete " + webResourceToDelete);
            }

            var localWebResourcesToAdd = localWebResources.Select(local => local.Key).Except(remoteWebResources.Select(remote => remote.Key));

            foreach(var webResourceToAdd in localWebResourcesToAdd)
            {
                Console.WriteLine("Will add " + webResourceToAdd);
            }

            var updatedWebResources = from localWebResource in localWebResources
                                      from remoteWebResource in remoteWebResources
                                      where localWebResource.Key == remoteWebResource.Key
                                      where localWebResource.Value.Content != remoteWebResource.Value.GetAliasedValue<string>("webresource.content")
                                      select localWebResource.Value;

            foreach(var updatedWebResource in updatedWebResources)
            {
                Console.WriteLine("Will update " + updatedWebResource.WebResourceName);
            }


        }
    }
}
