using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;

using DanielsToolbox.Extensions;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System.Linq;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class ExportSolutionCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; init; }
        public string SolutionName { get; init; }
        public static IEnumerable<Symbol> Arguments()
                                            => new Symbol[]
            {
                new Argument<string>("solution-name", "Solution name (unique name)"),
            };

        public static Command Create()
        {
            var command = new Command("export", "Export a solution from Dataverse")
            {
               DataverseServicePrincipalCommandLine.Arguments(),
               Arguments()
            };

            command.Handler = CommandHandler.Create<ExportSolutionCommandLine>(async handler => await handler.ExportSolution());

            return command;
        }

        public async Task<string> ExportSolution()
                    => await ExportSolution(new FileInfo(Path.GetTempFileName()));

        public async Task<string> ExportSolution(FileInfo pathToSaveSolutionZip)
        {
            ServiceClient client = DataverseServicePrincipalCommandLine.Connect();

            var zipPath = pathToSaveSolutionZip.FullName;

            Console.WriteLine($"Exporting solution {SolutionName}");

            var timer = Stopwatch.StartNew();

            var exportSolutionResponseTask = client.ExecuteAsync(new ExportSolutionRequest
            {
                SolutionName = SolutionName,
                Managed = false
            });

            int count = 1;

            while(!exportSolutionResponseTask.IsCompleted)
            {              
                await Task.Delay(5000);

                Console.WriteLine(string.Join("", Enumerable.Repeat(".", count++)));
            }

            var exportSolutionResponse = (ExportSolutionResponse)(await exportSolutionResponseTask);

            Console.WriteLine($"Solution exported in {timer.Elapsed:c}");

            File.WriteAllBytes(zipPath, exportSolutionResponse.ExportSolutionFile);

            Console.WriteLine($"Solution was {Math.Round(exportSolutionResponse.ExportSolutionFile.Length / 1000.0, 2)} kilobytes and saved to " + zipPath);

            return zipPath;
        }
    }
}