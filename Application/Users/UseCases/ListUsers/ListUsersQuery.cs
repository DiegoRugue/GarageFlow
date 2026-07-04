using Mediator;

namespace GarageFlow.Application.Users.UseCases.ListUsers;

public sealed record ListUsersQuery(int Page = 1, int PageSize = 20) : IRequest<ListUsersResult>;
