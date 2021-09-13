using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanielsToolbox.Helpers
{
    public class QueryHelper
    {
        public static List<Workflow> GetModernWorkFlows(ServiceClient client, string solutionName)
        {
            var context = new OrganizationServiceContext(client);

            var flows =

                from solution in context.CreateQuery("solution")
                join component in context.CreateQuery("solutioncomponent")
                on solution.GetAttributeValue<Guid>("solutionid") equals component.GetAttributeValue<EntityReference>("solutionid").Id
                join workflow in context.CreateQuery("workflow")
                on component.GetAttributeValue<Guid>("objectid") equals workflow.GetAttributeValue<Guid>("workflowid")
                where
                    solution.GetAttributeValue<string>("uniquename") == solutionName &&
                    component.GetAttributeValue<OptionSetValue>("componenttype").Value == 29 &&
                    workflow.GetAttributeValue<OptionSetValue>("category").Value == 5
                select new Workflow
                {
                    Id = workflow.GetAttributeValue<Guid>("workflowid"),
                    CreatedOn = workflow.GetAttributeValue<DateTime>("createdon"),
                    Name = workflow.GetAttributeValue<string>("name"),
                    StateCode = workflow.GetAttributeValue<OptionSetValue>("statecode").Value,
                    StatusCode = workflow.GetAttributeValue<OptionSetValue>("statuscode").Value,
                    ClientData = workflow.GetAttributeValue<string>("clientdata")
                };


            return flows.ToList();
        }

        public class Workflow
        {
            public Guid Id { get; set; }
            public DateTime CreatedOn { get; set; }
            public string Name { get; set; }
            public int StateCode { get; set; }
            public int StatusCode { get; set; }
            public string ClientData { get; set; }
        }
    }
}
