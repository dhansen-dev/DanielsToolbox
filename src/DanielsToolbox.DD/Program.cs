using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace DanielToolbox.Core
{
    class Program
    {
        const string LIB_PATH = @".\Lib";

        static async Task Main(string[] args)
            => await MakeSureDependenciesAreInstalled();

        private static async Task MakeSureDependenciesAreInstalled()
        {
            if ((File.Exists(LIB_PATH + "/Microsoft.ApplicationInsights.dll") &&
                File.Exists(LIB_PATH + "/Microsoft.PowerPlatform.Tooling.BatchedTelemetry.dll") &&
                File.Exists(LIB_PATH + "/SolutionPackagerLib.dll")) == false)
            {

                Console.WriteLine("Required files were not found. Downloading package");

                Directory.CreateDirectory(LIB_PATH);

                var client = new HttpClient();

                await DownloadSolutionPackagerFiles(client);

                await DownloadSdkFiles(client); 
            }
            else
            {
                Console.WriteLine("All dependencies exists");
            }

        }

        private static async Task DownloadSdkFiles(HttpClient client)
        {
            var coreAssembliesVersion = "9.0.2.42";

            var sdkPackage = await client.GetByteArrayAsync($"https://www.nuget.org/api/v2/package/microsoft.crmsdk.coreassemblies/{coreAssembliesVersion}");

            Console.WriteLine($"Downloaded version {coreAssembliesVersion} of CoreAssemblies");

            using (var archive = new ZipArchive(new MemoryStream(sdkPackage)))
            {
                Console.WriteLine("Extracting required files");

                await Task.WhenAll(SaveFileToDiskFromArchive("lib/net462/", "Microsoft.Crm.Sdk.Proxy.dll", archive),
                                   SaveFileToDiskFromArchive("lib/net462/", "Microsoft.Xrm.Sdk.dll", archive)
                                );
            }
        }

        private static async Task DownloadSolutionPackagerFiles(HttpClient client)
        {
            var coreToolsVersion = "9.1.0.111";

            var toolPackage = await client.GetByteArrayAsync($"https://www.nuget.org/api/v2/package/Microsoft.CrmSdk.CoreTools/{coreToolsVersion}");

            Console.WriteLine($"Downloaded version {coreToolsVersion} of Core Tools");

            using (var archive = new ZipArchive(new MemoryStream(toolPackage)))
            {
                Console.WriteLine("Extracting required files");

                await Task.WhenAll(SaveFileToDiskFromArchive("content/bin/coretools/", "SolutionPackagerLib.dll", archive),
                                   SaveFileToDiskFromArchive("content/bin/coretools/", "Microsoft.ApplicationInsights.dll", archive),
                                   SaveFileToDiskFromArchive("content/bin/coretools/", "Microsoft.PowerPlatform.Tooling.BatchedTelemetry.dll", archive)
                                );
            }
        }

        private static async Task SaveFileToDiskFromArchive(string path, string filename, ZipArchive archive)
        {
            Console.WriteLine("Saving " + filename);

            var libFilePath = Path.Combine(LIB_PATH, filename);

            using (var entryStream = archive.GetEntry($"{path}{filename}").Open())
            {
                var tempPath = Path.GetTempPath();
                                
                var memorystream = new MemoryStream();

                await entryStream.CopyToAsync(memorystream);

                var tempFile = Path.Combine(tempPath, filename);

                await File.WriteAllBytesAsync(tempFile, memorystream.ToArray());

                var nugetFileVersion = AssemblyName.GetAssemblyName(tempFile).Version;

                var libFileExists = File.Exists(libFilePath);
                
                if (libFileExists == false || nugetFileVersion > AssemblyName.GetAssemblyName(libFilePath).Version)
                {
                    File.Move(tempFile, Path.Combine(LIB_PATH, filename));
                } else
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
