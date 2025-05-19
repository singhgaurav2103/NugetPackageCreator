using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace NugetPackageCreatorTool
{
    internal partial class NuGetPackageTool
    {
        static Dictionary<string, bool> KeepCacheOfUpdatedNugetPackage = new Dictionary<string, bool>();
        static Dictionary<string, string> keepTrackOnUpdatedPackageOriginalVersion = new Dictionary<string, string>();
        static Queue<DependencyConfiguration> dependencyConfigurationsQueue = new Queue<DependencyConfiguration>();
        static int TotalDepencies = 0;
        static int TotalResolvedDependencies = 0;
        static string CurrentlatestUniqueVersion;
        static void Main(string[] args)
        {
            var ToolVersionIdentifier = ConfigurationManager.AppSettings["ToolVersionIdentifier"];
            CurrentlatestUniqueVersion = DateTime.Now.ToString("yyyyMMddTHHmmssfff") + "-" + ToolVersionIdentifier;
            var configurationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DependencyConfiguration.json");
            string jsonContent = File.ReadAllText(configurationFilePath);

            // Deserialize JSON to class model
            Dependencies dependencies = JsonConvert.DeserializeObject<Dependencies>(jsonContent);
            TotalDepencies = dependencies.DependencyConfiguration.Count;

            var ProjectInitialPath = ConfigurationManager.AppSettings["ProjectInitialPath"];
            var outPutPath = ConfigurationManager.AppSettings["LocalNuGet"];

            Console.WriteLine("Please select '1' for refrencing and '2' for Derfencing!");
            string option = Console.ReadLine();
            if (option == "1")
            {
                Console.WriteLine($"Refrencing Started... At {DateTime.Now} ");
                StartPackageRefrencing(dependencies, ProjectInitialPath, outPutPath);
                Console.WriteLine($"Finished At {DateTime.Now}");
            }
            else if (option == "2")
            {
                Console.WriteLine("Derefrencing Started... At {DateTime.Now}");
                StartPackageDeRefrencing(dependencies, ProjectInitialPath, outPutPath);
                Console.WriteLine($"Derefrencing Finished At {DateTime.Now}");
            }
            else
            {
                Console.WriteLine("Please select a valid option!");
            }
            Console.ReadLine();

        }

        static void StartPackageRefrencing(Dependencies dependencies, string ProjectInitialPath,
            string outPutPath)
        {
            CleanSolution(dependencies, ProjectInitialPath);

            //e.g -: Mark dependency CreatePackage to not create in list if user has asked.
            MarkPackageToNotCreate(dependencies);

            //Check If Nuget Source Exist if not create.
            if (!dependencies.NugetSourceAlreadyExists)
            {
                AddLocalNuGetSource(ConfigurationManager.AppSettings["LocalNugetName"], outPutPath);
            }
            CreateInitialeDependentIndegreeAndAddInQueueIfZeroIndegree(dependencies);
            PointLatestNugetPackageAndBuildProject(ProjectInitialPath, outPutPath, dependencies);

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
                ReStoreSolution(dependencies, ProjectInitialPath);
                ReBuildSolution(dependencies, ProjectInitialPath);
            }
            
        }
        static void StartPackageDeRefrencing(Dependencies dependencies, string ProjectInitialPath,
            string outPutPath)
        {

            RestoreOriginalCSPROJFiles(ProjectInitialPath, dependencies, outPutPath);
        }
    }
}
