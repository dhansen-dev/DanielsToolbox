using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace DanielToolbox.Core
{
    class Program
    {
        static async Task Main(string[] args)
            => await MakeSureDependenciesAreInstalled();

        private static async Task MakeSureDependenciesAreInstalled()
        {
            const string LIB_PATH = @".\Lib";

            if ((File.Exists(LIB_PATH + "/Microsoft.ApplicationInsights.dll") &&
                File.Exists(LIB_PATH + "/Microsoft.PowerPlatform.Tooling.BatchedTelemetry.dll") &&
                File.Exists(LIB_PATH + "/SolutionPackagerLib.dll")) == false)
            {

                Console.WriteLine("Required files were not found. Downloading package");

                Directory.CreateDirectory(LIB_PATH);

                var client = new HttpClient();

                var toolPackage = await client.GetByteArrayAsync("https://www.nuget.org/api/v2/package/Microsoft.CrmSdk.CoreTools/9.1.0.79");

                Console.WriteLine("Package downloaded");

                using (var archive = new ZipArchive(new MemoryStream(toolPackage)))
                {

                    Console.WriteLine("Extracting required files");

                    await Task.WhenAll(SaveFileToDiskFromArchive("SolutionPackagerLib.dll", archive),
                                       SaveFileToDiskFromArchive("Microsoft.ApplicationInsights.dll", archive),
                                       SaveFileToDiskFromArchive("Microsoft.PowerPlatform.Tooling.BatchedTelemetry.dll", archive)
                                    );
                }

                async Task SaveFileToDiskFromArchive(string filename, ZipArchive archive)
                {
                    Console.WriteLine("Saving " + filename);

                    using (var entryStream = archive.GetEntry($"content/bin/coretools/{filename}").Open())
                    {
                        var memorystream = new MemoryStream();
                        await entryStream.CopyToAsync(memorystream);

                        File.WriteAllBytes(Path.Combine(LIB_PATH, filename), memorystream.ToArray());
                    }
                }
            }
            else
            {
                Console.WriteLine("All dependencies exists");
            }

        }
    }
}
