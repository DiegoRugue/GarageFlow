using NetArchTest.Rules;
using System.Reflection;
using System.Runtime.Loader;

namespace GarageFlow.Tests.Unit.Architecture;

public class DependencyRulesTests
{
    private const string ApiNamespace = "GarageFlow.Api";
    private const string ApplicationNamespace = "GarageFlow.Application";
    private const string BuildingBlocksNamespace = "GarageFlow.BuildingBlocks";
    private const string DomainNamespace = "GarageFlow.Domain";
    private const string InfrastructureNamespace = "GarageFlow.Infrastructure";
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    [Fact]
    public void BuildingBlocks_ShouldNotDependOnBusinessLayers()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.BuildingBlocks", "BuildingBlocks"))
            .That()
            .ResideInNamespaceStartingWith(BuildingBlocksNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApiNamespace,
                ApplicationNamespace,
                DomainNamespace,
                InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(BuildingBlocks_ShouldNotDependOnBusinessLayers));
    }

    [Fact]
    public void Domain_ShouldNotDependOnApiOrApplicationOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Domain", "Domain"))
            .That()
            .ResideInNamespaceStartingWith(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, ApplicationNamespace, InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(Domain_ShouldNotDependOnApiOrApplicationOrInfrastructure));
    }

    [Fact]
    public void Application_ShouldNotDependOnApiOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Application", "Application"))
            .That()
            .ResideInNamespaceStartingWith(ApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(Application_ShouldNotDependOnApiOrInfrastructure));
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApiLayer()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Infrastructure", "Infrastructure"))
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace)
            .GetResult();

        AssertRule(result, nameof(Infrastructure_ShouldNotDependOnApiLayer));
    }

    [Fact]
    public void Api_Endpoints_ShouldDependOnApplicationLayer()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Api", "Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .And()
            .HaveNameEndingWith("Endpoint")
            .Should()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_Endpoints_ShouldDependOnApplicationLayer));
    }

    [Fact]
    public void Api_Endpoints_ShouldNotDependOnDomainOrInfrastructure_OutsideKnownDrift()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Api", "Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .And()
            .HaveNameEndingWith("Endpoint")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, BuildingBlocksNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_Endpoints_ShouldNotDependOnDomainOrInfrastructure_OutsideKnownDrift));
    }

    [Fact]
    public void Api_ModuleRegistrationTypes_ShouldNotDependOnDomainOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Api", "Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .And()
            .HaveNameEndingWith("Endpoints")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, BuildingBlocksNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_ModuleRegistrationTypes_ShouldNotDependOnDomainOrInfrastructure));
    }

    [Fact]
    public void Api_SecurityTypes_ShouldNotDependOnDomainOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Api", "Api"))
            .That()
            .ResideInNamespaceStartingWith($"{ApiNamespace}.Security")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, BuildingBlocksNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_SecurityTypes_ShouldNotDependOnDomainOrInfrastructure));
    }

    private static void AssertRule(TestResult result, string ruleName)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failingTypes = string.Join(", ", result.FailingTypeNames);
        Assert.True(result.IsSuccessful, $"{ruleName} failed for: {failingTypes}");
    }

    private static Assembly LoadAssembly(string assemblyName, string projectFolder)
    {
        var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));
        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        var binPath = Path.Combine(RepositoryRoot, projectFolder, "bin");
        var candidates = Directory.GetFiles(binPath, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        Assert.True(candidates.Length > 0, $"Assembly '{assemblyName}' was not found under '{binPath}'.");
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidates[0]);
    }
}
