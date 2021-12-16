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

            Console.WriteLine("Publishing all changes");

            var publishTask = client.ExecuteAsync(new PublishAllXmlRequest());

            var watch = Stopwatch.StartNew(); 

            while(!publishTask.IsCompleted)
            {
                await Task.Delay(5000);

                Console.WriteLine($"Still waiting for publish to finish ({Math.Round(watch.Elapsed.TotalSeconds, 2)}s)");
            }

            await publishTask;

            Console.WriteLine($"All changes published in {Math.Round(watch.Elapsed.TotalSeconds)}s");
        }
    }
}
