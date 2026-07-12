namespace GarageFlow.Application.WorkOrders.Ports;

public sealed record IntakeRequestClaim(
    IntakeRequestClaimState State,
    string PayloadHash,
    string? ResponseJson);
