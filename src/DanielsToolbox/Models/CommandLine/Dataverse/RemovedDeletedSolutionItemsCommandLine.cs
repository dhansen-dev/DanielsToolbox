using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;

using DanielsToolbox.Extensions;
using DanielsToolbox.Helpers;
using DanielsToolbox.Models.CommandLine.PowerAutomate;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class RemovedDeletedSolutionItemsCommandLine
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

        public FileInfo SolutionFile { get; init; }

        public Uri SourceEnvironmentUri { get; init; }   

        public bool UseSourceEnvironment { get; init; }

        public string SolutionName { get; init;  }

        public string SearchPattern { get; init;  }

        public bool Publish { get; init; }

        public bool IncludeWebResources { get; init; }

        public bool IncludeConnectionReferences { get; init; }

        public bool IncludeFlows { get; init; }

        public static IEnumerable<Symbol> Arguments()
         => new Symbol[]
         {
             new Argument<string>("solutionName", "Name of solution containing webresources"),
             new Option<FileInfo>("--solution-file", "Path to solution containing web resources").ExistingOnly(),
             new Option<string>("--search-pattern", () => "*.*"),
             new Option<bool>("--publish", () => true, "Publish updated webresources"),
             new Option<Uri>("--source-environment-uri"),
             new Option<bool>("--use-source-environment"),
             new Option<bool>("--include-web-resources", () => true),
             new Option<bool>("--include-connection-references", () => true),
             new Option<bool>("--include-flows", () => true)
         };

        public static Command Create()
        {
            var command = new Command("remove-deleted-solution-items")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                Arguments()
            };

            command.Handler = CommandHandler.Create<RemovedDeletedSolutionItemsCommandLine>(async handler => await handler.RemovedDeletedSolutionItems());

            return command;
        }

        private async Task RemovedDeletedSolutionItems()
        {
            var client = DataverseServicePrincipalCommandLine.Connect();

            Guid solutionId = await GetSolutionId(client, SolutionName);

            IEnumerable<SolutionComponentIdentifier> localWebResources = Enumerable.Empty<SolutionComponentIdentifier>(); ;
            IEnumerable<SolutionComponentIdentifier> localConnectionReferences = Enumerable.Empty<SolutionComponentIdentifier>(); ;
            IEnumerable<SolutionComponentIdentifier> localFlows = Enumerable.Empty<SolutionComponentIdentifier>(); ;

            if (!UseSourceEnvironment)
            {
                using var zip = ZipFile.OpenRead(SolutionFile.FullName);

                var xdoc = XDocument.Load(zip.Entries.Single(entry => entry.Name == "customizations.xml").Open());
                localWebResources = IncludeWebResources ? xdoc.Root.XPathSelectElements("WebResources/WebResource").Select(wr => new SolutionComponentIdentifier
                {
                    Id = Guid.Parse(wr.Element("WebResourceId").Value),
                    Name = wr.Element("Name").Value
                }) : localWebResources;

                localConnectionReferences = IncludeConnectionReferences ? xdoc.Root.XPathSelectElements("connectionreferences/connectionreference").Select(wr => new SolutionComponentIdentifier
                {
                    Name = wr.Attribute("connectionreferencelogicalname").Value
                }) : localConnectionReferences;

                localFlows = IncludeFlows ? xdoc.Root.XPathSelectElements("Workflows/Workflow[Category = 5 and Type = 1]").Select(flow => new SolutionComponentIdentifier
                {
                    Id = Guid.Parse(flow.Attribute("WorkflowId").Value),
                    Name = flow.Attribute("Name").Value
                }) : localFlows;
            }
            else
            {
                var sourceClient = DataverseServicePrincipalCommandLine.ConnectTo(SourceEnvironmentUri);

                var sourceSolutionId = await GetSolutionId(sourceClient, SolutionName);

                localWebResources = IncludeWebResources ? GetExistingWebResourcesInSolution(sourceClient, sourceSolutionId) : localWebResources;
                localConnectionReferences = IncludeConnectionReferences ? await GetExistingConnectionReferencesInSolution(sourceClient, sourceSolutionId) : localConnectionReferences;
                localFlows = IncludeFlows ? QueryHelper.GetModernWorkFlowsFromSolution(sourceClient, SolutionName)
                                        .Select(flow => new SolutionComponentIdentifier { Id = flow.Id, Name = flow.Name, }) : localFlows;
            }

            var remoteWebResources = GetExistingWebResourcesInSolution(client, solutionId);

            var remoteWebResourcesToDelete = remoteWebResources.Except(localWebResources);

            foreach (var webResourceToDelete in remoteWebResourcesToDelete)
            {
                Console.WriteLine("Will delete " + webResourceToDelete.Name);

                client.Delete("webresource", webResourceToDelete.Id);
            }

            var remoteConnectionReferences = await GetExistingConnectionReferencesInSolution(client, solutionId);

            var connectionReferencesToDelete = remoteConnectionReferences.Except(localConnectionReferences);

            foreach (var connectionReference in connectionReferencesToDelete)
            {
                Console.WriteLine("Will delete " + connectionReference.Name);

                client.Delete("connectionreference", connectionReference.Id);
            }

            var remoteFlows = QueryHelper
                                   .GetModernWorkFlowsFromSolution(client, SolutionName)
                                   .Select(flow => new SolutionComponentIdentifier { Id = flow.Id, Name = flow.Name, });

            var flowsToDelete = remoteFlows.Except(localFlows).ToList();

            Console.WriteLine($"\nFound {flowsToDelete.Count()} flows to delete in target environment");

            foreach (var deletedFlow in flowsToDelete)
            {
                Console.WriteLine($"Deleting flow called {deletedFlow.Name}");


                client.Delete("workflow", deletedFlow.Id);

                Console.WriteLine($"{deletedFlow.Name} deleted");
            }

            static IEnumerable<SolutionComponentIdentifier> GetExistingWebResourcesInSolution(ServiceClient client, Guid solutionId)
            {
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

                var remoteWebResources = remoteUnpublishedWebResourcesEntities.Select(e => new SolutionComponentIdentifier
                {
                    Id = e.GetAttributeValue<Guid>("webresourceid"),
                    Name = e.GetAttributeValue<string>("name")
                });
                return remoteWebResources;
            }

            static async Task<IEnumerable<SolutionComponentIdentifier>> GetExistingConnectionReferencesInSolution(ServiceClient client, Guid solutionId)
            {
                var solutionComponentQuery = new QueryExpression("connectionreference")
                {
                    ColumnSet = new ColumnSet(true)
                };

                var connectionReferenceLink = solutionComponentQuery.AddLink("solutioncomponent", "connectionreferenceid", "objectid");
                connectionReferenceLink.EntityAlias = "sc";
                connectionReferenceLink.Columns.AllColumns = true;
                connectionReferenceLink.LinkCriteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);

                var remoteConnectionReferencesEntities = (await client.RetrieveMultipleAsync(solutionComponentQuery)).Entities;

                var remoteConnectionReferences = remoteConnectionReferencesEntities.Select(e => new SolutionComponentIdentifier
                {
                    Id = e.GetAttributeValue<Guid>("connectionreferenceid"),
                    Name = e.GetAttributeValue<string>("connectionreferencelogicalname")
                });

                return remoteConnectionReferences;
            }

            static async Task<Guid> GetSolutionId(ServiceClient client, string solutionName)
            {
                var solutionIdQuery = new QueryExpression("solution")
                {
                    ColumnSet = new ColumnSet(true)
                };

                solutionIdQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, solutionName);

                var solutionIdQueryResult = (await client.RetrieveMultipleAsync(solutionIdQuery));

                var solutionId = solutionIdQueryResult.Entities[0].Id;
                return solutionId;
            }
        }



        private class SolutionComponentIdentifier : IEquatable<SolutionComponentIdentifier>
        {
            public Guid Id { get; set; }
            public string Name { get; set; }

            public bool Equals(SolutionComponentIdentifier other)
                => Name == other?.Name;

            public override bool Equals(object obj)
                => Equals(obj as SolutionComponentIdentifier);

            public override int GetHashCode()
                => HashCode.Combine(Name);
        }
    }
}
