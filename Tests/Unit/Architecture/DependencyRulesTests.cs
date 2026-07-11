using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;
using NetArchTest.Rules;

namespace GarageFlow.Tests.Unit.Architecture;

public class DependencyRulesTests
{
    private const string ApiNamespace = "GarageFlow.Adapters.Api";
    private const string ApplicationNamespace = "GarageFlow.Application";
    private const string DomainNamespace = "GarageFlow.Domain";
    private const string HostNamespace = "GarageFlow.Host";
    private const string InfrastructureNamespace = "GarageFlow.Adapters.Infrastructure";
    private const string SharedKernelNamespace = "GarageFlow.SharedKernel";
    private const string TestsNamespace = "GarageFlow.Tests";
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    [Fact]
    public void SharedKernel_ShouldNotDependOnAnyProductionProject()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.SharedKernel", "SharedKernel"))
            .That()
            .ResideInNamespaceStartingWith(SharedKernelNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApiNamespace,
                ApplicationNamespace,
                DomainNamespace,
                HostNamespace,
                InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(SharedKernel_ShouldNotDependOnAnyProductionProject));
    }

    [Fact]
    public void Domain_ShouldOnlyDependOnSharedKernel()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Domain", "Domain"))
            .That()
            .ResideInNamespaceStartingWith(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, ApplicationNamespace, HostNamespace, InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(Domain_ShouldOnlyDependOnSharedKernel));
    }

    [Fact]
    public void Application_ShouldOnlyDependOnDomainAndSharedKernel()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Application", "Application"))
            .That()
            .ResideInNamespaceStartingWith(ApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, HostNamespace, InfrastructureNamespace)
            .GetResult();

        AssertRule(result, nameof(Application_ShouldOnlyDependOnDomainAndSharedKernel));
    }

    [Fact]
    public void ApiAdapter_ShouldOnlyDependOnApplication()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, HostNamespace, InfrastructureNamespace, SharedKernelNamespace)
            .GetResult();

        AssertRule(result, nameof(ApiAdapter_ShouldOnlyDependOnApplication));
    }

    [Fact]
    public void InfrastructureAdapter_ShouldOnlyDependOnApplicationDomainAndSharedKernel()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Infrastructure", "Adapters.Infrastructure"))
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNamespace, HostNamespace)
            .GetResult();

        AssertRule(result, nameof(InfrastructureAdapter_ShouldOnlyDependOnApplicationDomainAndSharedKernel));
    }

    [Fact]
    public void Host_ShouldOnlyDependOnAllowedCompositionRootProjects()
    {
        var projectReferences = GetProjectReferences("Host", "GarageFlow.Host.csproj");
        var allowedReferences = new[]
        {
            Path.Combine("Adapters.Api", "GarageFlow.Adapters.Api.csproj"),
            Path.Combine("Adapters.Infrastructure", "GarageFlow.Adapters.Infrastructure.csproj"),
            Path.Combine("Application", "GarageFlow.Application.csproj"),
            Path.Combine("SharedKernel", "GarageFlow.SharedKernel.csproj")
        };

        Assert.Equal(
            allowedReferences.Order(StringComparer.OrdinalIgnoreCase),
            projectReferences.Order(StringComparer.OrdinalIgnoreCase));

        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Host", "Host"))
            .That()
            .ResideInNamespaceStartingWith(HostNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, TestsNamespace)
            .GetResult();

        AssertRule(result, nameof(Host_ShouldOnlyDependOnAllowedCompositionRootProjects));
    }

    [Fact]
    public void Api_Endpoints_ShouldDependOnApplicationLayer()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
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
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .And()
            .HaveNameEndingWith("Endpoint")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, SharedKernelNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_Endpoints_ShouldNotDependOnDomainOrInfrastructure_OutsideKnownDrift));
    }

    [Fact]
    public void Api_ModuleRegistrationTypes_ShouldNotDependOnDomainOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
            .That()
            .ResideInNamespaceStartingWith(ApiNamespace)
            .And()
            .HaveNameEndingWith("Endpoints")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, SharedKernelNamespace)
            .GetResult();

        AssertRule(result, nameof(Api_ModuleRegistrationTypes_ShouldNotDependOnDomainOrInfrastructure));
    }

    [Fact]
    public void Api_SecurityTypes_ShouldNotDependOnDomainOrInfrastructure()
    {
        var result = Types
            .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
            .That()
            .ResideInNamespaceStartingWith($"{ApiNamespace}.Security")
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, SharedKernelNamespace)
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

    private static string[] GetProjectReferences(string projectFolder, string projectFileName)
    {
        var projectFilePath = Path.Combine(RepositoryRoot, projectFolder, projectFileName);
        var projectDirectory = Path.GetDirectoryName(projectFilePath);
        Assert.True(projectDirectory is not null, $"Project directory was not found for '{projectFilePath}'.");

        return XDocument.Load(projectFilePath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar))
            .Select(include => Path.GetRelativePath(RepositoryRoot, Path.GetFullPath(include, projectDirectory)))
            .Select(include => include.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar))
            .ToArray();
    }
}
