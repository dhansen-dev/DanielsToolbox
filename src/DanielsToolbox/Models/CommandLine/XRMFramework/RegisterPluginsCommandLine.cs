using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;
using DanielsToolbox.Managers;
using DanielsToolbox.Models.CommandLine.Dataverse;

using Microsoft.PowerPlatform.Dataverse.Client;

namespace DanielsToolbox.Models.CommandLine.XRMFramework
{
    public class RegisterPluginsCommandLine
    {
        public string SolutionName { get; init; }
        public FileInfo PluginAssemblyPath { get; init; }
        public bool UpdateOnlyPluginAssembly { get; init; }

        public bool SyncPluginSteps { get; init; }
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; set; }

        public static IEnumerable<Symbol> Arguments()
            => new Symbol[] {
                new Argument<FileInfo>("pluginassemblypath", "Path to plugin assembly").ExistingOnly(),
                new Option<bool>("--update-only-plugin-assembly", "Only update assembly"),
                new Option<string>("--solution-name", "Will add assembly and related data to named solution"),
                new Option<bool>("--sync-plugin-steps", () => false, "Will sync remote plugin steps with local")
            };

        public static Command Create()
        {
            var command = new Command("pluginregistration", "Register plugin assembly with optional steps and images in dataverse")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                Arguments()
            };

            command.Handler = CommandHandler.Create<RegisterPluginsCommandLine>(async handler => await handler.RegisterPluginsInAssembly());

            return command;
        }

        private async Task RegisterPluginsInAssembly()
        {

            ServiceClient client = DataverseServicePrincipalCommandLine.Connect();

            await PluginManager.RegisterPluginsInCRM(this, client);
        }
    }
}
