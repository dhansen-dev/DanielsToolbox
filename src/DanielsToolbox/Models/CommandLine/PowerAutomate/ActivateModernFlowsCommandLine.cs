using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

using System;
using System.CommandLine.Invocation;
using System.CommandLine;
using System.Linq;

using DanielsToolbox.Extensions;
using DanielsToolbox.Models.CommandLine.Dataverse;

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

            var context = new OrganizationServiceContext(client);

            var inactiveFlowsQuery =

                from solution in context.CreateQuery("solution")
                join component in context.CreateQuery("solutioncomponent")
                on solution.GetAttributeValue<Guid>("solutionid") equals component.GetAttributeValue<EntityReference>("solutionid").Id
                join workflow in context.CreateQuery("workflow")
                on component.GetAttributeValue<Guid>("objectid") equals workflow.GetAttributeValue<Guid>("workflowid")
                where
                    solution.GetAttributeValue<string>("uniquename") == SolutionName &&
                    component.GetAttributeValue<OptionSetValue>("componenttype").Value == 29 &&
                    workflow.GetAttributeValue<OptionSetValue>("category").Value == 5 &&
                    workflow.GetAttributeValue<OptionSetValue>("statecode").Value == 0 &&
                    workflow.GetAttributeValue<OptionSetValue>("statuscode").Value == 1
                select new
                {
                    SolutionId = solution.Id,
                    ComponentId = component.Id,
                    Workflow = new { Id = workflow.GetAttributeValue<Guid>("workflowid"), CreatedOn = workflow.GetAttributeValue<DateTime>("createdon"), Name = workflow.GetAttributeValue<string>("name") }
                };

            var inactiveFlows = inactiveFlowsQuery.ToList();

            Console.WriteLine($"\nFound {inactiveFlows.Count()} flows to enable");

            foreach (var inactiveFlow in inactiveFlowsQuery.ToList().OrderByDescending(c => c.Workflow.CreatedOn))
            {
                Console.WriteLine($"Enabling flow called {inactiveFlow.Workflow.Name}");

                var enabledFlow = new Entity("workflow", inactiveFlow.Workflow.Id)
                {
                    ["workflowid"] = inactiveFlow.Workflow.Id,
                    ["statecode"] = new OptionSetValue(1),
                    ["statuscode"] = new OptionSetValue(2)
                };

                client.Update(enabledFlow);

                Console.WriteLine($"{inactiveFlow.Workflow.Name} enabled");
            }
        }
    }
}
