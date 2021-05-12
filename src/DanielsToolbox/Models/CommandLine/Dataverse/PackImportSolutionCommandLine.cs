using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

using DanielsToolbox.Extensions;

namespace DanielsToolbox.Models.CommandLine.Dataverse
{
    public class PackImportSolutionCommandLine
    {
        public ImportSolutionCommandLine ImportSolutionCommandLine { get; init; }
        public SolutionPackagerCommandLine PackSolutionCommandLine { get; init; }

        public FileInfo PathToZipFile { get; init; }

        public static Command Create()
        {
            var command = new Command("pack-import", "Export a solution from Dataverse and extract it with solution packager")
            {
                DataverseServicePrincipalCommandLine.Arguments(),
                SolutionPackagerCommandLine.Arguments().Union(ImportSolutionCommandLine.Arguments(), SymbolEqualityComparer.Create)

            };

            var ett = SymbolEqualityComparer.Create;
            var två = SymbolEqualityComparer.Create;



            command.Handler = CommandHandler.Create<PackImportSolutionCommandLine>(a => a.PackImport());

            return command;
        }

        public void PackImport()
        {
            PackSolutionCommandLine.RunSolutionPackager();

            ImportSolutionCommandLine.Import();
        }
    }
}
