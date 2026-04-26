namespace GarageFlow.Tests.Integration.Api.Vehicles.Contracts;

public sealed record ProblemDetailsResponse(
    string? Title,
    string? Detail,
    int? Status);
