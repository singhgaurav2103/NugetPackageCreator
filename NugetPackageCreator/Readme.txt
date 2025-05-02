# NuGet Package Creator

This application is designed to automate the creation and management of NuGet packages for various projects in a multi-solution environment. 
It provides configuration-driven support for creating packages, managing dependencies, and handling symbols files.

## Features

- **NuGet Package Creation**: Automatically create NuGet packages for projects based on the configuration.
- **Dependency Management**: Define and manage dependencies between projects.
- **Symbols Support**: Optionally include `.pdb` files for debugging symbols.
- **Solution Management**: Clean, Restore, rebuild, and manage solutions as part of the package creation process.
- **Customizable Configuration**: Use a JSON-based configuration file to define project-specific settings.

## Configuration

The application uses a `DependencyConfiguration.json` file to define the behavior for each project. 
Below are the key sections of the configuration:

### Global Settings

- `CurrentVersion`: Specifies the current version of the packages.
- `NugetSourceAlreadyExists`: Indicates if the NuGet source already exists and it will not be created.
- `UseForceSymbols`: Determines whether to force the inclusion of symbols.

### Project-Specific Settings

Each project is defined under the `DependencyConfiguration` array with the following properties:

- `TrailingPath`: The relative path to the project file.
- `CreatePackage`: Whether to create a NuGet package for the project.
- `ID`: The unique identifier for the project.
- `DependentPackageIDs`: A list of dependent packages required by the project.
- `AddDependentNugetPackage`: Whether to add dependent NuGet packages.
- `SolutionTrailingPath`: The relative path to the solution file.
- `AddSymbolsScriptToProject`: Whether to add a script for symbols to the project.
- `CreatePdbFile`: Whether to generate a `.pdb` file for the project.

### Solution Management

- `IgnoreAddingPackageToSolution`: Specifies solutions to ignore when adding packages.
- `IgnoreCreatingPackageToSolution`: Specifies solutions to ignore when creating packages.
- `ReStoreSolution`: Specifies solutions to restore before building.
- `RebuildSolution`: Specifies solutions to rebuild.

## App.Config Keys

The application also uses `App.Config` to store additional settings that are not part of the JSON configuration 
and are required for the application to function correctly. Below are the keys and their purposes:

- `LocalNugetName`: The API key used to publish packages to the NuGet repository.
- `LocalNuGet`: The Path of the NuGet source where packages will be published.
- `ProjectInitialPath`: This is the path of your working directory where all your projects are located.

## How to Use

1. **Configure the Application**:
   - Update the 'App.Config - AppSettings' and `DependencyConfiguration.json` file to match your 
   project's structure and requirements.

2. **Run the Application**:
   - Execute the application to create NuGet packages, manage dependencies, and handle symbols.

3. **Manage Solutions**:
   - Use the configuration to restore or rebuild solutions as needed.

## Example Configuration

Here is an example of a project configuration:
{ "CurrentVersion": "1.0.0", "NugetSourceAlreadyExists": true, "UseForceSymbols": false,
"DependencyConfiguration": [ { "TrailingPath": "src/ProjectA/ProjectA.csproj", "CreatePackage": true,
"ID": "ProjectA", "DependentPackageIDs": ["ProjectB", "ProjectC"], "AddDependentNugetPackage": true,
"SolutionTrailingPath": "solutions/ProjectA.sln", "AddSymbolsScriptToProject": false,
"CreatePdbFile": true }, { "TrailingPath": "src/ProjectB/ProjectB.csproj", "CreatePackage": true, 
"ID": "ProjectB", "DependentPackageIDs": [], "AddDependentNugetPackage": false, 
"SolutionTrailingPath": "solutions/ProjectB.sln", "AddSymbolsScriptToProject": true,
"CreatePdbFile": true } ], "IgnoreAddingPackageToSolution": ["solutions/ProjectC.sln"],
"IgnoreCreatingPackageToSolution": ["solutions/ProjectD.sln"], "ReStoreSolution": ["solutions/ProjectA.sln",
"solutions/ProjectB.sln"], "RebuildSolution": ["solutions/ProjectA.sln"] }

