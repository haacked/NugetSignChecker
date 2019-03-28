using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace src
{
    class Program
    {
        const int packageDownloadCount = 20;

        static void Main(string[] args)
        {
            var tempDirectory = Path.GetTempPath();
            if (!Directory.Exists(tempDirectory))
            {
                Console.WriteLine($"Temp directory '{tempDirectory}' doesn't exist. That's odd. I'm going to bail on this.");
                return;
            }

            var tempPackageDirectory = Path.Combine(tempDirectory, Path.GetRandomFileName());
            try
            {
                try
                {
                    Directory.CreateDirectory(tempPackageDirectory);
                    Console.WriteLine($"Installing the top {packageDownloadCount} most popular community packages (by download count) to '{tempPackageDirectory}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Had some trouble creating the temp directory. Going to bail. {e}");
                    return;
                }
                var mostDownloadedPackageIds = GetMostDownloadedPackageIds(communityPackagesOnly: true);

                int signedCount = 0;
                int totalPackageCount = 0;

                foreach (var packageId in mostDownloadedPackageIds)
                {
                    Console.Write(packageId.PadRight(43));
                    RunProcessGetStandardOutAndExitCode("nuget", $"install {packageId} -Source https://api.nuget.org/v3/index.json -ExcludeVersion -OutputDirectory {tempPackageDirectory}");
                    var packageFilePath = Path.Combine(tempPackageDirectory, packageId, "*.nupkg");
                    var (output, _) = RunProcessGetStandardOutAndExitCode("nuget", $"verify -Signatures {packageFilePath}");

                    if (!output.Contains("Signature type: Repository"))
                    {
                        Console.WriteLine("Something went wrong. These packages should be Repository signed by NuGet. Skipping.");
                        continue;
                    }
                    totalPackageCount++;
                    if (output.Contains("Signature type: Author"))
                    {
                        signedCount++;
                        Console.WriteLine("SIGNED!");
                    }
                    else
                    {
                        Console.WriteLine("NOT SIGNED!");
                    }
                }
                Console.WriteLine($"{signedCount} packages were signed out of {totalPackageCount}.");
                Console.WriteLine($"That's {(float)signedCount / totalPackageCount:P1} of the {totalPackageCount} packages");
            }
            finally
            {
                try
                {
                    Directory.Delete(tempPackageDirectory, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"I tried to be friendly to the environment but cleaning up the directory '{tempPackageDirectory}' failed. {e}");
                }
            }
            Console.WriteLine("Done. Hit Enter to close.");
            Console.ReadLine();
        }

        static (string, int) RunProcessGetStandardOutAndExitCode(string fileName, string arguments)
        {
            using (var process = new Process())
            {
                var buffer = new StringBuilder();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = arguments
                };
                process.OutputDataReceived += (_, e) => buffer.Append(e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                return (buffer.ToString(), process.ExitCode);
            }
        }

        static List<string> GetMostDownloadedPackageIds(bool communityPackagesOnly)
        {
            var webClient = new WebClient();
            string page = webClient.DownloadString("https://www.nuget.org/stats/packages");

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(page);

            string filter = communityPackagesOnly ? "!showAllPackageDownloads()" : "showAllPackageDownloads";

            return htmlDocument.DocumentNode.SelectSingleNode($"//table[@data-bind='visible: {filter}']")
                        .Descendants("tr")
                        .Skip(1) // Header

                        .Where(tr => tr.Elements("td").Count() > 1)
                        .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                        .Select(cell => cell[1])
                        .GroupBy(id => id.IndexOf('.') < 0 ? id : id.Substring(0, id.IndexOf('.')))
                        .Select(g => g.First())
                        .Take(packageDownloadCount)
                        .ToList();
        }
    }
}
