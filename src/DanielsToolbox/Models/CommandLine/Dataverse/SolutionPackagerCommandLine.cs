using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Crm.Tools.SolutionPackager;
using Microsoft.PowerPlatform.Tooling.BatchedTelemetry;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;

using DanielsToolbox.Extensions;
using System.Text.Json;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class SolutionPackagerCommandLine
    {
        private readonly PackagerArguments _arguments = new();
        public CommandAction Action { init => _arguments.Action = value; }

        public AllowDelete AllowDeletes { init => _arguments.AllowDeletes = value; }
        public AllowWrite AllowWrites { init => _arguments.AllowWrites = value; }
        public bool Clobber { init => _arguments.Clobber = value; }
        public TraceLevel ErrorLevel { init => _arguments.ErrorLevel = value; }
        public DirectoryInfo Folder { init => _arguments.Folder = value.FullName; }
        public string LocaleTemplate { init => _arguments.LocaleTemplate = value; }
        public bool Localize { init => _arguments.Localize = value; }
        public FileInfo LogFile { init => _arguments.LogFile = value.FullName; }
        public FileInfo MappingFile { init => _arguments.MappingFile = value.FullName; }
        public bool NoLogo { init => _arguments.NoLogo = value; }
        public SolutionPackageType PackageType { init => _arguments.PackageType = value; }
        public FileInfo PathToZipFile { init => _arguments.PathToZipFile = value.FullName; }
        public bool RepackOnPackForTesting { init => _arguments.RepackOnPackForTesting = value; }
        public string SingleComponent { init => _arguments.SingleComponent = value; }
        public bool UseLcid { init => _arguments.UseLcid = value; }
        public bool UseUnmanagedFileForManaged { init => _arguments.UseUnmanagedFileForManaged = value; }

        public static IEnumerable<Symbol> Arguments()
            => new Symbol[] {
                new Argument<DirectoryInfo>("folder", "Path to solution folder").LegalFilePathsOnly(),
                new Option<CommandAction>("--action", "Action to Perform"),
                new Option<FileInfo>("--path-to-zip-file", "Path to file containing solution"),
                new Option<FileInfo>("--mapping-file", "Path to mapping file").ExistingOnly(),
                new Option<FileInfo>("--log-file").ExistingOnly(),
                new Option<TraceLevel>("--error-level"),
                new Option<SolutionPackageType>("--package-type"),
                new Option<AllowDelete>("--allow-deletes"),
                new Option<AllowWrite>("--allow-writes"),
                new Option<bool>("--clobber"),
                new Option<string>("--single-component", () => "NONE", "Default NONE")
            };

        public static Command Create()
        {
            var command = new Command("packager", "Calls solution packager")
            {
                    Arguments()
            };

            command.Handler = CommandHandler.Create<SolutionPackagerCommandLine>(a => a.RunSolutionPackager());

            return command;
        }

        public void RunSolutionPackager()
        {
            _arguments.DisableTelemetry = true;

            var defaultTelemetryConfiguration = TelemetryConfiguration.CreateDefault();
            defaultTelemetryConfiguration.DisableTelemetry = true;

            var telemetryClient = new AppTelemetryClient(defaultTelemetryConfiguration);

            var packager = new SolutionPackager(_arguments, telemetryClient);

            packager.Run();

            if (_arguments.Action == CommandAction.Extract)
            {
                foreach (var flowDefinition in new DirectoryInfo(Path.Combine(_arguments.Folder, "Workflows/")).GetFiles("*.json"))
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(flowDefinition.FullName));

                    using Stream waypointsStreamWriter = new FileStream(flowDefinition.FullName, FileMode.OpenOrCreate);
                    var utf8MemoryWriter = new Utf8JsonWriter(waypointsStreamWriter, new JsonWriterOptions
                    {
                        Indented = true
                    });

                    doc.WriteTo(utf8MemoryWriter);

                    utf8MemoryWriter.Flush();
                }
            }
            Console.WriteLine("Solution extracted");
        }
    }
}
