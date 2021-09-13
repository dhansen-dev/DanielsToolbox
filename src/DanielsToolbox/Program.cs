using DanielsToolbox.Models.CommandLine.AzureDevops;
using DanielsToolbox.Models.CommandLine.Dataverse;
using DanielsToolbox.Models.CommandLine.PowerAutomate;
using DanielsToolbox.Models.CommandLine.XRMFramework;

using System.CommandLine;
using System.Threading.Tasks;

namespace PowerPlatformCLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        => await new RootCommand("Tools that Daniel find useful")
            {
                new Command("power-automate")
                {
                    ActivateModernFlowsCommandLine.Create(),
                    CreateMermaidDiagramsFromPowerAutomateFlowsCommandLine.Create()
                },
                new Command("dataverse")
                {
                    SolutionPackagerCommandLine.Create(),
                    ExportSolutionCommandLine.Create(),
                    ImportSolutionCommandLine.Create(),
                    PackImportSolutionCommandLine.Create(),
                    ExportExtractSolutionCommandLine.Create(),
                    SynchronizeWebResourcesCommandLine.Create()
                },
                new Command("azure-devops")
                {
                    CreatePullRequestCommandLine.Create()
                },
                new Command("xrmframework", "Tools related to XRMFramework")
                {
                    RegisterPluginsCommandLine.Create(),
                    DocumentPluginsCommandLine.Create()
                }
            }.InvokeAsync(args);
    }
}
