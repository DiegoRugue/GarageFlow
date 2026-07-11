using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.InventoryItems.UseCases.DeleteInventoryItem;

public sealed record DeleteInventoryItemCommand(Guid Id) : ICommand;
