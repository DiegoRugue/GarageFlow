using System.Diagnostics.CodeAnalysis;

namespace GarageFlow.Tests.E2E.Support.Fixtures;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The public name describes the xUnit collection fixture contract.")]
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2eApiCollection : ICollectionFixture<E2eApiFixture>
{
    public const string Name = "GarageFlow E2E API";
}
