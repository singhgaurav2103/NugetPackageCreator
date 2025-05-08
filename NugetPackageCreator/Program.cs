
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace NugetPackageCreatorTool
{
    internal partial class NugePackageTool
    {
        static Dictionary<string, bool> KeepCacheOfUpdatedNugetPackage = new Dictionary<string, bool>();
        static Queue<DependencyConfiguration> dependencyConfigurationsQueue = new Queue<DependencyConfiguration>();
        static int TotalDepencies = 0;
        static int TotalResolvedDependencies = 0;
        static string CurrentlatestUniqueVersion;
        static void Main(string[] args)
        {
            Console.WriteLine($"Started At -{DateTime.Now}");
            CurrentlatestUniqueVersion = DateTime.Now.ToString("yyyyMMddTHHmmssfff");
            var configurationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DependencyConfiguration.json");
            string jsonContent = File.ReadAllText(configurationFilePath);

            // Deserialize JSON to class model
            Dependencies dependencies = JsonConvert.DeserializeObject<Dependencies>(jsonContent);
            TotalDepencies = dependencies.DependencyConfiguration.Count;

            var ProjectInitialPath = ConfigurationManager.AppSettings["ProjectInitialPath"];
            var outPutPath = ConfigurationManager.AppSettings["LocalNuGet"];

             CleanSolution(dependencies, ProjectInitialPath);

            //e.g -: Mark dependency CreatePackage to not create in list if user has asked.
            MarkPackageToNotCreate(dependencies);

            //Check If Nuget Source Exist if not create.
            if(!dependencies.NugetSourceAlreadyExists)
            {
                AddLocalNuGetSource(ConfigurationManager.AppSettings["LocalNugetName"], outPutPath);
            }
            CreateInitialeDependentIndegreeAndAddInQueueIfZeroIndegree(dependencies);
            PointLatestNugetPackageAndBuildProject(ProjectInitialPath, outPutPath, dependencies);

            FinishPackageCreation(dependencies, ProjectInitialPath);

            Console.ReadLine();

        }
        
        static void MarkPackageToNotCreate(Dependencies dependencies)
        {
            foreach (var solutionIgnoreDict in dependencies.IgnoreCreatingPackageToSolution)
            {
                foreach (var kvp in solutionIgnoreDict)
                {
                    if (kvp.Value) // only act if the flag is true
                    {
                        foreach (var dependency in dependencies.DependencyConfiguration
                            .Where(x => x.SolutionTrailingPath == kvp.Key))
                        {
                            dependency.CreatePackage = false;
                        }
                    }
                }
            }

        }
        static void PointLatestNugetPackageAndBuildProject(string ProjectInitialPath, string outPutPath,
           Dependencies dependencies)
        {
            while (dependencyConfigurationsQueue.Count > 0)
            {
                var dependencyConfiguration = dependencyConfigurationsQueue.Dequeue();
                var projectPath = Path.Combine(ProjectInitialPath, dependencyConfiguration.TrailingPath);

                //Point to latest nuget package.
                updatePackageToProjects(dependencies, ProjectInitialPath, dependencyConfiguration);

                if (dependencyConfiguration.CreatePackage)
                {
                    //Create Package.
                    CreatePackage(projectPath, outPutPath, dependencyConfiguration.ID,
                        dependencies.CurrentVersion, dependencies.UseForceSymbols);
                }
                TotalResolvedDependencies++;
                UpdateDependentIndegreeAndAddInQueueIfZeroIndegree(dependencyConfiguration.ID, dependencies);
            }
        }
        static void CreateInitialeDependentIndegreeAndAddInQueueIfZeroIndegree(Dependencies dependencies)
        {
            foreach (var dependencyConfiguration in dependencies.DependencyConfiguration)
            {
                if (dependencyConfiguration.DependentPackageIDs == null
                    || dependencyConfiguration.DependentPackageIDs.Count == 0)
                {
                    dependencyConfigurationsQueue.Enqueue(dependencyConfiguration);
                }
                else
                {
                    dependencyConfiguration.DependentIndegree = dependencyConfiguration.DependentPackageIDs.Count;
                }
            }
        }
        static void UpdateDependentIndegreeAndAddInQueueIfZeroIndegree(string createdPackageName, Dependencies dependencies)
        {
            foreach (var dependencyConfiguration in dependencies.DependencyConfiguration)
            {
                if (dependencyConfiguration.DependentIndegree > 0)
                {
                    // Check if the createdPackageName exists in any dictionary in DependentPackageIDs
                    if (dependencyConfiguration.DependentPackageIDs.Any(dict => dict.ContainsKey(createdPackageName)))
                    {
                        dependencyConfiguration.DependentIndegree--;

                        // If the indegree becomes zero, enqueue the dependency
                        if (dependencyConfiguration.DependentIndegree == 0)
                        {
                            dependencyConfigurationsQueue.Enqueue(dependencyConfiguration);
                        }
                    }
                }
            }
        }
        static void updatePackageToProjects(Dependencies dependencies, string ProjectInitialPath, DependencyConfiguration dependencyConfiguration)
        {
            if (dependencyConfiguration?.DependentPackageIDs != null &&
                dependencyConfiguration.DependentPackageIDs.Count > 0 &&
                NeedToAddNugetPackage(dependencyConfiguration, dependencies))
            {
                var projectPath = Path.Combine(ProjectInitialPath, dependencyConfiguration.TrailingPath);
                string latestVersion = null;

                // Iterate over each dictionary in DependentPackageIDs
                foreach (var packageDict in dependencyConfiguration.DependentPackageIDs)
                {
                    foreach (var kvp in packageDict)
                    {
                        string packageName = kvp.Key;
                        bool isEnabled = kvp.Value;

                        // Only process the package if it is enabled
                        if (isEnabled &&  KeepCacheOfUpdatedNugetPackage.ContainsKey(packageName) &&
                            KeepCacheOfUpdatedNugetPackage[packageName])
                        {
                            latestVersion = PointToLatestVersion(projectPath, packageName);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(latestVersion))
                {
                    string currentVersion = $"{latestVersion}-{CurrentlatestUniqueVersion}";

                       if(dependencies.UseForceSymbols && dependencyConfiguration.AddSymbolsScriptToProject)
                        CheckAndCopyPdbFilesScriptAddInCsProj(projectPath, ConfigurationManager.AppSettings["LocalNuGet"]);

                    RestoreProject(projectPath, currentVersion);
                    Console.WriteLine("Project restored with updated local package.");
                }
            }
        }

        static void FinishPackageCreation(Dependencies dependencies, string ProjectInitialPath)
        {
            if (TotalDepencies != TotalResolvedDependencies)
            {
                var circularDepenciesNameWithCommaSeprated = string.Join(",",
                                                    dependencies.DependencyConfiguration
                                                    .Where(x => x.DependentIndegree != 0)
                                                    .Select(x => x.ID));
                Console.WriteLine($"Finished with few dependency did not resolved due to circular relation provided in DependencyConfiguration.json file -{circularDepenciesNameWithCommaSeprated} ");

            }
            else
            {
                //Restore Solutions after package is created and added in all the solutions
                foreach (var reStoreSolutionsDictionary in dependencies.ReStoreSolution)
                {
                    foreach (var solutionKeyPair in reStoreSolutionsDictionary)
                    {
                        if (solutionKeyPair.Value)
                        {
                            var solutionPath = Path.Combine(ProjectInitialPath,
                            solutionKeyPair.Key);

                            RestoreSolution(solutionPath);
                        }
                    }
                }
                //Rebuild Solutions after package is created and added in all the solutions
                foreach (var rebuildSolutionsDictionary in dependencies.RebuildSolution)
                {
                    foreach(var solutionKeyPair in rebuildSolutionsDictionary)
                    {
                        if(solutionKeyPair.Value)
                        {
                            var solutionPath = Path.Combine(ProjectInitialPath,
                            solutionKeyPair.Key);

                            ReBuildSolutionWithMSBuild(solutionPath);
                        }
                    }
                }                  

                Console.WriteLine($"Finished At {DateTime.Now}");
            }
        }
    }
}
