using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class PublishAllCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipal { get; init; }

        public static Command Create()
        {
            var command = new Command("publish-all", "Publishes all customizations")
            {
                DataverseServicePrincipalCommandLine.Arguments()
            };

            command.Handler = CommandHandler.Create<PublishAllCommandLine>(async c => await c.PublishAll());

            return command;

        }

        private async Task PublishAll()
        {
            var client = DataverseServicePrincipal.Connect();

            var context = new OrganizationServiceContext(client.Clone());

            Console.WriteLine("Publishing all changes");

            var startDate = DateTime.Now;

            var publishTask = client.ExecuteAsync(new PublishAllXmlRequest());

            await Task.Delay(1000);

            var publishAllRecord = context.CreateQuery("msdyn_solutionhistory")
                                .Where(x => x.GetAttributeValue<string>("msdyn_name") == "PublishAll")
                                .OrderByDescending(x => x.GetAttributeValue<DateTime>("msdyn_starttime"))
                                .FirstOrDefault();

            Entity publishAllEntity = null;

            var pollingClient = client.Clone();

            do
            {
                publishAllEntity = await pollingClient.RetrieveAsync("msdyn_solutionhistory", publishAllRecord.GetAttributeValue<Guid>("msdyn_solutionhistoryid"), new ColumnSet(true));

                Console.WriteLine($"PublishAll is still running");

                await Task.Delay(5000);

            } while (publishAllEntity.Contains("msdyn_result") == false);

            if((publishAllEntity.GetAttributeValue<bool?>("msdyn_result") ?? false) == false)
            {
                throw new Exception(publishAllEntity.GetAttributeValue<string>("msdyn_exceptionmessage") + publishAllEntity.GetAttributeValue<string>("msdyn_exceptionstack"));
            }

            Console.WriteLine($"Publish all successful in {publishAllEntity.GetAttributeValue<int>("msdyn_totaltime")}s");
        }
    }
}
