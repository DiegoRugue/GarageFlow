using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.UpdateVehicleBrand;

public sealed class UpdateVehicleBrandHandler(
    IVehicleBrandRepository vehicleBrandRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleBrandCommand, UpdateVehicleBrandResult>
{
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<UpdateVehicleBrandResult> Handle(UpdateVehicleBrandCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = VehicleBrandName.Create(request.Name);

        var vehicleBrandId = VehicleBrandId.From(request.Id);
        var vehicleBrand = await _vehicleBrandRepository.GetByIdAsync(vehicleBrandId, cancellationToken);
        if (vehicleBrand is null)
        {
            throw new NotFoundException($"Vehicle brand with ID '{request.Id}' was not found.");
        }

        var exists = await _vehicleBrandRepository.ExistsByNameAsync(normalizedName, vehicleBrandId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle brand with name '{normalizedName}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            vehicleBrand.Update(normalizedName);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateVehicleBrandResult(
                Id: vehicleBrand.Id.Value,
                Name: vehicleBrand.Name,
                CreatedAt: vehicleBrand.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
