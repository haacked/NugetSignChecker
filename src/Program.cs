using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace src
{
    class Program
    {
        const string nugetApiBaseAddress = "https://api.nuget.org/";
        const int packageDownloadCount = 100;

        static async Task Main(string[] args)
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
                    Console.WriteLine($"Downloading the top {packageDownloadCount} most popular community packages (by download count) to '{tempPackageDirectory}'");
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

                    var latestVersion = await GetPackageLatestVersion(packageId);
                    var packageFilePath = await DownloadPackage(packageId, latestVersion, tempPackageDirectory);
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

        static async Task<string> GetPackageLatestVersion(string id)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync($"{nugetApiBaseAddress}v3-flatcontainer/{id}/index.json");
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                var versions = JsonConvert.DeserializeObject<PackageVersions>(responseBody);
                return versions.Versions.LastOrDefault();
            }
        }

        static async Task<string> DownloadPackage(string id, string version, string destinationDirectory)
        {
            string nupkgFileName = $"{id}.{version}.nupkg";
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync($"{nugetApiBaseAddress}v3-flatcontainer/{id}/{version}/{nupkgFileName}"))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    string fileToWriteTo = Path.Combine(destinationDirectory, nupkgFileName);
                    using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    }

                    response.Content = null;
                    return fileToWriteTo;
                }
            }
        }
    }

    internal class PackageVersions
    {
        public string[] Versions { get; set; }
    }
}
