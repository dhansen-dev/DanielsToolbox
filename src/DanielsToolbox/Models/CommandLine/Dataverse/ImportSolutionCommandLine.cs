
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;

using DanielsToolbox.Extensions;
using System.CommandLine.Invocation;
using ShellProgressBar;
using System.Runtime.CompilerServices;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class ImportSolutionCommandLine
    {
        public DataverseServicePrincipalCommandLine DataverseServicePrincipalCommandLine { get; set; }

        public bool DisplayProgressBar { get; init; }

        public bool PublishChanges { get; init; }
        public FileInfo PathToZipFile { get; init; }
        public static IEnumerable<Symbol> Arguments()
            => new Symbol[]
            {
                new Option<FileInfo>("--path-to-zip-file"),
                new Option<bool>("--display-progress-bar"),
                new Option<bool>("--publish-changes", getDefaultValue: () => true)
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

        public static void ImportAsyncAndWaitWithProgress(ServiceClient client, string solutionZipPath, bool publishChanges, bool displayProgressBar)
        {
            var asyncOperationId = client.ImportSolutionAsync(solutionZipPath, out Guid importJobId);

            Console.WriteLine($"Async solution import requested. Async id: {asyncOperationId}, Import job id: {importJobId}");

            Console.WriteLine();

            WaitForAsyncOperationToStart(asyncOperationId, client);

            var options = new ProgressBarOptions
            {

            };

            using (var pbar = new ProgressBar(100 * 100, "Importing solution", options))
            {
                WaitForAsyncOperationToComplete(importJobId, asyncOperationId, client, pbar, displayProgressBar);

                PrintProgress(displayProgressBar, pbar, "Changes successfully imported");

                if (publishChanges)
                {
                    PrintProgress(displayProgressBar, pbar, "Publishing changes!");

                    client.Execute(new PublishAllXmlRequest());

                    PrintProgress(displayProgressBar, pbar, "Changes successfully published!");
                }

                pbar.Tick(100 * 100);

            }
        }

        public static void WaitForAsyncOperationToComplete(Guid importJobId, Guid asyncOperationId, ServiceClient client, ProgressBar pbar, bool displayProgressBar)
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

                    PrintProgress(displayProgressBar, pbar, $"{currentProgress}%");

                    pbar.Tick((int)currentProgress * 100);

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

            var asyncOperation = new AsyncOperation(asyncOpEntity);

            if (asyncOperation.StatusCode != AsyncOperation.AsyncOperationStatusCode.Succeeded)
            {
                throw new Exception(asyncOperation.FriendlyMessage);
            }
        }

        public static void WaitForAsyncOperationToStart(Guid asyncOperationId, ServiceClient client)
        {
            AsyncOperation asyncOperation = null;
            Entity asyncOperationEntity;

            int tries = 0;
            do
            {
                asyncOperationEntity = client.Retrieve("asyncoperation", asyncOperationId, new ColumnSet(true));

                asyncOperation = new AsyncOperation(asyncOperationEntity);

                Thread.Sleep(1500);
            } while (tries++ < 10 && !asyncOperation?.HasStarted() == false);
        }

        public void Import(FileInfo pathToZipFile)
        {
            ServiceClient client = DataverseServicePrincipalCommandLine.Connect();

            ImportAsyncAndWaitWithProgress(client, PathToZipFile.FullName, PublishChanges, DisplayProgressBar);
        }

        public void Import()
            => Import(PathToZipFile);

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

        private static void PrintProgress(bool displayProgressBar, ProgressBar progressBar, string message)
        {
            Action<string> printer = displayProgressBar ? progressBar.WriteLine : Console.WriteLine;

            printer(message);
        }
    }
}