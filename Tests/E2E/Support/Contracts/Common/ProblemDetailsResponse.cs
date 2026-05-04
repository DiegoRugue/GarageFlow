namespace GarageFlow.Tests.E2E.Support.Contracts.Common;

public sealed record ProblemDetailsResponse(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);
