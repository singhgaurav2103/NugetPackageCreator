using Newtonsoft.Json;
using System.Collections.Generic;

namespace NugetPackageCreatorTool
{
    public class Dependencies
    {
        public string CurrentVersion { get; set; }
        public List<DependencyConfiguration> DependencyConfiguration { get; set; }
        public List<Dictionary<string, bool>> IgnoreCreatingPackageToSolution { get; set; }
        public List<Dictionary<string, bool>> IgnoreAddingPackageToSolution { get; set; }
        public List<Dictionary<string, bool>> RebuildSolution { get; set; }
        public List<Dictionary<string, bool>> ReStoreSolution { get; set; }
        public bool NugetSourceAlreadyExists { get; set; }
        public bool UseForceSymbols { get; set; }
    }


    public class DependencyConfiguration
    {
        public string TrailingPath { get; set; }
        public bool CreatePackage { get; set; }
        public string ID { get; set; }
        public List<Dictionary<string, bool>> DependentPackageIDs { get; set; }
        public bool AddDependentNugetPackage { get; set; }
        public string SolutionTrailingPath { get; set; }
        public bool AddSymbolsScriptToProject { get; set; }
        [JsonIgnore]
        public int DependentIndegree { get; set; }
    }
}
