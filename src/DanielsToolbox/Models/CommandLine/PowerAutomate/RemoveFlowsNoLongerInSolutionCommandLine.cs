using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

using System;
using System.CommandLine.Invocation;
using System.CommandLine;
using System.Linq;

using DanielsToolbox.Extensions;
using DanielsToolbox.Models.CommandLine.Dataverse;
using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Generic;
using DanielsToolbox.Helpers;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Xml.XPath;

namespace DanielsToolbox.Models.CommandLine.PowerAutomate
{
    public class RemoveFlowsNoLongerInSolutionCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; init; }
        public FileInfo SolutionFile { get; init; }

        public string SolutionName { get; init; }

        public static Command Create()
        {
            var command = new Command("synchronize-flows", "Removes flows in target solution that are no longer part of source solution")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                new Argument<string>("solution-name", "Solution name"),    
                new Argument<FileInfo>("solution-file", "Solution file").ExistingOnly()
            };

            command.Handler = CommandHandler.Create<RemoveFlowsNoLongerInSolutionCommandLine>(a => a.SynchronizeFlowsWithSolutionFile());

            return command;
        }

        private class WorkflowIdentitfier : IEquatable<WorkflowIdentitfier>
        {
            public Guid Id { get; set; }
            public string Name { get; set; }

            public bool Equals(WorkflowIdentitfier other)
                => Id == other.Id;

            public override bool Equals(object obj)
                => Equals(obj as WorkflowIdentitfier);

            public override int GetHashCode() 
                => HashCode.Combine(Id);
        }

        public void SynchronizeFlowsWithSolutionFile()
        {
            var client = DataverseServicePrincipalCommandLine.Connect();

            var currentFlowInTarget = QueryHelper
                                    .GetModernWorkFlowsFromSolution(client, SolutionName)
                                    .Select(flow => new WorkflowIdentitfier { Id = flow.Id, Name = flow.Name, });

            using (var zip = ZipFile.OpenRead(SolutionFile.FullName))
            {

                var xdoc = XDocument.Load(zip.Entries.Single(entry => entry.Name == "customizations.xml").Open());
                var flowsInSourceSolution = xdoc.Root.XPathSelectElements("Workflows/Workflow[Category = 5 and Type = 1]").Select(flow => new WorkflowIdentitfier
                {
                    Id = Guid.Parse(flow.Attribute("WorkflowId").Value),
                    Name = flow.Attribute("Name").Value
                });

                var flowsToDelete = currentFlowInTarget.Except(flowsInSourceSolution).ToList();

                Console.WriteLine($"\nFound {flowsToDelete.Count()} flows to delete in target environment");

                foreach (var deletedFlow in flowsToDelete)
                {
                    Console.WriteLine($"Deleting flow called {deletedFlow.Name}");


                    client.Delete("workflow", deletedFlow.Id);

                    Console.WriteLine($"{deletedFlow.Name} deleted");
                }
            }
        }
    }
}
