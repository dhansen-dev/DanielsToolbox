
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DanielsToolbox.Models.CommandLine.AzureDevops
{
    public class TransitionTasksCommandLine
    {
        public string PersonalAccessToken { get; init; }
        public Guid QueryId { get; init; }

        public string Newstate { get; init; }
        public string BaseUrl { get; init; }


        public static Command Create()
        {
            var command = new Command("transition-tasks", "Moves tasks from specified state to new state")
            {
                new Argument<Guid>("query-id"),
                new Argument<string>("new-state"),
                new Argument<string>("base-url"),
                new Argument<string>("personal-access-token")
            };

            command.Handler = CommandHandler.Create<TransitionTasksCommandLine>(async (a) => await a.TransitionTasks());

            return command;

        }

        public async Task TransitionTasks()
        {
            using var wiClient = new WorkItemTrackingHttpClient(new Uri(BaseUrl), new VssBasicCredential(string.Empty, PersonalAccessToken));

            var queryResult = await wiClient.QueryByIdAsync(QueryId);

            var doc = new JsonPatchDocument
            {
                { 
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = "/fields/System.State",
                        Value = Newstate
                    } 
                }
            };

            foreach (var workitem in queryResult.WorkItems)
            {
                await wiClient.UpdateWorkItemAsync(doc, workitem.Id);
            }
        }
            
    }
}