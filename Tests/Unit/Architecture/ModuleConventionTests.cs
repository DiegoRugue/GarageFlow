namespace GarageFlow.Tests.Unit.Architecture;

public class ModuleConventionTests
{
    private static readonly string[] BusinessModules =
    [
        "Customers",
        "WorkOrders",
        "Services",
        "Vehicles",
        "InventoryItems",
        "Users"
    ];

    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    [Fact]
    public void CanonicalBusinessModules_ShouldExistAcrossLayers()
    {
        var missingPaths = new List<string>();

        foreach (var module in BusinessModules)
        {
            var requiredPaths = new[]
            {
                Path.Combine(RepositoryRoot, "Adapters.Api", module),
                Path.Combine(RepositoryRoot, "Application", module),
                Path.Combine(RepositoryRoot, "Domain", module),
                Path.Combine(RepositoryRoot, "Adapters.Infrastructure", module),
                Path.Combine(RepositoryRoot, "Tests", "Shared", module),
                Path.Combine(RepositoryRoot, "Tests", "Unit", module),
                Path.Combine(RepositoryRoot, "Tests", "Integration", "Api", module)
            };

            foreach (var path in requiredPaths)
            {
                if (!Directory.Exists(path))
                {
                    missingPaths.Add(path);
                }
            }
        }

        Assert.True(
            missingPaths.Count == 0,
            $"Missing canonical module paths: {string.Join(", ", missingPaths)}");
    }

    [Fact]
    public void Api_ModuleFolders_ShouldContainModuleRegistrationFile()
    {
        var modulesWithoutRegistration = new List<string>();
        var missingPaths = new List<string>();

        foreach (var module in BusinessModules)
        {
            var modulePath = Path.Combine(RepositoryRoot, "Adapters.Api", module);
            if (!Directory.Exists(modulePath))
            {
                missingPaths.Add(ToRelativePath(modulePath));
                continue;
            }

            var registrationFiles = Directory.GetFiles(modulePath, "*Endpoints.cs", SearchOption.TopDirectoryOnly);

            if (registrationFiles.Length == 0)
            {
                modulesWithoutRegistration.Add(module);
            }
        }

        AssertNoMissingPaths(missingPaths, nameof(Api_ModuleFolders_ShouldContainModuleRegistrationFile));

        Assert.True(
            modulesWithoutRegistration.Count == 0,
            $"Missing module endpoint registration file (*Endpoints.cs): {string.Join(", ", modulesWithoutRegistration)}");
    }

    [Fact]
    public void Api_Files_ShouldUseEndpointRequestResponseNaming()
    {
        var missingPaths = new List<string>();
        var invalidFiles = GetModuleFiles("Adapters.Api", missingPaths)
            .Where(file => !HasAnySuffix(file, "Endpoint.cs", "Endpoints.cs", "Request.cs", "Response.cs"))
            .Select(ToRelativePath)
            .ToArray();

        AssertNoMissingPaths(missingPaths, nameof(Api_Files_ShouldUseEndpointRequestResponseNaming));

        Assert.True(
            invalidFiles.Length == 0,
            $"API files outside naming convention: {string.Join(", ", invalidFiles)}");
    }

    [Fact]
    public void Application_Files_ShouldUseUseCaseNaming()
    {
        var missingPaths = new List<string>();
        var invalidFiles = GetModuleFiles("Application", missingPaths)
            .Where(file => !IsValidApplicationFile(file))
            .Select(ToRelativePath)
            .ToArray();

        AssertNoMissingPaths(missingPaths, nameof(Application_Files_ShouldUseUseCaseNaming));

        Assert.True(
            invalidFiles.Length == 0,
            $"Application files outside naming convention: {string.Join(", ", invalidFiles)}");
    }

    [Fact]
    public void Domain_ShouldNotContainRepositoryOrReadModelFolders()
    {
        var invalidPaths = new List<string>();

        foreach (var module in BusinessModules)
        {
            var repositoryPath = Path.Combine(RepositoryRoot, "Domain", module, "Repositories");
            if (Directory.Exists(repositoryPath))
            {
                invalidPaths.Add(ToRelativePath(repositoryPath));
            }

            var readModelsPath = Path.Combine(RepositoryRoot, "Domain", module, "ReadModels");
            if (Directory.Exists(readModelsPath))
            {
                invalidPaths.Add(ToRelativePath(readModelsPath));
            }
        }

        Assert.True(
            invalidPaths.Count == 0,
            $"Domain must not contain repository/read-model folders: {string.Join(", ", invalidPaths)}");
    }

    [Fact]
    public void Infrastructure_ConfigurationAndRepositoryFiles_ShouldFollowNaming()
    {
        var invalidFiles = new List<string>();
        var missingPaths = new List<string>();

        foreach (var module in BusinessModules)
        {
            var configurationPath = Path.Combine(RepositoryRoot, "Adapters.Infrastructure", module, "Configurations");
            var repositoryPath = Path.Combine(RepositoryRoot, "Adapters.Infrastructure", module, "Repositories");

            if (!Directory.Exists(configurationPath))
            {
                missingPaths.Add(ToRelativePath(configurationPath));
            }
            else
            {
                var configurationFiles = Directory.GetFiles(configurationPath, "*.cs", SearchOption.TopDirectoryOnly);
                invalidFiles.AddRange(configurationFiles
                    .Where(file => !file.EndsWith("EntityConfiguration.cs", StringComparison.Ordinal))
                    .Select(ToRelativePath));
            }

            if (!Directory.Exists(repositoryPath))
            {
                missingPaths.Add(ToRelativePath(repositoryPath));
            }
            else
            {
                var repositoryFiles = Directory.GetFiles(repositoryPath, "*.cs", SearchOption.TopDirectoryOnly);
                invalidFiles.AddRange(repositoryFiles
                    .Where(file => !HasAnySuffix(file, "Repository.cs", "Queries.cs"))
                    .Select(ToRelativePath));
            }
        }

        AssertNoMissingPaths(missingPaths, nameof(Infrastructure_ConfigurationAndRepositoryFiles_ShouldFollowNaming));

        Assert.True(
            invalidFiles.Count == 0,
            $"Infrastructure files outside naming convention: {string.Join(", ", invalidFiles)}");
    }

    private static IEnumerable<string> GetModuleFiles(string layerName, List<string> missingPaths)
    {
        foreach (var module in BusinessModules)
        {
            var modulePath = Path.Combine(RepositoryRoot, layerName, module);
            if (!Directory.Exists(modulePath))
            {
                missingPaths.Add(ToRelativePath(modulePath));
                continue;
            }

            foreach (var file in Directory.GetFiles(modulePath, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static bool HasAnySuffix(string filePath, params string[] suffixes)
    {
        return suffixes.Any(suffix => filePath.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static bool IsValidApplicationFile(string filePath)
    {
        var relativePath = ToRelativePath(filePath);
        var pathSegments = relativePath.Split('/');

        if (pathSegments.Length < 3)
        {
            return false;
        }

        if (!string.Equals(pathSegments[0], "Application", StringComparison.Ordinal))
        {
            return false;
        }

        if (pathSegments.Contains("UseCases") &&
            HasAnySuffix(filePath, "Command.cs", "Query.cs", "Handler.cs", "Result.cs", "Dto.cs"))
        {
            return true;
        }

        if (pathSegments.Contains("Ports") && Path.GetFileName(filePath).StartsWith('I'))
        {
            return true;
        }

        if (pathSegments.Contains("ReadModels") && HasAnySuffix(filePath, "ReadModel.cs", "Projection.cs", "Dto.cs"))
        {
            return true;
        }

        if (pathSegments.Contains("Events") && HasAnySuffix(filePath, "Handler.cs"))
        {
            return true;
        }

        if (pathSegments.Contains("Abstractions") && Path.GetFileName(filePath).StartsWith('I'))
        {
            return true;
        }

        return pathSegments.Contains("Common");
    }

    private static string ToRelativePath(string absolutePath)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, absolutePath);
        return relativePath.Replace('\\', '/');
    }

    private static void AssertNoMissingPaths(List<string> missingPaths, string testName)
    {
        Assert.True(
            missingPaths.Count == 0,
            $"{testName} cannot validate conventions because required directories are missing: {string.Join(", ", missingPaths)}");
    }
}
