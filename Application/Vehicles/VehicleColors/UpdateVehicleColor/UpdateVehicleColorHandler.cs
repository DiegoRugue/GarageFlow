using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.UpdateVehicleColor;

public sealed class UpdateVehicleColorHandler(
    IVehicleColorRepository vehicleColorRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleColorCommand, UpdateVehicleColorResult>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<UpdateVehicleColorResult> Handle(UpdateVehicleColorCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = VehicleColorName.Create(request.Name);

        var vehicleColorId = VehicleColorId.From(request.Id);
        var vehicleColor = await _vehicleColorRepository.GetByIdAsync(vehicleColorId, cancellationToken);
        if (vehicleColor is null)
        {
            throw new NotFoundException($"Vehicle color with ID '{request.Id}' was not found.");
        }

        var exists = await _vehicleColorRepository.ExistsByNameAsync(normalizedName, vehicleColorId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle color with name '{normalizedName}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            vehicleColor.Update(normalizedName);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateVehicleColorResult(
                Id: vehicleColor.Id.Value,
                Name: vehicleColor.Name,
                CreatedAt: vehicleColor.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
