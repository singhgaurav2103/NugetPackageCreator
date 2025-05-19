using System.Xml.Linq;
using System;
using System.Linq;
using System.IO;

namespace NugetPackageCreatorTool
{
    internal partial class NuGetPackageTool
    {
        static void AddOrUpdatePackageInXml(string packageName, string packageVersion)
        {
            XDocument doc;
            string filePath = Path.Combine(Environment.CurrentDirectory, "PackageMetadataManager.xml");

            if (File.Exists(filePath))
            {
                doc = XDocument.Load(filePath);
            }
            else
            {
                doc = new XDocument(new XElement("Packages"));
            }

            var root = doc.Element("Packages");
            if (root == null)
            {
                root = new XElement("Packages");
                doc.Add(root);
            }

            var existingPackage = root.Elements("Package")
                .FirstOrDefault(pkg => (string)pkg.Element("Name") == packageName);

            if (existingPackage != null)
            {
                existingPackage.Element("Version")?.SetValue(packageVersion);
                Console.WriteLine($"Updated version of '{packageName}' to {packageVersion}");
            }
            else
            {
                var newPackage = new XElement("Package",
                    new XElement("Name", packageName),
                    new XElement("Version", packageVersion)
                );
                root.Add(newPackage);
                Console.WriteLine($"Added new package '{packageName}' with version {packageVersion}");
            }

            doc.Save(filePath);
        }
        public static string GetPackageVersionFromXml(string packageName)
        {
            string filePath = Path.Combine(Environment.CurrentDirectory, "PackageMetadataManager.xml");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"XML file not found: {filePath}");
                return null;
            }

            XDocument doc = XDocument.Load(filePath);
            XElement packagesElement = doc.Element("Packages");
            if (packagesElement == null)
                return null;

            var packageElement = packagesElement.Elements("Package")
                .FirstOrDefault(p => string.Equals(
                    (string)p.Element("Name"), packageName, StringComparison.OrdinalIgnoreCase));

            return packageElement?.Element("Version")?.Value;
        }

    }
}
