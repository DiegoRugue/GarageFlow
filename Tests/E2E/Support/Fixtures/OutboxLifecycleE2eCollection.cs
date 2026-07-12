namespace GarageFlow.Tests.E2E.Support.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OutboxLifecycleE2eCollectionDefinition : ICollectionFixture<OutboxLifecycleE2eFixture>
{
    public const string Name = "Outbox lifecycle E2E";
}
