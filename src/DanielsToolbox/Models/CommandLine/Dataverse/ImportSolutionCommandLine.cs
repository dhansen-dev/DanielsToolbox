using DanielsToolbox.Extensions;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using ShellProgressBar;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class ImportSolutionCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; set; }

        public bool DisplayProgressBar { get; init; }

        public FileInfo PathToZipFile { get; init; }
        public bool PrintDevopsProgress { get; init; }

        public bool PublishChanges { get; init; }

        public static IEnumerable<Symbol> Arguments()
            => new Symbol[]
            {
                new Option<FileInfo>("--path-to-zip-file"),
                new Option<bool>("--display-progress-bar"),
                new Option<bool>("--print-devops-progress"),
                new Option<bool>("--publish-all", getDefaultValue: () => true)
            };

        public static Command Create()
        {
            var command = new Command("import", "Import a solution to Dataverse")
            {
               DataverseServicePrincipalCommandLine.Arguments(),
               Arguments()
            };

            command.Handler = CommandHandler.Create<ImportSolutionCommandLine>(a => a.Import());

            return command;
        }

        public static void WaitForAsyncOperationToStart(Guid asyncOperationId, ServiceClient client)
        {
            AsyncOperation asyncOperation = null;
            Entity asyncOperationEntity;

            int tries = 0;
            do
            {
                asyncOperationEntity = client.Retrieve("asyncoperation", asyncOperationId, new ColumnSet(true));

                asyncOperation = asyncOperationEntity.ToEntity<AsyncOperation>();

                Thread.Sleep(1500);
            } while (tries++ < 10 && asyncOperation?.HasStarted() == false);
        }

        public void Import(FileInfo pathToZipFile)
        {
            ServiceClient client = DataverseServicePrincipalCommandLine.Connect();

            ImportAsyncAndWaitWithProgress(client, PathToZipFile.FullName, PublishChanges, DisplayProgressBar);
        }

        public void Import()
            => Import(PathToZipFile);

        public void ImportAsyncAndWaitWithProgress(ServiceClient client, string solutionZipPath, bool publishChanges, bool displayProgressBar)
        {
            Console.WriteLine("Reading archive " + solutionZipPath);

            using (var zip = ZipFile.OpenRead(solutionZipPath))
            {
                var solutionXML = XDocument.Load(zip.GetEntry("solution.xml").Open());

                Console.WriteLine("Importing version " + solutionXML.XPathSelectElement("ImportExportXml/SolutionManifest/Version").Value);
            }

            var asyncOperationId = client.ImportSolutionAsync(solutionZipPath, out Guid importJobId);

            Console.WriteLine($"Async solution import requested. Async id: {asyncOperationId}, Import job id: {importJobId}");

            Console.WriteLine();

            WaitForAsyncOperationToStart(asyncOperationId, client);

            importJobId = FindRealImportJobId(solutionZipPath, client);

            var options = new ProgressBarOptions
            {
            };

            ProgressBar pbar = DisplayProgressBar ? new ProgressBar(100 * 100, "Importing solution", options) : null;

            using (pbar)
            {
                WaitForAsyncOperationToComplete(importJobId, asyncOperationId, client, pbar);

                PrintProgress(pbar, "Changes successfully imported", 1, "Solution Import");

                if (publishChanges)
                {
                    PrintProgress(pbar, "Publishing changes!", 0, "Publishing");

                    client.Execute(new PublishAllXmlRequest());

                    PrintProgress(pbar, "Changes successfully published!", 1, "Publishing");
                }

                pbar?.Tick(100 * 100);
            }
        }

        public void WaitForAsyncOperationToComplete(Guid importJobId, Guid asyncOperationId, ServiceClient client, ProgressBar progressBar)
        {
            ImportJob importJob = null;
            Entity importJobEntity;
            do
            {
                var query = new QueryExpression("importjob")
                {
                    ColumnSet = new ColumnSet(true),
                    NoLock = true
                };

                query.Criteria.AddCondition("importjobid", ConditionOperator.Equal, importJobId);

                importJobEntity = client.RetrieveMultiple(query).Entities.FirstOrDefault();

                if (importJobEntity != null)
                {
                    importJob = new ImportJob(importJobEntity);

                    var currentProgress = Math.Round(importJob.Progress, 2);

                    PrintProgress(progressBar, $"{currentProgress}%", currentProgress, "Solution Import");

                    if (importJob.IsCompleted() == false)
                    {
                        Thread.Sleep(5000);
                    }
                }
                else
                {
                    Thread.Sleep(5000);
                }
            } while (importJobEntity == null || importJob?.IsCompleted() == false);

            Thread.Sleep(5000);

            var asyncOpEntity = client.Retrieve("asyncoperation", asyncOperationId, new ColumnSet(true));

            var asyncOperation = asyncOpEntity.ToEntity<AsyncOperation>();

            if (asyncOperation.StatusCode != AsyncOperation.AsyncOperationStatusCode.Succeeded)
            {
                throw new Exception(asyncOperation.FriendlyMessage);
            }
        }

        /// <summary>
        /// For some reason we do not get the actual import job id any more, so we query for it
        /// instead
        /// </summary>
        /// <param name="solutionZipPath">The solution zip path so we can extract the solution name</param>
        /// <param name="client">CRM Service client</param>
        /// <returns>The actual import job id</returns>
        private static Guid FindRealImportJobId(string solutionZipPath, ServiceClient client)
        {
            using (var zip = ZipFile.OpenRead(solutionZipPath))
            {
                var solutionXML = zip.Entries.Where(e => e.Name == "solution.xml").Single();

                using (var solutionXMLStream = solutionXML.Open())
                {
                    var xdoc = XDocument.Load(solutionXMLStream);

                    var solutionName = xdoc.Root.XPathSelectElement("SolutionManifest/UniqueName").Value;

                    var query = new QueryExpression("importjob")
                    {
                        ColumnSet = new ColumnSet(true),
                        NoLock = true
                    };

                    query.Criteria.AddCondition("solutionname", ConditionOperator.Equal, solutionName);
                    query.Criteria.AddCondition("completedon", ConditionOperator.Null);

                    query.AddOrder("createdon", OrderType.Descending);

                    EntityCollection queryResult = null;

                    do
                    {
                        Thread.Sleep(5000);

                        queryResult = client.RetrieveMultiple(query);
                    } while (!queryResult.Entities.Any());

                    // Assume that we only have one result.
                    var importJobEntity = queryResult.Entities.Single();

                    return importJobEntity.Id;
                }
            }
        }

        private static bool IsNotNull(ref int numberOfNulls, Entity importJob, Guid importJobId)
        {
            if (numberOfNulls <= 10)
            {
                if (importJob != null)
                {
                    return true;
                }
                else
                {
                    numberOfNulls++;

                    return false;
                }
            }
            else
            {
                throw new Exception("Kunde inte hitta ett importjob med id " + importJobId);
            }
        }

        private void PrintProgress(ProgressBar progressBar, string message, double currentProgress, string currentProcess)
        {
            if (DisplayProgressBar)
            {
                progressBar?.WriteLine(message);

                progressBar?.Tick((int)currentProgress * 100);
            }
            else
            {
                if (PrintDevopsProgress)
                {
                    Console.WriteLine($"##vso[task.setprogress value={(int)currentProgress};]{currentProcess}");

                    if (currentProgress >= 100)
                    {
                        Console.WriteLine($"##vso[task.complete result=Succeeded;]DONE");
                    }
                }

                Console.WriteLine(message);
            }
        }
    }
}