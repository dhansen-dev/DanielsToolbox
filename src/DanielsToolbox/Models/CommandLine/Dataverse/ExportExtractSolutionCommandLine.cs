using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

using DanielsToolbox.Extensions;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class ExportExtractSolutionCommandLine
    {
        public ExportSolutionCommandLine ExportSolutionCommandLine { get; init; }
        public SolutionPackagerCommandLine ExtractSolutionCommandLine { get; init; }

        public FileInfo PathToZipFile { get; init; }

        public static Command Create()
        {
            var command = new Command("export-extract", "Export a solution from Dataverse and extract it with solution packager")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                ExportSolutionCommandLine.Arguments(),
                SolutionPackagerCommandLine.Arguments()
            };

            command.Handler = CommandHandler.Create<ExportExtractSolutionCommandLine>(a => a.ExportExtract());

            return command;
        }

        public void ExportExtract()
        {
            ExportSolutionCommandLine.ExportSolution(PathToZipFile);

            ExtractSolutionCommandLine.RunSolutionPackager();
        }
    }
}
