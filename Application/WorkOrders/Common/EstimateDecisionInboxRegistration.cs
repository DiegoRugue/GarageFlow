namespace GarageFlow.Application.WorkOrders.Common;

public sealed record EstimateDecisionInboxRegistration(
    bool IsNew,
    string StoredPayloadHash);
