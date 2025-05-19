using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NugetPackageCreatorTool
{
    internal partial class NuGetPackageTool
    {
        static void RestoreOriginalCSPROJFiles(string projectInitialPath,
            Dependencies dependencies, string outputPath)
        {
            Dictionary<string, bool> dictionaryOfIgnoaredAddingSolution = new Dictionary<string, bool>();
            foreach (var dependency in dependencies.DependencyConfiguration)
            {
                var solution = dependency.SolutionTrailingPath;
                if (!dictionaryOfIgnoaredAddingSolution.ContainsKey(solution))
                {
                    var ignoreAddingPackageToSolution = dependencies.IgnoreAddingPackageToSolution
                        .FirstOrDefault(d => d != null && d.ContainsKey(solution));

                    if (ignoreAddingPackageToSolution != null &&
                        !ignoreAddingPackageToSolution[solution] && dependency.AddDependentNugetPackage
                        && dependency.DependentPackageIDs.Count > 0)
                    {
                        RestoreToOriginalVersion(projectInitialPath, dependencies, dependency);
                        dictionaryOfIgnoaredAddingSolution.Add(solution, ignoreAddingPackageToSolution[solution]);
                    }

                }
                else if (dependency.DependentPackageIDs.Count > 0
                      && dependency.AddDependentNugetPackage && !dictionaryOfIgnoaredAddingSolution[solution])
                {
                    RestoreToOriginalVersion(projectInitialPath, dependencies, dependency);
                }
            }
        }
        static void RestoreToOriginalVersion(string projectInitialPath,
            Dependencies dependencies, DependencyConfiguration dependency)
        {
            var projectPath = Path.Combine(projectInitialPath, dependency.TrailingPath);
            bool hasAnyDependentPackage = false;
            foreach (var packageDict in dependency.DependentPackageIDs)
            {
                foreach (var kvp in packageDict)
                {
                    string packageName = kvp.Key;
                    bool isEnabled = kvp.Value;
                    if (isEnabled)
                    {
                        hasAnyDependentPackage = true;
                        var packageOriginalVersion = GetPackageVersionFromXml(packageName);
                        if (!string.IsNullOrEmpty(packageOriginalVersion))
                        {
                            Console.WriteLine($"Rerefrencing {packageName} to version {packageOriginalVersion} at {projectPath}");
                            UpdateProjectFileAndReturnOriginalVersionOfAddedPackage(projectPath, packageName,
                                packageOriginalVersion);
                        }
                    }
                }
            }
            if (hasAnyDependentPackage && dependencies.UseForceSymbols && dependency.AddSymbolsScriptToProject)
            {
                XDocument projectFile = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
                RemoveTargetByName(projectFile, "CopyPdbFilesFromSymbols", projectPath);
                RemoveTargetByName(projectFile, "CleanPdbFilesBeforeBuild", projectPath);
            }

        }
        static void RemoveTargetByName(XDocument projectFile, string targetName, string projectPath)
        {
            var targetsToRemove = projectFile.Descendants()
                .Where(e => e.Name.LocalName == "Target" && e.Attribute("Name")?.Value == targetName)
                .ToList();

            foreach (var target in targetsToRemove)
            {
                Console.WriteLine($"Removing existing Target '{targetName} from {projectPath}'...");
                target.Remove();
            }

            // Read original encoding and BOM presence
            string content;
            Encoding encoding;
            bool hasUtf8Bom;
            (content, encoding, hasUtf8Bom) = ReadFileWithEncodingAndBom(projectPath);
            bool hasXmlDeclaration = content.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            Encoding encodingToUse;
            if (encoding is UTF8Encoding)
            {
                encodingToUse = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasUtf8Bom);
            }
            else
            {
                encodingToUse = encoding;
            }
            // Step 3: Save using original encoding and declaration presence
            var xmlWriterSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = !hasXmlDeclaration,
                Encoding = encodingToUse,
                Indent = true
            };

            RetryFileAccess(() =>
            {
                using (var stream = new StreamWriter(projectPath, false, xmlWriterSettings.Encoding))
                using (var writer = XmlWriter.Create(stream, xmlWriterSettings))
                {
                    projectFile.Save(writer);
                }
            });
        }
    }
}
