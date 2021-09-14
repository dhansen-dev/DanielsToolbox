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
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

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

            var asyncExport = await client.ExecuteAsync(new OrganizationRequest("ExportSolutionAsync")
            {
                Parameters = new ParameterCollection
                {
                    {  "SolutionName", SolutionName },
                    {  "Managed", false }
                }                
            });

            var exportAsyncOperationId = Guid.Parse(asyncExport["AsyncOperationId"].ToString());
            var exportJobId = Guid.Parse(asyncExport["ExportJobId"].ToString());

            var asyncExportOperation = (await client.RetrieveAsync("asyncoperation", exportAsyncOperationId, new ColumnSet(true))).ToEntity<AsyncOperation>();

            int count = 1;

            

            while(!asyncExportOperation.IsCompleted())
            {              
                await Task.Delay(10000);

                Console.WriteLine(new string('.', count++));

                asyncExportOperation = (await client.RetrieveAsync("asyncoperation", exportAsyncOperationId, new ColumnSet(true))).ToEntity<AsyncOperation>();
            }

            if(asyncExportOperation.StatusCode != AsyncOperation.AsyncOperationStatusCode.Succeeded)
            {
                throw new Exception("Export failed with message: " + asyncExportOperation.FriendlyMessage);
            }

            var exportDataResponse = await client.ExecuteAsync(new OrganizationRequest("DownloadSolutionExportData")
            {
                Parameters = new ParameterCollection
                {
                    { "ExportJobId", exportJobId }
                }
            });

            var exportSolutionFile = (byte[])(exportDataResponse["ExportSolutionFile"]);

            Console.WriteLine($"Solution exported in {timer.Elapsed:c}");

            File.WriteAllBytes(zipPath, exportSolutionFile);

            Console.WriteLine($"Solution was {Math.Round(exportSolutionFile.Length / 1000.0, 2)} kilobytes and saved to " + zipPath);

            return zipPath;
        }
    }
}