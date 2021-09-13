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

namespace DanielsToolbox.Models.CommandLine.PowerAutomate
{
    public class ActivateModernFlowsCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; init; }
        public string SolutionName { get; set; }

        public static Command Create()
        {
            var command = new Command("activate-modern-flows", "Activates all modern flows that are turned of")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                new Argument<string>("solution-name", "In what solution does the modern flows live")
            };

            command.Handler = CommandHandler.Create<ActivateModernFlowsCommandLine>(a => a.ActivateModernFlows());

            return command;
        }

        public void ActivateModernFlows()
        {
            var client = DataverseServicePrincipalCommandLine.Connect();

            var inactiveFlows = QueryHelper
                                    .GetModernWorkFlows(client, SolutionName)
                                    .Where(w => w.StateCode == 0 && w.StatusCode == 1)
                                    .OrderByDescending(w => w.CreatedOn);

            Console.WriteLine($"\nFound {inactiveFlows.Count()} flows to enable");

            foreach (var inactiveFlow in inactiveFlows)
            {
                Console.WriteLine($"Enabling flow called {inactiveFlow.Name}");

                var enabledFlow = new Entity("workflow", inactiveFlow.Id)
                {
                    ["workflowid"] = inactiveFlow.Id,
                    ["statecode"] = new OptionSetValue(1),
                    ["statuscode"] = new OptionSetValue(2)
                };

                client.Update(enabledFlow);

                Console.WriteLine($"{inactiveFlow.Name} enabled");
            }
        }
    }
}
