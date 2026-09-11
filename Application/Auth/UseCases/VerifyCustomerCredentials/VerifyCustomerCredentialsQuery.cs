using Mediator;

namespace GarageFlow.Application.Auth.UseCases.VerifyCustomerCredentials;

public sealed record VerifyCustomerCredentialsQuery(string Cpf, string Password)
    : IRequest<VerifyCustomerCredentialsResult>;
