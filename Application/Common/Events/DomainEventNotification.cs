using GarageFlow.SharedKernel.Domain.Events;
using Mediator;

namespace GarageFlow.Application.Common.Events;

public sealed record DomainEventNotification(DomainEvent DomainEvent) : INotification;
