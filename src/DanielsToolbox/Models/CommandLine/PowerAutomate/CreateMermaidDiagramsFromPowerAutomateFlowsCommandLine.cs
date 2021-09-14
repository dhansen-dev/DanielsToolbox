using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DanielsToolbox.Extensions;
using DanielsToolbox.Helpers;
using DanielsToolbox.Models.CommandLine.Dataverse;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DanielsToolbox.Models.CommandLine.PowerAutomate
{
    public class CreateMermaidDiagramsFromPowerAutomateFlowsCommandLine
    {

        public DataverseServicePrincipalCommandLine DataverseServicePrincipal { get; init; }
        public string SolutionName { get; init; }

		public DirectoryInfo OutputDir { get; init; }

		public string Output { get; init; }

        public static IEnumerable<Symbol> Arguments()
         => new Symbol[] {
           new Argument<DirectoryInfo>("output-dir", "Output dir for mermaid diagrams"),
		   new Argument<string>("solution-name", "Solution name (unique name)"),
		   new Option<string>("--output", "Filetype of the output").FromAmong("png", "svg", "pdf")		   
		 };

        public static Command Create()
        {
            var command = new Command("create-mermaid-diagrams")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                Arguments()
            };

            command.Handler = CommandHandler.Create<CreateMermaidDiagramsFromPowerAutomateFlowsCommandLine>(async handler => await handler.CreateDiagrams());

            return command;
        }

        private async Task CreateDiagrams()
        {
            var client = DataverseServicePrincipal.Connect();

			Console.WriteLine("Flows from " + SolutionName + " will be used");
			Console.WriteLine("Mermaid diagrams will be stored at " + OutputDir.FullName);
			Console.WriteLine("Selected output format: " + Output);

			Directory.CreateDirectory(OutputDir.FullName);

            foreach(var flow in QueryHelper.GetModernWorkFlowsFromSolution(client, SolutionName))
            {
				Console.WriteLine("Creating diagram for " + flow.Name);

				JObject json = (JObject)JsonConvert.DeserializeObject(flow.ClientData);

				var triggers = json.SelectToken("properties.definition.triggers");

				var triggerObjects = triggers.ToObject<Dictionary<string, Trigger>>();

				var actions = json.SelectToken("properties.definition.actions");

				var actionsObject = actions.ToObject<Dictionary<string, FlowAction>>();

				var list = new Dictionary<string, FlowAction>();

				FindChildren(actionsObject, list);

				var graph = new StringBuilder();

				graph.AppendLine("flowchart");

				var parents = actionsObject.Where(o => !o.Value.RunAfter.Any());

				foreach (var parent in parents)
				{
					foreach (var trigger in triggerObjects)
					{
						graph.AppendLine(FormatKey(trigger.Key) + "-->" + FormatKey(parent.Key));
					}
				}

				GenerateFlowChart(actionsObject, list, graph);

				var inputPath = Path.GetTempFileName();				

				File.WriteAllText(inputPath, graph.ToString());

				var outputFile = $"{Path.Combine(OutputDir.FullName, flow.Name)}.{Output}";

				Console.WriteLine("Generating diagram");

				var mermaidProcess = Process.Start(new ProcessStartInfo("mmdc", $"-i \"{inputPath}\" -o \"{outputFile}\" -w 2540 -H 1440 -b transparent")
				{
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Hidden
				});

				await mermaidProcess.WaitForExitAsync();

				Console.WriteLine("Diagram generated");
			}
        }

        private void GenerateFlowChart(IEnumerable<KeyValuePair<string, FlowAction>> parents, Dictionary<string, FlowAction> list, StringBuilder graph)
		{
			if (parents == null || !parents.Any()) return;

			foreach (var actionKV in parents)
			{
				var action = actionKV.Value;

				switch (action.Type)
				{
					case "Scope":
					case "Foreach":
						graph.AppendLine("subgraph " + FormatKey(actionKV.Key));
						graph.AppendLine("direction TB");

						GenerateFlowChart(action.Actions, list, graph);

						graph.AppendLine("end");
						break;
					case "Switch":
						graph.AppendLine("subgraph " + FormatKey(actionKV.Key));
						graph.AppendLine("direction TB");
						foreach (var c in action.Cases)
						{

							graph.AppendLine("subgraph " + FormatKey(c.Key));
							graph.AppendLine("direction TB");
							GenerateFlowChart(c.Value.Actions, list, graph);
							graph.AppendLine("end");
						}
						graph.AppendLine("end");

						foreach (var c in action.Cases)
						{
							graph.AppendLine(FormatKey(actionKV.Key) + "-->" + FormatKey(c.Key));
						}

						break;
					case "If":
						graph.AppendLine(FormatKey(actionKV.Key) + "{" + actionKV.Key + "}");
						GraphIfElse(actionKV.Key, "Yes", action.Actions, list, graph);
						if (action.Else != null)
							GraphIfElse(actionKV.Key, "No", action.Else?.Actions, list, graph);
						break;
					case "OpenApiConnection":
						graph.AppendLine(FormatKey(actionKV.Key) + "[(" + actionKV.Key + ")]");
						break;
					default:
						graph.AppendLine(FormatKey(actionKV.Key));
						break;
				}

				foreach (var after in action.RunAfter)
				{
					graph.AppendLine(FormatKey(after.Key) + "-->" + FormatKey(actionKV.Key));
				}
			}
		}

        private string FormatKey(string key)
		{
			key = key.Replace("_call", "_Call").Replace("(", "-").Replace(")", "-");

			return key;
		}

        private void GraphIfElse(string key, string text, Dictionary<string, FlowAction> actions, Dictionary<string, FlowAction> list, StringBuilder graph)
		{
			GenerateFlowChart(actions, list, graph);

			foreach (var runafters in actions.Where(t => !t.Value.RunAfter.Any()))
			{
				graph.AppendLine(FormatKey(key) + "-->|" + text + "|" + FormatKey(runafters.Key));
			}
		}

        private void FindChildren(Dictionary<string, FlowAction> children, Dictionary<string, FlowAction> list)
		{
			foreach (var obj in children)
			{
				var action = obj.Value;

				list[obj.Key] = action;

				if (action.Type == "Cases")
				{
					foreach (var cse in action.Cases)
						FindChildren(cse.Value.Actions, list);
				}

				FindChildren(action.Actions, list);
			}
		}

		public class FlowAction
		{
			public Dictionary<string, string[]> RunAfter { get; set; } = new Dictionary<string, string[]>();
			public Dictionary<string, CaseDTO> Cases { get; set; } = new Dictionary<string, CaseDTO>();
			public Dictionary<string, FlowAction> Actions { get; set; } = new Dictionary<string, FlowAction>();
			public FlowAction Else { get; set; }
			public string Type { get; set; }
			public string ForEach { get; set; }
			public string Description { get; set; }
			public string Name { get; set; }
		}

		public class Trigger
		{
			public string Type { get; set; }
			public string Kind { get; set; }
			}

		public class CaseDTO
		{
			public string Case { get; set; }
			public Dictionary<string, FlowAction> Actions { get; set; } = new Dictionary<string, FlowAction>();
		}
	}
}