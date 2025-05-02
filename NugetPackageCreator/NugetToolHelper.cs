using NuGet.Versioning;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace NugetPackageCreatorTool
{
    internal partial class NugePackageTool
    {
        private static void CleanAllSolution(Dependencies dependencies, string ProjectInitialPath)
        {
            var groupedBySolution = dependencies.DependencyConfiguration
                .GroupBy(dep => dep.SolutionTrailingPath)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var group in groupedBySolution)
            {
                bool isIgnoreAdd = false;
                var ignoreAddDict = dependencies.IgnoreAddingPackageToSolution
                    .FirstOrDefault(x => x.ContainsKey(group.Key));

                if (ignoreAddDict != null)
                    ignoreAddDict.TryGetValue(group.Key, out isIgnoreAdd);

                bool isIgnoreCreate = false;
                var ignoreCreateDict = dependencies.IgnoreCreatingPackageToSolution
                    .FirstOrDefault(x => x.ContainsKey(group.Key));

                if (ignoreCreateDict != null)
                    ignoreCreateDict.TryGetValue(group.Key, out isIgnoreCreate);

                if (!(isIgnoreAdd && isIgnoreCreate))
                {
                    foreach (var config in group.Value)
                    {
                        var SolutionPath = Path.Combine(ProjectInitialPath, config.TrailingPath);
                        Console.WriteLine($"Cleaning Solution: {SolutionPath}");
                        CleanSolutionWithMSBuild(SolutionPath);
                    }
                }
            }
        }
        static void CreatePackage(string projectPath, string outPutPath,
        string packageName, string version, bool useForceSymbols)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("Usage: NuGetPackageCreator <path to .csproj or .nuspec file>");
                return;
            }

            if (!File.Exists(projectPath))
            {
                Console.WriteLine("Error: The specified file does not exist.");
                return;
            }
            try
            {
                Console.WriteLine($"Updated version to: {version}-{CurrentlatestUniqueVersion}");

                if (projectPath.EndsWith(".csproj"))
                {
                    RebuildProject(projectPath);
                    CreateDotnetPackage(projectPath, outPutPath, version, useForceSymbols);
                    KeepCacheOfUpdatedNugetPackage.Add(packageName, true);
                }

                Console.WriteLine($"NuGet package created successfully in: {projectPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        static void AddLocalNuGetSource(string sourceName, string sourcePath)
        {
            Console.WriteLine("Checking NuGet source...");

            // Check existing sources
            string output = RunCommandWithOutput("dotnet", "nuget list source");

            if (!output.Contains(sourcePath))
            {
                Console.WriteLine($"Adding local NuGet source: {sourcePath}");
                RunProcess("dotnet", $"nuget add source \"{sourcePath}\" -n {sourceName}");
            }
            else
            {
                Console.WriteLine("Local NuGet source already exists.");
            }
        }
        static void CreateDotnetPackage(string inputPath, string outputPath,
            string version, bool useForceSymbols)
        {
            // Generate a unique version for the package
            string uniqueVersion = $"{version}-{CurrentlatestUniqueVersion}";
            string arguments = $"pack {inputPath} -p:PackageVersion={uniqueVersion} -o {outputPath} --include-symbols --include-source";
            Console.WriteLine($"Creating NuGet package with version: {uniqueVersion}");

            // Run the dotnet pack command
            RunProcess("dotnet", arguments);

            //Extract PDB Files from symbols.nupkg
            if (useForceSymbols)
                ExtractPdbFromSymbolsNupkg(Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}.{uniqueVersion}.symbols.nupkg"), outputPath);


        }
        static void RunProcess(string fileName, string arguments)
        {
            int maxRetries = 5; // Maximum number of retries
            int delayMilliseconds = 500; // Delay between retries
            int retries = 0;

            while (true)
            {
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = fileName;
                        process.StartInfo.Arguments = arguments;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        process.Start();

                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Command failed with error: {error}");
                        }

                        // If successful, break out of the retry loop
                        Console.WriteLine(output);
                        break;
                    }
                }
                catch (Exception ex) when (retries < maxRetries)
                {
                    retries++;
                    Console.WriteLine($"Error occurred while creating package {ex.ToString()}. Retrying {retries}/{maxRetries}...");
                    Thread.Sleep(delayMilliseconds);
                }

            }
        }
        static void RebuildProject(string inputPath)
        {
            string arguments = $"build {inputPath}";

            Console.WriteLine("Rebuilding the project...");
            RunProcess("dotnet", arguments);
            Console.WriteLine("Rebuild completed.");
        }

        static string PointToLatestVersion(string projectPath, string packageName)
        {
            string localNuGetRepo = ConfigurationManager.AppSettings["LocalNuGet"];
            if (!File.Exists(projectPath))
            {
                Console.WriteLine($"Error: The specified project file does not exist {projectPath}");
                return null;
            }

            if (!Directory.Exists(localNuGetRepo))
            {
                Console.WriteLine("Error: The specified local NuGet repository does not exist.");
                return null;
            }

            try
            {
                string latestVersion = GetLatestLocalNuGetVersion(localNuGetRepo, packageName);

                //That means no updated package is in local nuget package manager
                if (string.IsNullOrEmpty(latestVersion))
                    return null;
                Console.WriteLine($"Latest version of {packageName} in local repository: {latestVersion}");

                UpdateProjectFile(projectPath, packageName, latestVersion);
                Console.WriteLine($"{packageName} updated to version {latestVersion} in {projectPath}");
                return latestVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }
        static string GetLatestLocalNuGetVersion(string localNuGetRepo, string packageName)
        {
            var packageFiles = Directory.GetFiles(localNuGetRepo, $"{packageName}.*-{CurrentlatestUniqueVersion}.nupkg");
            if (!packageFiles.Any())
            {
                Console.WriteLine($"No versions of {packageName} found in the local repository.");
                return null;
            }

            var latestPackage = packageFiles
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name =>
                {
                    var packageNameWithoutGUID = name.Replace(CurrentlatestUniqueVersion.ToString(), "");
                    var splitPackageNameByDot = packageNameWithoutGUID.Split('.');
                    splitPackageNameByDot = splitPackageNameByDot
                        .Where(str => !Regex.IsMatch(str, @"\d")).ToArray();
                    var pckName = string.Join(".", splitPackageNameByDot);
                    return pckName == packageName;
                })
                .Where(name =>
                {
                    return name.IndexOf(CurrentlatestUniqueVersion.ToString()) > -1;
                })
                .Select(name => name.Substring(packageName.Length + 1))
                .Where(versionPart => NuGetVersion.TryParse(versionPart, out _))
                .Select(versionPart => NuGetVersion.Parse(versionPart))
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(latestPackage?.ToString()))
            {
                throw new Exception($"Failed to parse versions for {packageName} in the local repository.");
            }

            return latestPackage.ToString();
        }
        static void UpdateProjectFile(string projectPath, string packageName, string latestVersion)
        {
            if (projectPath.EndsWith(".csproj"))
            {
                RetryFileAccess(() =>
                {
                    XDocument projectFile = XDocument.Load(projectPath);

                    //For Normal csproj Files.
                    var packageReferenceWithInclude = projectFile.Descendants("PackageReference")
                                                      .FirstOrDefault(p => p.Attribute("Include")?.Value == packageName);
                    //For Web based csproj files.
                    var packageReference = projectFile.Descendants()
                    .Where(x => x.Name.LocalName == "PackageReference")
                    .FirstOrDefault(p => p.Attribute("Include")?.Value == packageName);


                    if (packageReferenceWithInclude != null)
                    {
                        packageReferenceWithInclude.SetAttributeValue("Version", latestVersion);
                        projectFile.Save(projectPath);
                    }
                    else if (packageReference != null)
                    {
                        var versionElement = packageReference.Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "Version");

                        if (versionElement != null)
                        {
                            // Update the existing Version element
                            versionElement.Value = latestVersion;
                            projectFile.Save(projectPath);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Package {packageName} not found in {projectPath}. Adding package reference.");
                        // If not found, you may want to add the package reference explicitly.
                        var itemGroup = projectFile.Descendants("ItemGroup").FirstOrDefault();
                        if (itemGroup != null)
                        {
                            itemGroup.Add(new XElement("PackageReference",
                                new XAttribute("Include", packageName),
                                new XAttribute("Version", latestVersion)
                            ));
                            projectFile.Save(projectPath);
                        }
                    }
                });
            }
            else if (projectPath.EndsWith("packages.config"))
            {
                RetryFileAccess(() =>
                {
                    XDocument configFile = XDocument.Load(projectPath);

                    var packageElement = configFile.Descendants("package")
                                                   .FirstOrDefault(p => p.Attribute("id")?.Value == packageName);

                    if (packageElement != null)
                    {
                        packageElement.SetAttributeValue("version", latestVersion);
                        configFile.Save(projectPath);
                    }
                    else
                    {
                        Console.WriteLine($"Package {packageName} not found in {projectPath}. Adding package reference.");
                        // If not found, you may want to add the package reference explicitly.
                        configFile.Root.Add(new XElement("package",
                            new XAttribute("id", packageName),
                            new XAttribute("version", latestVersion)
                        ));
                        configFile.Save(projectPath);
                    }
                });
            }
            else
            {
                throw new NotSupportedException("Unsupported project file type.");
            }
        }
        static void ExtractPdbFromSymbolsNupkg(string symbolsNupkgPath, string outputDirectory)
        {
            if (!File.Exists(symbolsNupkgPath))
            {
                Console.WriteLine("symbols.nupkg file not found.");
                return;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using (var archive = ZipFile.OpenRead(symbolsNupkgPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        string destinationPath = Path.Combine(outputDirectory, Path.GetFileName(entry.FullName));
                        // Delete the specific .pdb file if it already exists
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                            Console.WriteLine($"Deleted existing PDB file: {destinationPath}");
                        }

                        entry.ExtractToFile(destinationPath, overwrite: true);
                        Console.WriteLine($"Extracted: {entry.FullName} -> {destinationPath}");
                    }
                }
            }
        }
        static void CheckAndCopyPdbFilesScriptAddInCsProj(string projectPath,
            string pdbSourcePath)
        {
            if (!Directory.Exists(pdbSourcePath))
            {
                Console.WriteLine($"Source path does not exist: {pdbSourcePath}");
                return;
            }

            RetryFileAccess(() =>
            {
                string escapedPath = pdbSourcePath.Replace("/", "\\").Replace(@"\", @"\\");
                string wildcardPath = $@"{escapedPath}\\**\\*.pdb";
                XDocument projectFile = XDocument.Load(projectPath);
                var ns = projectFile.Root.Name.Namespace;

                // --- Add target to copy PDB files after build ---
                var copyTarget = projectFile.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Target" && e.Attribute("Name")?.Value == "CopyPdbFilesFromSymbols");
                if (copyTarget == null)
                {
                    var targetElement = new XElement(ns + "Target",
                        new XAttribute("Name", "CopyPdbFilesFromSymbols"),
                        new XAttribute("AfterTargets", "Build"),
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "PdbFiles",
                                new XAttribute("Include", wildcardPath)
                            )
                        ),
                        new XElement(ns + "Copy",
                            new XAttribute("SourceFiles", "@(PdbFiles)"),
                            new XAttribute("DestinationFolder", "$(OutputPath)"),
                            new XAttribute("Condition", "Exists('%(PdbFiles.Identity)')"),
                            new XAttribute("SkipUnchangedFiles", "true")
                        )
                    );

                    projectFile.Root.Add(targetElement);
                    Console.WriteLine("Added Target 'CopyPdbFilesFromSymbols' to project.");
                }
                else
                {
                    Console.WriteLine("Target 'CopyPdbFilesFromSymbols' already exists in project.");
                }
                // --- Add target to clean PDB files before build ---
                var cleanTarget = projectFile.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Target" && e.Attribute("Name")?.Value == "CleanPdbFilesBeforeBuild");

                if (cleanTarget == null)
                {
                    var cleanElement = new XElement(ns + "Target",
                        new XAttribute("Name", "CleanPdbFilesBeforeBuild"),
                        new XAttribute("BeforeTargets", "Clean;BeforeRebuild"),
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "PdbFilesToDeleteFromBin",
                                new XAttribute("Include", @"$(MSBuildProjectDirectory)\bin\**\*.pdb")
                            )
                        ),
                        new XElement(ns + "Delete",
                            new XAttribute("Files", "@(PdbFilesToDeleteFromBin)")
                        )
                    );

                    projectFile.Root.Add(cleanElement);
                    Console.WriteLine("Added Target 'CleanPdbFilesBeforeBuild' to project.");
                }
                else
                {
                    Console.WriteLine("Target 'CleanPdbFilesBeforeBuild' already exists in project.");
                }

                projectFile.Save(projectPath);
            });
        }
        static void RetryFileAccess(Action fileAccessAction, int maxRetries = 5, int delayMilliseconds = 500)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    fileAccessAction();
                    break;
                }
                catch (IOException ex) when (retries < maxRetries)
                {
                    retries++;
                    Console.WriteLine($"File access failed. Retrying {retries}/{maxRetries}...");
                    Thread.Sleep(delayMilliseconds);
                }
            }
        }
        static void RestoreProject(string projectPath, string version = null)
        {
            // Restore the project
            string arguments = $"restore {projectPath} --no-cache --force";
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine(output);
                    Console.WriteLine($"Restore failed: {error}");
                }


            }
        }
        static bool NeedToAddNugetPackage(DependencyConfiguration dependencyConfiguration,
            Dependencies dependencies)
        {
            bool needToAddPackage = true;
            foreach (var IgnoreAddingPackageDictionary in dependencies.IgnoreAddingPackageToSolution)
            {
                foreach (var solutionKeyPair in IgnoreAddingPackageDictionary)
                {
                    if (solutionKeyPair.Value && solutionKeyPair.Key == dependencyConfiguration.SolutionTrailingPath)
                    {
                        needToAddPackage = false;
                    }
                }
            }
            return needToAddPackage;
        }
        static void RestoreSolution(string solutionPath)
        {
            string nugetPath = GetNugetExePath();

            // Step 1: Restore NuGet packages
            Console.WriteLine($"Restoring NuGet packages for {solutionPath}...");
            RunProcess(nugetPath, $"restore \"{solutionPath}\"");
        }
        static void ReBuildSolutionWithMSBuild(string solutionPath)
        {
            string msbuildPath = GetMsBuildPath();

            Console.WriteLine($"Building the solution {solutionPath} using MSBuild...");
            RunProcess(msbuildPath, $"\"{solutionPath}\" /t:Build /p:Configuration=Debug /m");
            Console.WriteLine($"Build completed for solution {solutionPath}.");
        }
        static void CleanSolutionWithMSBuild(string solutionPath)
        {
            string msbuildPath = GetMsBuildPath();

            // Step 1: Clean the solution
            Console.WriteLine($"Cleaning the solution {solutionPath} using MSBuild...");
            RunProcess(msbuildPath, $"\"{solutionPath}\" /t:Clean /p:Configuration=Debug");
            Console.WriteLine($"Clean completed for solution {solutionPath}.");
        }
        static string GetMsBuildPath()
        {
            // Possible paths to look for MSBuild
            string[] possiblePaths = ConfigurationManager.AppSettings["MsBuildPath"]?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Check environment variables
            string msBuildPathFromEnv = Environment.GetEnvironmentVariable("MSBuildPath");
            if (!string.IsNullOrEmpty(msBuildPathFromEnv) && File.Exists(msBuildPathFromEnv))
            {
                return msBuildPathFromEnv;
            }

            // Check standard locations
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            Console.WriteLine("MSBuild.exe not found. Attempting to download and install Build Tools...");

            // Download and install Visual Studio Build Tools if MSBuild is not found
            InstallVisualStudioBuildTools();

            // Recheck paths after installation
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("MSBuild.exe still not found after attempting installation.");
        }
        static void InstallVisualStudioBuildTools()
        {
            string vsInstallerPath = Path.Combine(Path.GetTempPath(), "vs_buildtools.exe");
            string downloadUrl = "https://aka.ms/vs/17/release/vs_buildtools.exe";

            // Download the installer if not already present
            if (!File.Exists(vsInstallerPath))
            {
                Console.WriteLine($"Downloading Visual Studio Build Tools from {downloadUrl}...");
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile(downloadUrl, vsInstallerPath);
                }
            }

            // Run the installer with required workloads
            Console.WriteLine("Running Visual Studio Build Tools installer...");
            Process installer = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = vsInstallerPath,
                    Arguments = "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.MSBuildTools",
                    UseShellExecute = true,
                    Verb = "runas" // Run as administrator
                }
            };
            installer.Start();
            installer.WaitForExit();

            if (installer.ExitCode != 0)
            {
                throw new Exception("Failed to install Visual Studio Build Tools.");
            }

            Console.WriteLine("Visual Studio Build Tools installed successfully.");
        }
        private static string GetNugetExePath()
        {
            string nugetFileName = "nuget.exe";
            string nugetExePathdownloadUrl = ConfigurationManager.AppSettings["NugetExePathdownloadUrl"];
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", nugetFileName);

            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

            Console.WriteLine($"nuget.exe not found at {defaultPath}. Downloading from {nugetExePathdownloadUrl}...");

            // Create directory if it doesn't exist
            string directory = Path.GetDirectoryName(defaultPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var client = new WebClient())
            {
                client.DownloadFile(nugetExePathdownloadUrl, defaultPath);
            }

            if (File.Exists(defaultPath))
            {
                Console.WriteLine($"nuget.exe downloaded successfully to {defaultPath}");
                return defaultPath;
            }

            throw new FileNotFoundException("Failed to download nuget.exe.");
        }
        static string RunCommandWithOutput(string command, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
    }
}
