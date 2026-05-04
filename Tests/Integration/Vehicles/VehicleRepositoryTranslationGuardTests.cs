using System.Reflection;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using GarageFlow.Infrastructure.Vehicles.Repositories;
using GarageFlow.Tests.Shared.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.Vehicles;

public sealed class VehicleRepositoryTranslationGuardTests
{
    [Fact]
    public void VehicleRepositories_ShouldAvoidStringMethodsThatNpgsqlCannotTranslateInQueries()
    {
        var repositoryDirectory = Path.Combine(
            FindSolutionRoot().FullName,
            "Infrastructure",
            "Vehicles",
            "Repositories");
        var repositoryFiles = Directory
            .EnumerateFiles(repositoryDirectory, "*.cs")
            .OrderBy(file => file)
            .ToArray();

        Assert.NotEmpty(repositoryFiles);

        var forbiddenPatterns = new[]
        {
            ".Name.Value.ToUpper()",
            ".ToUpperInvariant()",
            ".ToLowerInvariant()"
        };
        var violations = repositoryFiles
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => new { File = file, LineNumber = index + 1, Line = line })
                .Where(entry => forbiddenPatterns.Any(pattern =>
                    entry.Line.Contains(pattern, StringComparison.Ordinal)))
                .Select(entry => $"{Path.GetFileName(entry.File)}:{entry.LineNumber}: {entry.Line.Trim()}"))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Vehicle repository queries must avoid string APIs that Npgsql cannot translate. Violations:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void VehicleDetailsQuery_ShouldTranslate_WhenCreatedWithCustomerIdFilter()
    {
        using var dbContext = CreateNpgsqlDbContext();
        var repository = new VehicleRepository(dbContext);
        var method = typeof(VehicleRepository).GetMethod(
            "CreateVehicleDetailsQuery",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var customerId = CustomerId.New();

        Assert.NotNull(method);

        var query = Assert.IsAssignableFrom<IQueryable<VehicleDetailsReadModel>>(
            method!.Invoke(repository, new object?[] { null, customerId }));

        var sql = query.ToQueryString();

        Assert.Contains("""WHERE""", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""CustomerId""", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VehicleDetailsQuery_ShouldTranslate_WhenCreatedWithVehicleIdFilter()
    {
        using var dbContext = CreateNpgsqlDbContext();
        var repository = new VehicleRepository(dbContext);
        var method = typeof(VehicleRepository).GetMethod(
            "CreateVehicleDetailsQuery",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var vehicleId = VehicleId.New();

        Assert.NotNull(method);

        var query = Assert.IsAssignableFrom<IQueryable<VehicleDetailsReadModel>>(
            method!.Invoke(repository, new object?[] { vehicleId, null }));

        var sql = query.ToQueryString();

        Assert.Contains("""WHERE""", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""Id""", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistsByNameAsync_ShouldUseInMemorySafePath_WhenProviderIsNotNpgsql()
    {
        using var dbContext = CreateInMemoryDbContext(Guid.NewGuid().ToString("N"));

        var primaryBrand = new VehicleBrandBuilder().WithName("Fiat").Build();
        var secondaryBrand = new VehicleBrandBuilder().WithName("Ford").Build();
        var color = new VehicleColorBuilder().WithName("Black").Build();
        var primaryModel = new VehicleModelBuilder()
            .WithVehicleBrandId(primaryBrand.Id.Value)
            .WithName("Uno")
            .Build();
        var secondaryModel = new VehicleModelBuilder()
            .WithVehicleBrandId(secondaryBrand.Id.Value)
            .WithName("Uno")
            .Build();

        dbContext.VehicleBrands.AddRange(primaryBrand, secondaryBrand);
        dbContext.VehicleColors.Add(color);
        dbContext.VehicleModels.AddRange(primaryModel, secondaryModel);
        await dbContext.SaveChangesAsync();

        var brandRepository = new VehicleBrandRepository(dbContext);
        var colorRepository = new VehicleColorRepository(dbContext);
        var modelRepository = new VehicleModelRepository(dbContext);

        Assert.True(await brandRepository.ExistsByNameAsync(VehicleBrandName.Create("fiat")));
        Assert.False(await brandRepository.ExistsByNameAsync(VehicleBrandName.Create("fiat"), primaryBrand.Id));

        Assert.True(await colorRepository.ExistsByNameAsync(VehicleColorName.Create("black")));
        Assert.False(await colorRepository.ExistsByNameAsync(VehicleColorName.Create("black"), color.Id));

        Assert.True(await modelRepository.ExistsByNameAsync(primaryBrand.Id, VehicleModelName.Create("uno")));
        Assert.False(await modelRepository.ExistsByNameAsync(primaryBrand.Id, VehicleModelName.Create("uno"), primaryModel.Id));
    }

    private static DirectoryInfo FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GarageFlow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate GarageFlow.slnx.");
    }

    private static GarageFlowDbContext CreateNpgsqlDbContext()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=garageflow;Username=garageflow;Password=garageflow")
            .Options;

        return new GarageFlowDbContext(options);
    }

    private static GarageFlowDbContext CreateInMemoryDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new GarageFlowDbContext(options);
    }
}
