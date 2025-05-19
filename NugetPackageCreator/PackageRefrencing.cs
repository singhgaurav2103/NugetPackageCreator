using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace NugetPackageCreatorTool
{
    internal partial class NuGetPackageTool
    {
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
                updatePackageToProjects(dependencies, ProjectInitialPath, dependencyConfiguration, outPutPath);

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
        static void updatePackageToProjects(Dependencies dependencies, string ProjectInitialPath,
            DependencyConfiguration dependencyConfiguration, string outPutPath)
        {
            if (dependencyConfiguration?.DependentPackageIDs != null &&
                dependencyConfiguration.DependentPackageIDs.Count > 0 &&
                NeedToAddNugetPackage(dependencyConfiguration, dependencies))
            {
                var projectPath = Path.Combine(ProjectInitialPath, dependencyConfiguration.TrailingPath);
                var dependencyBackUpPath = Path.Combine(outPutPath, string.Concat(dependencyConfiguration.ID, ".csproj"));
                string ToolVersionIdentifier = ConfigurationManager.AppSettings["ToolVersionIdentifier"];
                bool hasAnyDependentPackage = false;
                // Iterate over each dictionary in DependentPackageIDs
                foreach (var packageDict in dependencyConfiguration.DependentPackageIDs)
                {
                    foreach (var kvp in packageDict)
                    {
                        string packageName = kvp.Key;
                        bool isEnabled = kvp.Value;

                        // Only process the package if it is enabled
                        if (isEnabled && KeepCacheOfUpdatedNugetPackage.ContainsKey(packageName) &&
                            KeepCacheOfUpdatedNugetPackage[packageName])
                        {
                            hasAnyDependentPackage = true;
                            string originalVersion = PointToLatestVersionIfItIsOriganlCSPROJ(projectPath, packageName);
                            if (!string.IsNullOrEmpty(originalVersion) && !keepTrackOnUpdatedPackageOriginalVersion.ContainsKey(packageName)
                             && !originalVersion.Contains(ToolVersionIdentifier))
                            {
                                AddOrUpdatePackageInXml(packageName, originalVersion);
                            }
                        }
                    }
                }

                if (hasAnyDependentPackage &&
                    dependencies.UseForceSymbols && dependencyConfiguration.AddSymbolsScriptToProject)
                    CheckAndCopyPdbFilesScriptAddInCsProj(projectPath, ConfigurationManager.AppSettings["LocalNuGet"]);


                RestoreProject(projectPath);
                Console.WriteLine("Project restored with updated local package.");

            }
        }
        static void ReStoreSolution(Dependencies dependencies, string ProjectInitialPath)
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
        }
        static void ReBuildSolution(Dependencies dependencies, string ProjectInitialPath)
        {
            //Rebuild Solutions after package is created and added in all the solutions
            foreach (var rebuildSolutionsDictionary in dependencies.RebuildSolution)
            {
                foreach (var solutionKeyPair in rebuildSolutionsDictionary)
                {
                    if (solutionKeyPair.Value)
                    {
                        var solutionPath = Path.Combine(ProjectInitialPath,
                        solutionKeyPair.Key);

                        ReBuildSolutionWithMSBuild(solutionPath);
                    }
                }
            }

        }
    }
}
