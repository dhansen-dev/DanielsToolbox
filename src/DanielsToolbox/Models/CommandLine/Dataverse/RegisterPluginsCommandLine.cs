﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;
using DanielsToolbox.Managers;

using Microsoft.PowerPlatform.Dataverse.Client;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class RegisterPluginsCommandLine
    {
        public string SolutionName { get; init; }
        public FileInfo PluginAssemblyPath { get; init; }
        public bool UpdateOnlyPluginAssembly { get; init; }
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; set; }

        public static IEnumerable<Symbol> Arguments()
            => new Symbol[] {
                new Argument<FileInfo>("pluginassemblypath", "Path to plugin assembly").ExistingOnly(),
                new Option<bool>("--update-only-plugin-assembly", "Only update assembly"),
                new Option<string>("--solution-name", "Will add assembly and related data to named solution")
            };

        public static Command Create()
        {
            var command = new Command("pluginregistration")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                Arguments()
            };

            command.Handler = CommandHandler.Create<RegisterPluginsCommandLine>(handler => handler.RegisterPluginsInAssembly());

            return command;
        }

        private void RegisterPluginsInAssembly()
        {
           ServiceClient client = DataverseServicePrincipalCommandLine.Connect();

            PluginManager.RegisterPluginsInCRM(this, client);
        }
    }
}
