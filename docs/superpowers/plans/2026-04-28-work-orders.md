# Work Orders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `WorkOrders` business module with customer portal approval, estimate lifecycle, inventory stock reservation/restoration, and customer-safe responses.

**Architecture:** Implement `WorkOrder` as the aggregate root with estimates and estimate lines as child entities. Keep state-machine invariants in Domain, orchestration in Application handlers, thin Minimal API endpoints in Api, and EF Core persistence in Infrastructure.

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, Mediator, EF Core 10 with Npgsql and InMemory tests, xUnit, Moq, NetArchTest.

---

## Scope Check

This plan implements one cohesive vertical slice: customer portal activation plus work orders. Portal activation is included because customer approval/rejection depends on a `Customer` user role and `User.CustomerId` link. Billing, scheduling, technician assignment, attachments, and staff approval override remain out of scope.

## File Structure

### Modify Existing Files

- `Domain/Users/Enums/UserRole.cs`: add `Customer`.
- `Domain/Users/Entities/User.cs`: add `CustomerId?`, role/link invariants, and customer user factory support.
- `Domain/Users/Events/UserCreated.cs`: include `CustomerId?`.
- `Infrastructure/Users/Configurations/UserEntityConfiguration.cs`: map `CustomerId`.
- `Domain/Users/Repositories/IUserRepository.cs`: add customer-link lookup methods.
- `Infrastructure/Users/Repositories/UserRepository.cs`: implement customer-link lookup methods.
- `Application/Users/CreateUser/CreateUserHandler.cs`: continue creating only staff users through admin endpoint and reject `Customer` role there.
- `Infrastructure/Auth/JwtTokenService.cs`: add customer id claim when present.
- `Api/Security/SecurityClaimTypes.cs`: add `CustomerId`.
- `Api/Security/SecurityRoles.cs`: add `Customer`.
- `Api/Security/SecurityPolicies.cs`: add `ActiveCustomer` and `ActiveStaff`.
- `Api/Security/AuthenticationExtensions.cs`: configure new policies.
- `Infrastructure/DataAccess/GarageFlowDbContext.cs`: add `WorkOrders` DbSet and configuration.
- `Infrastructure/DataAccess/DependencyInjection.cs`: register `IWorkOrderRepository`.
- `Api/Program.cs`: map customer portal activation and work order endpoints.
- `Tests/Unit/Architecture/ModuleConventionTests.cs`: add `WorkOrders` to canonical modules.
- `Tests/Shared/Users/UserBuilder.cs`: support `CustomerId`.
- `Domain/InventoryItems/Entities/InventoryItem.cs`: add stock decrease/increase behavior.
- `Domain/InventoryItems/Events/InventoryItemStockUpdated.cs`: reuse existing event for reservation/restoration.

### Create Domain Work Order Files

- `Domain/WorkOrders/Entities/WorkOrder.cs`
- `Domain/WorkOrders/Entities/Estimate.cs`
- `Domain/WorkOrders/Entities/EstimateInventoryLine.cs`
- `Domain/WorkOrders/Entities/EstimateServiceLine.cs`
- `Domain/WorkOrders/Enums/WorkOrderStatus.cs`
- `Domain/WorkOrders/Enums/EstimateStatus.cs`
- `Domain/WorkOrders/Events/WorkOrderCreated.cs`
- `Domain/WorkOrders/Events/WorkOrderStatusChanged.cs`
- `Domain/WorkOrders/Events/EstimateCreated.cs`
- `Domain/WorkOrders/Events/EstimateSubmitted.cs`
- `Domain/WorkOrders/Events/EstimateApproved.cs`
- `Domain/WorkOrders/Events/EstimateRejected.cs`
- `Domain/WorkOrders/Events/EstimateInventoryLineAdded.cs`
- `Domain/WorkOrders/Events/EstimateServiceLineAdded.cs`
- `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`
- `Domain/WorkOrders/Repositories/WorkOrderDetailsReadModel.cs`
- `Domain/WorkOrders/Repositories/WorkOrderEstimateReadModel.cs`
- `Domain/WorkOrders/Repositories/WorkOrderInventoryLineReadModel.cs`
- `Domain/WorkOrders/Repositories/WorkOrderServiceLineReadModel.cs`
- `Domain/WorkOrders/ValueObjects/WorkOrderId.cs`
- `Domain/WorkOrders/ValueObjects/EstimateId.cs`
- `Domain/WorkOrders/ValueObjects/EstimateInventoryLineId.cs`
- `Domain/WorkOrders/ValueObjects/EstimateServiceLineId.cs`
- `Domain/WorkOrders/ValueObjects/EstimateItemQuantity.cs`

### Create Application Files

- `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserCommand.cs`
- `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserHandler.cs`
- `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserResult.cs`
- `Application/WorkOrders/CreateWorkOrder/CreateWorkOrderCommand.cs`
- `Application/WorkOrders/CreateWorkOrder/CreateWorkOrderHandler.cs`
- `Application/WorkOrders/CreateWorkOrder/CreateWorkOrderResult.cs`
- `Application/WorkOrders/CreateEstimate/CreateEstimateCommand.cs`
- `Application/WorkOrders/CreateEstimate/CreateEstimateHandler.cs`
- `Application/WorkOrders/CreateEstimate/CreateEstimateResult.cs`
- `Application/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemCommand.cs`
- `Application/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemHandler.cs`
- `Application/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemResult.cs`
- `Application/WorkOrders/AddEstimateService/AddEstimateServiceCommand.cs`
- `Application/WorkOrders/AddEstimateService/AddEstimateServiceHandler.cs`
- `Application/WorkOrders/AddEstimateService/AddEstimateServiceResult.cs`
- `Application/WorkOrders/SubmitEstimate/SubmitEstimateCommand.cs`
- `Application/WorkOrders/SubmitEstimate/SubmitEstimateHandler.cs`
- `Application/WorkOrders/ApproveMyEstimate/ApproveMyEstimateCommand.cs`
- `Application/WorkOrders/ApproveMyEstimate/ApproveMyEstimateHandler.cs`
- `Application/WorkOrders/RejectMyEstimate/RejectMyEstimateCommand.cs`
- `Application/WorkOrders/RejectMyEstimate/RejectMyEstimateHandler.cs`
- `Application/WorkOrders/StartDiagnosis/StartDiagnosisCommand.cs`
- `Application/WorkOrders/StartDiagnosis/StartDiagnosisHandler.cs`
- `Application/WorkOrders/StartWork/StartWorkCommand.cs`
- `Application/WorkOrders/StartWork/StartWorkHandler.cs`
- `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderCommand.cs`
- `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderHandler.cs`
- `Application/WorkOrders/DeliverWorkOrder/DeliverWorkOrderCommand.cs`
- `Application/WorkOrders/DeliverWorkOrder/DeliverWorkOrderHandler.cs`
- `Application/WorkOrders/CancelWorkOrder/CancelWorkOrderCommand.cs`
- `Application/WorkOrders/CancelWorkOrder/CancelWorkOrderHandler.cs`
- `Application/WorkOrders/GetWorkOrderById/GetWorkOrderByIdQuery.cs`
- `Application/WorkOrders/GetWorkOrderById/GetWorkOrderByIdHandler.cs`
- `Application/WorkOrders/GetWorkOrderById/WorkOrderDetailsDto.cs`
- `Application/WorkOrders/GetWorkOrderById/WorkOrderEstimateDto.cs`
- `Application/WorkOrders/GetWorkOrderById/WorkOrderInventoryLineDto.cs`
- `Application/WorkOrders/GetWorkOrderById/WorkOrderServiceLineDto.cs`
- `Application/WorkOrders/ListWorkOrders/ListWorkOrdersQuery.cs`
- `Application/WorkOrders/ListWorkOrders/ListWorkOrdersHandler.cs`
- `Application/WorkOrders/ListWorkOrders/ListWorkOrdersResult.cs`
- `Application/WorkOrders/GetMyWorkOrderById/GetMyWorkOrderByIdQuery.cs`
- `Application/WorkOrders/GetMyWorkOrderById/GetMyWorkOrderByIdHandler.cs`
- `Application/WorkOrders/ListMyWorkOrders/ListMyWorkOrdersQuery.cs`
- `Application/WorkOrders/ListMyWorkOrders/ListMyWorkOrdersHandler.cs`
- `Application/WorkOrders/ListMyWorkOrders/ListMyWorkOrdersResult.cs`
- `Application/WorkOrders/CustomerWorkOrderDetailsDto.cs`
- `Application/WorkOrders/CustomerWorkOrderEstimateDto.cs`
- `Application/WorkOrders/CustomerWorkOrderInventoryLineDto.cs`
- `Application/WorkOrders/CustomerWorkOrderServiceLineDto.cs`

### Create Api Files

- `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserEndpoint.cs`
- `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserRequest.cs`
- `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserResponse.cs`
- `Api/WorkOrders/WorkOrderEndpoints.cs`
- `Api/WorkOrders/CreateWorkOrder/CreateWorkOrderEndpoint.cs`
- `Api/WorkOrders/CreateWorkOrder/CreateWorkOrderRequest.cs`
- `Api/WorkOrders/CreateWorkOrder/CreateWorkOrderResponse.cs`
- `Api/WorkOrders/CreateEstimate/CreateEstimateEndpoint.cs`
- `Api/WorkOrders/CreateEstimate/CreateEstimateResponse.cs`
- `Api/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemEndpoint.cs`
- `Api/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemRequest.cs`
- `Api/WorkOrders/AddEstimateInventoryItem/AddEstimateInventoryItemResponse.cs`
- `Api/WorkOrders/AddEstimateService/AddEstimateServiceEndpoint.cs`
- `Api/WorkOrders/AddEstimateService/AddEstimateServiceRequest.cs`
- `Api/WorkOrders/AddEstimateService/AddEstimateServiceResponse.cs`
- `Api/WorkOrders/SubmitEstimate/SubmitEstimateEndpoint.cs`
- `Api/WorkOrders/StartDiagnosis/StartDiagnosisEndpoint.cs`
- `Api/WorkOrders/StartWork/StartWorkEndpoint.cs`
- `Api/WorkOrders/CompleteWorkOrder/CompleteWorkOrderEndpoint.cs`
- `Api/WorkOrders/DeliverWorkOrder/DeliverWorkOrderEndpoint.cs`
- `Api/WorkOrders/CancelWorkOrder/CancelWorkOrderEndpoint.cs`
- `Api/WorkOrders/GetWorkOrderById/GetWorkOrderByIdEndpoint.cs`
- `Api/WorkOrders/ListWorkOrders/ListWorkOrdersEndpoint.cs`
- `Api/WorkOrders/Responses/WorkOrderDetailsResponse.cs`
- `Api/WorkOrders/Responses/WorkOrderEstimateResponse.cs`
- `Api/WorkOrders/Responses/WorkOrderInventoryLineResponse.cs`
- `Api/WorkOrders/Responses/WorkOrderServiceLineResponse.cs`
- `Api/WorkOrders/ListMyWorkOrders/ListMyWorkOrdersEndpoint.cs`
- `Api/WorkOrders/GetMyWorkOrderById/GetMyWorkOrderByIdEndpoint.cs`
- `Api/WorkOrders/ApproveMyEstimate/ApproveMyEstimateEndpoint.cs`
- `Api/WorkOrders/RejectMyEstimate/RejectMyEstimateEndpoint.cs`
- `Api/WorkOrders/Responses/CustomerWorkOrderDetailsResponse.cs`
- `Api/WorkOrders/Responses/CustomerWorkOrderEstimateResponse.cs`
- `Api/WorkOrders/Responses/CustomerWorkOrderInventoryLineResponse.cs`
- `Api/WorkOrders/Responses/CustomerWorkOrderServiceLineResponse.cs`

### Create Infrastructure Files

- `Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs`
- `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- EF-generated `WorkOrdersModule` migration files under `Infrastructure/DataAccess/Migrations`
- `Infrastructure/DataAccess/Migrations/GarageFlowDbContextModelSnapshot.cs`

### Create Test Files

- `Tests/Shared/WorkOrders/WorkOrderBuilder.cs`
- `Tests/Shared/WorkOrders/CreateWorkOrderRequest.cs`
- `Tests/Shared/WorkOrders/AddEstimateInventoryItemRequest.cs`
- `Tests/Shared/WorkOrders/AddEstimateServiceRequest.cs`
- `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`
- `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderDetailsResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderEstimateResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderInventoryLineResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderServiceLineResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderDetailsResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderEstimateResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderInventoryLineResponse.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderServiceLineResponse.cs`

---

### Task 1: Customer User Domain Foundation

**Files:**
- Modify: `Domain/Users/Enums/UserRole.cs`
- Modify: `Domain/Users/Entities/User.cs`
- Modify: `Domain/Users/Events/UserCreated.cs`
- Modify: `Tests/Shared/Users/UserBuilder.cs`
- Test: `Tests/Unit/Users/UserDomainTests.cs`

- [ ] **Step 1: Write failing domain tests for customer-user invariants**

Add these tests to `Tests/Unit/Users/UserDomainTests.cs`:

```csharp
[Fact]
public void Create_ShouldCreateCustomerUser_WhenCustomerIdIsProvided()
{
    var customerId = CustomerId.New();

    var user = User.Create(
        FullName.Create("Customer User"),
        Email.Create("customer.user@example.com"),
        new DateOnly(1990, 1, 1),
        UserRole.Customer,
        "hash",
        customerId);

    Assert.Equal(UserRole.Customer, user.Role);
    Assert.Equal(customerId, user.CustomerId);
}

[Fact]
public void Create_ShouldThrowValidationException_WhenCustomerRoleHasNoCustomerId()
{
    Assert.Throws<ValidationException>(() => User.Create(
        FullName.Create("Customer User"),
        Email.Create("customer.user@example.com"),
        new DateOnly(1990, 1, 1),
        UserRole.Customer,
        "hash"));
}

[Fact]
public void Create_ShouldThrowValidationException_WhenStaffRoleHasCustomerId()
{
    Assert.Throws<ValidationException>(() => User.Create(
        FullName.Create("Staff User"),
        Email.Create("staff.user@example.com"),
        new DateOnly(1990, 1, 1),
        UserRole.Attendant,
        "hash",
        CustomerId.New()));
}
```

Include `using GarageFlow.Domain.Customers.ValueObjects;` if it is missing.

- [ ] **Step 2: Run the focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Users.UserDomainTests"
```

Expected: FAIL because `UserRole.Customer`, `User.CustomerId`, and the overload accepting `CustomerId` do not exist.

- [ ] **Step 3: Implement the domain changes**

Update `Domain/Users/Enums/UserRole.cs`:

```csharp
namespace GarageFlow.Domain.Users.Enums;

public enum UserRole
{
    Admin = 1,
    Attendant = 2,
    Customer = 3
}
```

Update `Domain/Users/Events/UserCreated.cs`:

```csharp
using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Events;

public sealed record UserCreated(
    UserId UserId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    CustomerId? CustomerId,
    bool MustChangePassword,
    DateTime CreatedAt) : DomainEvent;
```

Update `Domain/Users/Entities/User.cs` by adding the `CustomerId` property, adding `CustomerId? customerId = null` to the private constructor and both `Create` overloads, calling `EnsureValidCustomerLink(role, customerId)`, and raising `UserCreated` with `CustomerId`.

The relevant final shape should be:

```csharp
public CustomerId? CustomerId { get; private set; }

private User(
    UserId id,
    FullName fullName,
    Email email,
    UserBirthDate birthDate,
    UserRole role,
    string passwordHash,
    bool mustChangePassword,
    CustomerId? customerId = null) : base(id)
{
    FullName = fullName;
    Email = NormalizeEmail(email);
    BirthDate = EnsureValidBirthDate(birthDate);
    Role = EnsureValidRole(role);
    CustomerId = EnsureValidCustomerLink(role, customerId);
    PasswordHash = EnsurePasswordHash(passwordHash);
    MustChangePassword = mustChangePassword;
}

public static User Create(
    FullName fullName,
    Email email,
    DateOnly birthDate,
    UserRole role,
    string passwordHash,
    CustomerId? customerId = null)
{
    return Create(
        fullName,
        email,
        UserBirthDate.Create(birthDate),
        role,
        passwordHash,
        customerId);
}

public static User Create(
    FullName fullName,
    Email email,
    UserBirthDate birthDate,
    UserRole role,
    string passwordHash,
    CustomerId? customerId = null)
{
    var user = new User(
        id: UserId.New(),
        fullName: fullName,
        email: email,
        birthDate: birthDate,
        role: role,
        passwordHash: passwordHash,
        mustChangePassword: true,
        customerId: customerId);

    user.RaiseDomainEvent(new UserCreated(
        UserId: user.Id,
        FullName: user.FullName.Value,
        Email: user.Email.Value,
        BirthDate: user.BirthDate.Value,
        Role: user.Role,
        CustomerId: user.CustomerId,
        MustChangePassword: user.MustChangePassword,
        CreatedAt: user.CreatedAt));

    return user;
}

private static CustomerId? EnsureValidCustomerLink(UserRole role, CustomerId? customerId)
{
    if (role == UserRole.Customer && customerId is null)
    {
        throw new ValidationException("Customer users must be linked to a customer.");
    }

    if (role != UserRole.Customer && customerId is not null)
    {
        throw new ValidationException("Only customer users can be linked to a customer.");
    }

    return customerId;
}
```

Add `using GarageFlow.Domain.Customers.ValueObjects;`.

Update `Tests/Shared/Users/UserBuilder.cs`:

```csharp
private CustomerId? _customerId;

public UserBuilder WithCustomerId(CustomerId customerId)
{
    _customerId = customerId;
    return this;
}

public User Build()
{
    var user = User.Create(
        fullName: FullName.Create(_fullName),
        email: Email.Create(_email),
        birthDate: _birthDate,
        role: _role,
        passwordHash: _passwordHash,
        customerId: _customerId);

    if (!_mustChangePassword)
    {
        user.ChangePassword(_passwordHash);
    }

    return user;
}
```

Add `using GarageFlow.Domain.Customers.ValueObjects;`.

- [ ] **Step 4: Run the focused tests and verify pass**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Users.UserDomainTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Domain/Users Tests/Shared/Users Tests/Unit/Users
git commit -m "feat: add customer user domain link"
```

### Task 2: Customer Portal Persistence, Claims, and Policies

**Files:**
- Modify: `Infrastructure/Users/Configurations/UserEntityConfiguration.cs`
- Modify: `Domain/Users/Repositories/IUserRepository.cs`
- Modify: `Infrastructure/Users/Repositories/UserRepository.cs`
- Modify: `Infrastructure/Auth/JwtTokenService.cs`
- Modify: `Api/Security/SecurityClaimTypes.cs`
- Modify: `Api/Security/SecurityRoles.cs`
- Modify: `Api/Security/SecurityPolicies.cs`
- Modify: `Api/Security/AuthenticationExtensions.cs`
- Modify: `Application/Users/CreateUser/CreateUserHandler.cs`
- Test: `Tests/Unit/Users/UserHandlersTests.cs`
- Test: `Tests/Unit/Architecture/DependencyRulesTests.cs`

- [ ] **Step 1: Write failing user handler tests**

Add a test to `Tests/Unit/Users/UserHandlersTests.cs` asserting `CreateUserHandler` rejects `UserRole.Customer`:

```csharp
[Fact]
public async Task CreateUser_ShouldThrowValidationException_WhenRoleIsCustomer()
{
    var userRepositoryMock = CreateUserRepositoryMock();
    var passwordHashServiceMock = CreatePasswordHashServiceMock();
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var handler = new CreateUserHandler(
        userRepositoryMock.Object,
        passwordHashServiceMock.Object,
        unitOfWorkMock.Object);

    await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
        new CreateUserCommand(
            FullName: "Customer User",
            Email: "customer.user@example.com",
            BirthDate: new DateOnly(1990, 1, 1),
            Role: UserRole.Customer),
        CancellationToken.None).AsTask());

    unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

- [ ] **Step 2: Run the focused test and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "CreateUser_ShouldThrowValidationException_WhenRoleIsCustomer"
```

Expected: FAIL because `CreateUserHandler` allows any enum role.

- [ ] **Step 3: Implement repository, mapping, claims, and policy changes**

Add methods to `Domain/Users/Repositories/IUserRepository.cs`:

```csharp
Task<User?> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default);

Task<bool> ExistsByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default);
```

Add `using GarageFlow.Domain.Customers.ValueObjects;`.

Implement in `Infrastructure/Users/Repositories/UserRepository.cs`:

```csharp
public async Task<User?> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default)
{
    return await _dbContext.Users.FirstOrDefaultAsync(
        user => user.CustomerId == customerId,
        cancellationToken);
}

public async Task<bool> ExistsByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default)
{
    return await _dbContext.Users
        .AsNoTracking()
        .AnyAsync(user => user.CustomerId == customerId, cancellationToken);
}
```

Update `Infrastructure/Users/Configurations/UserEntityConfiguration.cs`:

```csharp
builder.Property(user => user.CustomerId)
    .HasConversion(
        customerId => customerId.HasValue ? customerId.Value.Value : (Guid?)null,
        value => value.HasValue ? CustomerId.From(value.Value) : null);

builder.HasIndex(user => user.CustomerId)
    .IsUnique()
    .HasFilter("\"CustomerId\" IS NOT NULL");

builder.HasOne<Customer>()
    .WithMany()
    .HasForeignKey(user => user.CustomerId)
    .OnDelete(DeleteBehavior.Restrict);
```

Add `using GarageFlow.Domain.Customers.Entities;` and `using GarageFlow.Domain.Customers.ValueObjects;`.

Update `Api/Security/SecurityClaimTypes.cs`:

```csharp
public const string CustomerId = "customer_id";
```

Update `Api/Security/SecurityRoles.cs`:

```csharp
public const string Customer = "Customer";
```

Update `Api/Security/SecurityPolicies.cs`:

```csharp
public const string ActiveCustomer = nameof(ActiveCustomer);
public const string ActiveStaff = nameof(ActiveStaff);
```

Update `Infrastructure/Auth/JwtTokenService.cs` to add a claim when `user.CustomerId` has value:

```csharp
if (user.CustomerId.HasValue)
{
    claims.Add(new Claim("customer_id", user.CustomerId.Value.Value.ToString()));
}
```

Update `Api/Security/AuthenticationExtensions.cs`:

```csharp
authorizationBuilder.AddPolicy(
    SecurityPolicies.ActiveCustomer,
    policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(SecurityRoles.Customer)
        .RequireClaim(SecurityClaimTypes.MustChangePassword, "false"));

authorizationBuilder.AddPolicy(
    SecurityPolicies.ActiveStaff,
    policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(SecurityRoles.Admin, SecurityRoles.Attendant)
        .RequireClaim(SecurityClaimTypes.MustChangePassword, "false"));
```

Update `Application/Users/CreateUser/CreateUserHandler.cs` before duplicate email validation:

```csharp
if (request.Role == UserRole.Customer)
{
    throw new ValidationException("Customer users must be created through customer portal activation.");
}
```

- [ ] **Step 4: Run focused tests and build**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "CreateUser_ShouldThrowValidationException_WhenRoleIsCustomer"
dotnet build GarageFlow.slnx
```

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add Domain/Users Infrastructure/Users Infrastructure/Auth Api/Security Application/Users Tests/Unit/Users
git commit -m "feat: wire customer user persistence and policies"
```

### Task 3: Customer Portal Activation Use Case

**Files:**
- Create: `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserCommand.cs`
- Create: `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserHandler.cs`
- Create: `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserResult.cs`
- Create: `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserEndpoint.cs`
- Create: `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserRequest.cs`
- Create: `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserResponse.cs`
- Modify: `Api/Customers/CustomerEndpoints.cs`
- Test: `Tests/Unit/Users/UserHandlersTests.cs`
- Test: `Tests/Integration/Api/Users/UsersApiTests.cs`

- [ ] **Step 1: Write failing handler tests for portal activation**

Add tests to `Tests/Unit/Users/UserHandlersTests.cs`:

```csharp
[Fact]
public async Task ActivateCustomerPortalUser_ShouldCreateCustomerUser_WhenCustomerExists()
{
    var customer = new CustomerBuilder()
        .WithFullName("Portal Customer")
        .WithEmail("portal.customer@example.com")
        .Build();
    var users = new List<User>();
    var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
    var userRepositoryMock = CreateUserRepositoryMock(users);
    var passwordHashServiceMock = CreatePasswordHashServiceMock();
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var handler = new ActivateCustomerPortalUserHandler(
        customerRepositoryMock.Object,
        userRepositoryMock.Object,
        passwordHashServiceMock.Object,
        unitOfWorkMock.Object);

    var result = await handler.Handle(
        new ActivateCustomerPortalUserCommand(customer.Id.Value, new DateOnly(1990, 1, 1)),
        CancellationToken.None);

    Assert.Equal(customer.Id.Value, result.CustomerId);
    Assert.Equal(UserRole.Customer, result.Role);
    Assert.Single(users);
    Assert.Equal(customer.Id, users[0].CustomerId);
    Assert.True(users[0].MustChangePassword);
}
```

Also add tests for missing customer, customer already linked, and email already used.

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "ActivateCustomerPortalUser"
```

Expected: FAIL because the command and handler do not exist.

- [ ] **Step 3: Add Application contracts**

Create `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserCommand.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserCommand(
    Guid CustomerId,
    DateOnly BirthDate) : IRequest<ActivateCustomerPortalUserResult>;
```

Create `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserResult.cs`:

```csharp
using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Application.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserResult(
    Guid Id,
    Guid CustomerId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt);
```

- [ ] **Step 4: Add Application handler**

Create `Application/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserHandler.cs`:

```csharp
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Repositories;
using Mediator;

namespace GarageFlow.Application.Customers.ActivateCustomerPortalUser;

public sealed class ActivateCustomerPortalUserHandler(
    ICustomerRepository customerRepository,
    IUserRepository userRepository,
    IPasswordHashService passwordHashService,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateCustomerPortalUserCommand, ActivateCustomerPortalUserResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<ActivateCustomerPortalUserResult> Handle(
        ActivateCustomerPortalUserCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.CustomerId);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.CustomerId}' was not found.");
        }

        if (await _userRepository.ExistsByCustomerIdAsync(customerId, cancellationToken))
        {
            throw new BusinessRuleViolationException($"Customer with ID '{request.CustomerId}' already has a portal user.");
        }

        var normalizedEmail = Email.Create(customer.Email.Value.ToLowerInvariant());
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new BusinessRuleViolationException($"A user with email '{normalizedEmail.Value}' already exists.");
        }

        var birthDate = UserBirthDate.Create(request.BirthDate);
        var initialPassword = User.GenerateInitialPassword(customer.FullName, birthDate);
        var passwordHash = _passwordHashService.Hash(initialPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = User.Create(
                customer.FullName,
                normalizedEmail,
                birthDate,
                UserRole.Customer,
                passwordHash,
                customerId);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new ActivateCustomerPortalUserResult(
                user.Id.Value,
                customer.Id.Value,
                user.FullName.Value,
                user.Email.Value,
                user.BirthDate.Value,
                user.Role,
                user.MustChangePassword,
                user.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

- [ ] **Step 5: Add API endpoint**

Create request/response records:

```csharp
namespace GarageFlow.Api.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserRequest(DateOnly BirthDate);
```

```csharp
namespace GarageFlow.Api.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserResponse(
    Guid Id,
    Guid CustomerId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    string Role,
    bool MustChangePassword,
    DateTime CreatedAt);
```

Create `Api/Customers/ActivateCustomerPortalUser/ActivateCustomerPortalUserEndpoint.cs`:

```csharp
using GarageFlow.Api.Security;
using GarageFlow.Application.Customers.ActivateCustomerPortalUser;
using Mediator;

namespace GarageFlow.Api.Customers.ActivateCustomerPortalUser;

public static class ActivateCustomerPortalUserEndpoint
{
    public static IEndpointRouteBuilder MapActivateCustomerPortalUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/customers/{customerId:guid}/portal-user", ActivateCustomerPortalUser)
            .RequireAuthorization(SecurityPolicies.ActiveStaff)
            .WithName("ActivateCustomerPortalUser")
            .WithTags("Customers")
            .WithSummary("Activate customer portal access")
            .Produces<ActivateCustomerPortalUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ActivateCustomerPortalUser(
        Guid customerId,
        ActivateCustomerPortalUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ActivateCustomerPortalUserCommand(customerId, request.BirthDate),
            cancellationToken);

        var response = new ActivateCustomerPortalUserResponse(
            result.Id,
            result.CustomerId,
            result.FullName,
            result.Email,
            result.BirthDate,
            result.Role.ToString(),
            result.MustChangePassword,
            result.CreatedAt);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
```

Register it in `Api/Customers/CustomerEndpoints.cs`:

```csharp
protectedCustomerRoutes.MapActivateCustomerPortalUserEndpoint();
```

- [ ] **Step 6: Run tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "ActivateCustomerPortalUser"
dotnet build GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Application/Customers/ActivateCustomerPortalUser Api/Customers Tests/Unit/Users
git commit -m "feat: activate customer portal users"
```

### Task 4: Inventory Stock Reservation Behavior

**Files:**
- Modify: `Domain/InventoryItems/Entities/InventoryItem.cs`
- Test: `Tests/Unit/InventoryItems/InventoryItemTests.cs`

- [ ] **Step 1: Write failing domain tests for stock decrease and restore**

Add tests:

```csharp
[Fact]
public void DecreaseStock_ShouldReduceStock_WhenQuantityIsAvailable()
{
    var item = new InventoryItemBuilder().WithStockQuantity(10).Build();

    item.DecreaseStock(3);

    Assert.Equal(7, item.StockQuantity.Value);
}

[Fact]
public void DecreaseStock_ShouldThrowBusinessRuleViolationException_WhenStockIsInsufficient()
{
    var item = new InventoryItemBuilder().WithStockQuantity(2).Build();

    Assert.Throws<BusinessRuleViolationException>(() => item.DecreaseStock(3));
}

[Fact]
public void IncreaseStock_ShouldRestoreStock_WhenQuantityIsPositive()
{
    var item = new InventoryItemBuilder().WithStockQuantity(2).Build();

    item.IncreaseStock(3);

    Assert.Equal(5, item.StockQuantity.Value);
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.InventoryItems.InventoryItemTests"
```

Expected: FAIL because `DecreaseStock` and `IncreaseStock` do not exist.

- [ ] **Step 3: Implement stock methods**

Add to `Domain/InventoryItems/Entities/InventoryItem.cs`:

```csharp
public void DecreaseStock(int quantity)
{
    if (quantity <= 0)
    {
        throw new ValidationException("Inventory stock decrease quantity must be greater than zero.");
    }

    if (StockQuantity.Value < quantity)
    {
        throw new BusinessRuleViolationException("Inventory item stock is insufficient.");
    }

    SetStockQuantity(InventoryItemStockQuantity.Create(StockQuantity.Value - quantity));
}

public void IncreaseStock(int quantity)
{
    if (quantity <= 0)
    {
        throw new ValidationException("Inventory stock increase quantity must be greater than zero.");
    }

    SetStockQuantity(InventoryItemStockQuantity.Create(StockQuantity.Value + quantity));
}
```

- [ ] **Step 4: Run focused tests and verify pass**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.InventoryItems.InventoryItemTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Domain/InventoryItems Tests/Unit/InventoryItems
git commit -m "feat: add inventory stock reservation behavior"
```

### Task 5: Work Order Domain Model

**Files:**
- Create: all Domain WorkOrder files listed in the File Structure section
- Test: `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- Test: `Tests/Shared/WorkOrders/WorkOrderBuilder.cs`

- [ ] **Step 1: Create failing domain tests**

Create `Tests/Unit/WorkOrders/WorkOrderTests.cs` with tests covering the approved spec names:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderTests
{
    [Fact]
    public void Create_ShouldCreateWorkOrder_WithCreatedStatus()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());

        Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
        Assert.Empty(workOrder.Estimates);
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsEmpty()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());
        var estimate = workOrder.CreateEstimate();

        Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));
    }

    [Fact]
    public void ApproveEstimate_ShouldApprovePendingEstimate_AndMoveWorkOrderToApproved()
    {
        var workOrder = CreatePendingWorkOrderWithService();
        var estimate = workOrder.Estimates.Single();

        workOrder.ApproveEstimate(estimate.Id);

        Assert.Equal(EstimateStatus.Approved, estimate.Status);
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
    }

    [Fact]
    public void RejectEstimate_ShouldRejectPendingEstimate_AndKeepWorkOrderWaitingApproval()
    {
        var workOrder = CreatePendingWorkOrderWithService();
        var estimate = workOrder.Estimates.Single();

        workOrder.RejectEstimate(estimate.Id);

        Assert.Equal(EstimateStatus.Rejected, estimate.Status);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
    }

    [Fact]
    public void Complete_ShouldThrowBusinessRuleViolationException_WhenNoEstimateIsApproved()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());

        Assert.Throws<BusinessRuleViolationException>(() => workOrder.Complete());
    }

    [Fact]
    public void Complete_ShouldCompleteWorkOrder_WhenEstimateIsApproved()
    {
        var workOrder = CreatePendingWorkOrderWithService();
        var estimate = workOrder.Estimates.Single();
        workOrder.ApproveEstimate(estimate.Id);
        workOrder.StartWork();

        workOrder.Complete();

        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    }

    [Fact]
    public void AddInventoryLine_ShouldCalculateEstimateTotal()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());
        var estimate = workOrder.CreateEstimate();

        workOrder.AddInventoryLine(
            estimate.Id,
            InventoryItemId.New(),
            Description.Create("Oil filter"),
            quantity: EstimateItemQuantity.Create(2),
            unitCost: Price.Create(10m),
            unitPrice: Price.Create(25m));
        workOrder.AddServiceLine(
            estimate.Id,
            ServiceId.New(),
            Description.Create("Oil change"),
            Price.Create(100m));

        Assert.Equal(150m, estimate.TotalAmount.Value);
    }

    private static WorkOrder CreatePendingWorkOrderWithService()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(
            estimate.Id,
            ServiceId.New(),
            Description.Create("Oil change"),
            Price.Create(100m));
        workOrder.SubmitEstimate(estimate.Id);
        return workOrder;
    }
}
```

Add these additional tests to the same file:

```csharp
[Fact] public void StartWork_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsWaitingApproval()
[Fact] public void Deliver_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsNotCompleted()
[Fact] public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCompleted()
[Fact] public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsDelivered()
[Fact] public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled()
[Fact] public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsRejected()
[Fact] public void RejectEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsApproved()
[Fact] public void AddServiceLine_ShouldThrowBusinessRuleViolationException_WhenEstimateIsApproved()
[Fact] public void AddServiceLine_ShouldThrowBusinessRuleViolationException_WhenEstimateIsRejected()
[Fact] public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenAnotherEstimateIsApproved()
```

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: FAIL because WorkOrders domain types do not exist.

- [ ] **Step 3: Implement value objects and enums**

Create strongly typed ids following the existing `CustomerId` pattern:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct WorkOrderId : IStronglyTypedId
{
    public Guid Value { get; }

    private WorkOrderId(Guid value)
    {
        Value = value;
    }

    public static WorkOrderId New() => new(Guid.NewGuid());

    public static WorkOrderId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Work order identifier cannot be empty.");
        }

        return new WorkOrderId(value);
    }

    public override string ToString() => Value.ToString();
}
```

Create the same shape for `EstimateId`, `EstimateInventoryLineId`, and `EstimateServiceLineId` with messages naming the correct identifier.

Create `EstimateItemQuantity`:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using System.Globalization;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct EstimateItemQuantity
{
    public int Value { get; }

    private EstimateItemQuantity(int value)
    {
        Value = value;
    }

    public static EstimateItemQuantity Create(int value)
    {
        if (value <= 0)
        {
            throw new ValidationException("Estimate item quantity must be greater than zero.");
        }

        return new EstimateItemQuantity(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator int(EstimateItemQuantity quantity) => quantity.Value;
}
```

Create `WorkOrderStatus` and `EstimateStatus` exactly as in the design.

- [ ] **Step 4: Implement entities and events**

Implement `WorkOrder`, `Estimate`, `EstimateInventoryLine`, and `EstimateServiceLine` with these public methods:

```csharp
public static WorkOrder Create(CustomerId customerId, VehicleId vehicleId);
public Estimate CreateEstimate();
public void AddInventoryLine(EstimateId estimateId, InventoryItemId inventoryItemId, Description description, EstimateItemQuantity quantity, Price unitCost, Price unitPrice);
public void AddServiceLine(EstimateId estimateId, ServiceId serviceId, Description description, Price unitPrice);
public void SubmitEstimate(EstimateId estimateId);
public void ApproveEstimate(EstimateId estimateId);
public Estimate RejectEstimate(EstimateId estimateId);
public void StartDiagnosis();
public void StartWork();
public void Complete();
public void Deliver();
public void Cancel();
```

Make `RejectEstimate` return the rejected estimate so handlers can restore stock from its inventory lines after the aggregate transition succeeds.

Use these internal helper concepts:

```csharp
private bool IsFinal => Status is WorkOrderStatus.Completed or WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled;

private void EnsureCanChangeContent()
{
    if (IsFinal)
    {
        throw new BusinessRuleViolationException("Finalized work orders cannot be changed.");
    }
}

private Estimate GetEstimateOrThrow(EstimateId estimateId)
{
    return _estimates.FirstOrDefault(estimate => estimate.Id == estimateId)
        ?? throw new NotFoundException($"Estimate with ID '{estimateId.Value}' was not found.");
}
```

Ensure status changes call a single transition method that rejects non-explicit transitions and updates `UpdatedAt`.

Implement line totals as calculated properties:

```csharp
public Price TotalPrice => Price.Create(UnitPrice.Value * Quantity.Value);
```

For estimate total, sum inventory and service line totals:

```csharp
public Price TotalAmount => Price.Create(
    InventoryLines.Sum(line => line.TotalPrice.Value) +
    ServiceLines.Sum(line => line.TotalPrice.Value));
```

- [ ] **Step 5: Run focused tests and verify pass**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Domain/WorkOrders Tests/Unit/WorkOrders Tests/Shared/WorkOrders
git commit -m "feat: add work order domain model"
```

### Task 6: Work Order Repository Contract and Infrastructure Mapping

**Files:**
- Create: `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`
- Create: read model files under `Domain/WorkOrders/Repositories`
- Create: `Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs`
- Create: `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- Modify: `Infrastructure/DataAccess/GarageFlowDbContext.cs`
- Modify: `Infrastructure/DataAccess/DependencyInjection.cs`
- Test: `Tests/Unit/Vehicles/VehicleRepositoryContractsCompilationTests.cs` or new `Tests/Unit/WorkOrders/WorkOrderRepositoryContractsCompilationTests.cs`

- [ ] **Step 1: Write compilation contract test**

Create `Tests/Unit/WorkOrders/WorkOrderRepositoryContractsCompilationTests.cs`:

```csharp
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderRepositoryContractsCompilationTests
{
    [Fact]
    public void IWorkOrderRepository_ShouldExposeRequiredReadMethods()
    {
        var methods = typeof(IWorkOrderRepository).GetMethods().Select(method => method.Name).ToArray();

        Assert.Contains("GetByIdAsync", methods);
        Assert.Contains("GetDetailsByIdAsync", methods);
        Assert.Contains("GetCustomerDetailsByIdAsync", methods);
        Assert.Contains("ListDetailsAsync", methods);
        Assert.Contains("ListCustomerDetailsAsync", methods);
        Assert.Contains("AddAsync", methods);
    }
}
```

- [ ] **Step 2: Run focused test and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "WorkOrderRepositoryContractsCompilationTests"
```

Expected: FAIL because repository contracts do not exist.

- [ ] **Step 3: Add repository contracts and read models**

Create `IWorkOrderRepository`:

```csharp
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Repositories;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default);
    Task<WorkOrderDetailsReadModel?> GetDetailsByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default);
    Task<WorkOrderDetailsReadModel?> GetCustomerDetailsByIdAsync(WorkOrderId id, CustomerId customerId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(int page, int pageSize, CustomerId? customerId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListCustomerDetailsAsync(int page, int pageSize, CustomerId customerId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
```

Create read models as sealed records with primitive values for easy projection:

```csharp
public sealed record WorkOrderDetailsReadModel(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderEstimateReadModel> Estimates);
```

Add estimate, inventory line, and service line records with both cost and price in inventory lines. Customer handlers will map away costs.

- [ ] **Step 4: Implement EF mapping and repository**

Add `DbSet<WorkOrder>` and apply configuration in `GarageFlowDbContext`.

Register repository in `DependencyInjection`:

```csharp
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
```

Implement `WorkOrderEntityConfiguration` using `HasMany` relationships with private backing fields. Map conversions for all VOs and enums as strings. Use `HasPrecision(18, 2)` for prices and `Description.MaxLength` for descriptions.

Implement `WorkOrderRepository.GetByIdAsync` with includes:

```csharp
return await _dbContext.WorkOrders
    .Include(workOrder => EF.Property<IReadOnlyCollection<Estimate>>(workOrder, "Estimates"))
    .FirstOrDefaultAsync(workOrder => workOrder.Id == id, cancellationToken);
```

Expose public read-only collection properties backed by private lists and configure field access. Keep public mutation through methods only. Use includes against the public read-only properties:

```csharp
return await _dbContext.WorkOrders
    .Include(workOrder => workOrder.Estimates)
        .ThenInclude(estimate => estimate.InventoryLines)
    .Include(workOrder => workOrder.Estimates)
        .ThenInclude(estimate => estimate.ServiceLines)
    .FirstOrDefaultAsync(workOrder => workOrder.Id == id, cancellationToken);
```

Implement details queries by loading aggregates with includes and mapping in memory. `ListDetailsAsync` and `ListCustomerDetailsAsync` return paginated `WorkOrderDetailsReadModel` values with estimate and line collections included for each page item.

- [ ] **Step 5: Run build and repository contract test**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "WorkOrderRepositoryContractsCompilationTests"
dotnet build GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Domain/WorkOrders Infrastructure/WorkOrders Infrastructure/DataAccess Tests/Unit/WorkOrders
git commit -m "feat: persist work order aggregate"
```

### Task 7: Staff Work Order Application Handlers

**Files:**
- Create: staff Application WorkOrder files listed in File Structure
- Test: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

- [ ] **Step 1: Write failing handler tests for core staff scenarios**

Create `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs` with tests for:

```csharp
[Fact] public async Task CreateWorkOrder_ShouldCreateCreatedWorkOrder_WhenVehicleBelongsToCustomer()
[Fact] public async Task CreateWorkOrder_ShouldThrowBusinessRuleViolationException_WhenVehicleDoesNotBelongToCustomer()
[Fact] public async Task AddEstimateInventoryItem_ShouldDecreaseStock_WhenStockIsSufficient()
[Fact] public async Task AddEstimateInventoryItem_ShouldThrowBusinessRuleViolationException_WhenStockIsInsufficient()
[Fact] public async Task AddEstimateInventoryItem_ShouldThrowValidationException_WhenQuantityIsInvalid()
[Fact] public async Task AddEstimateService_ShouldAddService_WhenServiceExists()
[Fact] public async Task SubmitEstimate_ShouldSubmitDraftEstimate_WhenEstimateHasLines()
[Fact] public async Task CompleteWorkOrder_ShouldThrowBusinessRuleViolationException_WhenNoEstimateIsApproved()
```

Add local helper methods named `CreateWorkOrderRepositoryMock`, `CreateCustomerRepositoryMock`, `CreateVehicleRepositoryMock`, `CreateInventoryItemRepositoryMock`, `CreateServiceRepositoryMock`, `CreateUserRepositoryMock`, and `CreateUnitOfWorkMock` in `WorkOrderHandlersTests`.

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderHandlersTests"
```

Expected: FAIL because handlers do not exist.

- [ ] **Step 3: Implement command/result records**

Use these command signatures:

```csharp
public sealed record CreateWorkOrderCommand(Guid CustomerId, Guid VehicleId) : IRequest<CreateWorkOrderResult>;
public sealed record CreateEstimateCommand(Guid WorkOrderId) : IRequest<CreateEstimateResult>;
public sealed record AddEstimateInventoryItemCommand(Guid WorkOrderId, Guid EstimateId, Guid InventoryItemId, int Quantity) : IRequest<AddEstimateInventoryItemResult>;
public sealed record AddEstimateServiceCommand(Guid WorkOrderId, Guid EstimateId, Guid ServiceId) : IRequest<AddEstimateServiceResult>;
public sealed record SubmitEstimateCommand(Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
public sealed record StartDiagnosisCommand(Guid WorkOrderId) : IRequest<Unit>;
public sealed record StartWorkCommand(Guid WorkOrderId) : IRequest<Unit>;
public sealed record CompleteWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
public sealed record DeliverWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
public sealed record CancelWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
```

Use these result record shapes:

```csharp
public sealed record CreateWorkOrderResult(Guid Id, Guid CustomerId, Guid VehicleId, string Status, DateTime CreatedAt);
public sealed record CreateEstimateResult(Guid Id, Guid WorkOrderId, string Status, decimal TotalAmount, DateTime CreatedAt);
public sealed record AddEstimateInventoryItemResult(Guid EstimateId, Guid InventoryItemId, string Description, int Quantity, decimal UnitCost, decimal UnitPrice, decimal TotalPrice);
public sealed record AddEstimateServiceResult(Guid EstimateId, Guid ServiceId, string Description, decimal UnitPrice, decimal TotalPrice);
```

- [ ] **Step 4: Implement handlers**

`CreateWorkOrderHandler` dependencies:

```csharp
IWorkOrderRepository workOrderRepository,
ICustomerRepository customerRepository,
IVehicleRepository vehicleRepository,
IUnitOfWork unitOfWork
```

Rules:

- load customer or throw `NotFoundException`;
- load vehicle or throw `NotFoundException`;
- compare `vehicle.CustomerId` to `customer.Id`; throw `BusinessRuleViolationException` if different;
- create aggregate and save in one transaction.

`AddEstimateInventoryItemHandler` dependencies:

```csharp
IWorkOrderRepository workOrderRepository,
IInventoryItemRepository inventoryItemRepository,
IUnitOfWork unitOfWork
```

Rules:

- validate `EstimateItemQuantity.Create(request.Quantity)`;
- load work order and inventory item;
- call `inventoryItem.DecreaseStock(quantity.Value)`;
- call `workOrder.AddInventoryLine(...)` with snapshot description, cost, and price;
- commit both mutations in one transaction.

Task 8 restores stock in `RejectMyEstimateHandler`; staff handlers do not reject estimates.

Status handlers should load work order, call the matching aggregate method, and commit.

- [ ] **Step 5: Run focused tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderHandlersTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/WorkOrders Tests/Unit/WorkOrders
git commit -m "feat: add staff work order handlers"
```

### Task 8: Customer Work Order Application Handlers

**Files:**
- Create: customer Application WorkOrder files listed in File Structure
- Test: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

- [ ] **Step 1: Write failing customer handler tests**

Add tests:

```csharp
[Fact] public async Task ApproveMyEstimate_ShouldApproveEstimate_WhenWorkOrderBelongsToCustomer()
[Fact] public async Task ApproveMyEstimate_ShouldThrowNotFoundException_WhenWorkOrderBelongsToAnotherCustomer()
[Fact] public async Task RejectMyEstimate_ShouldRejectEstimate_AndRestoreStock_WhenWorkOrderBelongsToCustomer()
[Fact] public async Task RejectMyEstimate_ShouldThrowNotFoundException_WhenWorkOrderBelongsToAnotherCustomer()
[Fact] public async Task GetMyWorkOrderById_ShouldNotExposeInventoryCost()
```

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "ApproveMyEstimate|RejectMyEstimate|GetMyWorkOrderById"
```

Expected: FAIL because customer handlers do not exist.

- [ ] **Step 3: Implement customer command/query records**

Use these signatures:

```csharp
public sealed record ListMyWorkOrdersQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<ListMyWorkOrdersResult>;
public sealed record GetMyWorkOrderByIdQuery(Guid UserId, Guid WorkOrderId) : IRequest<CustomerWorkOrderDetailsDto>;
public sealed record ApproveMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
public sealed record RejectMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
```

- [ ] **Step 4: Implement customer ownership helper in each handler**

Use a private method pattern in handlers:

```csharp
private static CustomerId GetRequiredCustomerId(User user)
{
    if (user.Role != UserRole.Customer || user.CustomerId is null)
    {
        throw new UnauthorizedAccessException("Authenticated user is not a customer user.");
    }

    return user.CustomerId.Value;
}
```

For cross-customer access, throw `NotFoundException` with a work-order not-found message.

`RejectMyEstimateHandler` must:

- load user and customer id;
- load work order;
- verify ownership;
- call `workOrder.RejectEstimate(estimateId)`;
- for each inventory line in the rejected estimate, load inventory item and call `IncreaseStock(line.Quantity.Value)`;
- commit all changes in one transaction.

- [ ] **Step 5: Run focused tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "ApproveMyEstimate|RejectMyEstimate|GetMyWorkOrderById|ListMyWorkOrders"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Application/WorkOrders Tests/Unit/WorkOrders
git commit -m "feat: add customer work order handlers"
```

### Task 9: Work Order API Endpoints

**Files:**
- Create: Api WorkOrder files listed in File Structure
- Modify: `Api/Program.cs`
- Test: `Tests/Unit/Architecture/DependencyRulesTests.cs`

- [ ] **Step 1: Add endpoint registration files**

Create `Api/WorkOrders/WorkOrderEndpoints.cs`:

```csharp
using GarageFlow.Api.Security;
using GarageFlow.Api.WorkOrders.AddEstimateInventoryItem;
using GarageFlow.Api.WorkOrders.AddEstimateService;
using GarageFlow.Api.WorkOrders.ApproveMyEstimate;
using GarageFlow.Api.WorkOrders.CancelWorkOrder;
using GarageFlow.Api.WorkOrders.CompleteWorkOrder;
using GarageFlow.Api.WorkOrders.CreateEstimate;
using GarageFlow.Api.WorkOrders.CreateWorkOrder;
using GarageFlow.Api.WorkOrders.DeliverWorkOrder;
using GarageFlow.Api.WorkOrders.GetMyWorkOrderById;
using GarageFlow.Api.WorkOrders.GetWorkOrderById;
using GarageFlow.Api.WorkOrders.ListMyWorkOrders;
using GarageFlow.Api.WorkOrders.ListWorkOrders;
using GarageFlow.Api.WorkOrders.RejectMyEstimate;
using GarageFlow.Api.WorkOrders.StartDiagnosis;
using GarageFlow.Api.WorkOrders.StartWork;
using GarageFlow.Api.WorkOrders.SubmitEstimate;

namespace GarageFlow.Api.WorkOrders;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var staffRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

        staffRoutes.MapCreateWorkOrderEndpoint();
        staffRoutes.MapListWorkOrdersEndpoint();
        staffRoutes.MapGetWorkOrderByIdEndpoint();
        staffRoutes.MapCreateEstimateEndpoint();
        staffRoutes.MapAddEstimateInventoryItemEndpoint();
        staffRoutes.MapAddEstimateServiceEndpoint();
        staffRoutes.MapSubmitEstimateEndpoint();
        staffRoutes.MapStartDiagnosisEndpoint();
        staffRoutes.MapStartWorkEndpoint();
        staffRoutes.MapCompleteWorkOrderEndpoint();
        staffRoutes.MapDeliverWorkOrderEndpoint();
        staffRoutes.MapCancelWorkOrderEndpoint();

        var customerRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveCustomer);

        customerRoutes.MapListMyWorkOrdersEndpoint();
        customerRoutes.MapGetMyWorkOrderByIdEndpoint();
        customerRoutes.MapApproveMyEstimateEndpoint();
        customerRoutes.MapRejectMyEstimateEndpoint();

        return app;
    }
}
```

Keep namespaces mirrored to folders, for example `Api/WorkOrders/StartDiagnosis/StartDiagnosisEndpoint.cs` uses `namespace GarageFlow.Api.WorkOrders.StartDiagnosis;`.

- [ ] **Step 2: Implement endpoint files**

For each endpoint:

- map request route exactly from the design;
- use only Application command/query types;
- extract `UserId` with `httpContext.User.GetRequiredUserId()` for `/me` endpoints;
- define `.WithName`, `.WithTags`, `.WithSummary`, `.Produces`, and `.ProducesProblem`.

Example customer approval endpoint:

```csharp
public static IEndpointRouteBuilder MapApproveMyEstimateEndpoint(this IEndpointRouteBuilder app)
{
    app.MapPost("/me/work-orders/{workOrderId:guid}/estimates/{estimateId:guid}/approve", ApproveMyEstimate)
        .WithName("ApproveMyEstimate")
        .WithTags("Work Orders")
        .WithSummary("Approve one of the authenticated customer's pending estimates")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

    return app;
}

private static async Task<IResult> ApproveMyEstimate(
    HttpContext httpContext,
    Guid workOrderId,
    Guid estimateId,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var userId = httpContext.User.GetRequiredUserId();
    await mediator.Send(new ApproveMyEstimateCommand(userId, workOrderId, estimateId), cancellationToken);
    return Results.NoContent();
}
```

Register in `Api/Program.cs`:

```csharp
app.MapWorkOrderEndpoints();
```

- [ ] **Step 3: Run architecture tests and build**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture.DependencyRulesTests"
dotnet build GarageFlow.slnx
```

Expected: PASS. All new Api request and response records use primitive or Application-owned types; status and role fields are `string` in Api responses to satisfy API boundary rules.

- [ ] **Step 4: Commit**

```bash
git add Api/WorkOrders Api/Program.cs Tests/Unit/Architecture
git commit -m "feat: expose work order endpoints"
```

### Task 10: EF Migration

**Files:**
- Create by command: EF-generated `WorkOrdersModule` migration files under `Infrastructure/DataAccess/Migrations`
- Modify by command: `Infrastructure/DataAccess/Migrations/GarageFlowDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

Run:

```bash
dotnet ef migrations add WorkOrdersModule --project Infrastructure --startup-project Api --context GarageFlowDbContext --output-dir DataAccess/Migrations
```

Expected: EF creates a migration named `WorkOrdersModule` under `Infrastructure/DataAccess/Migrations`.

- [ ] **Step 2: Inspect migration**

Verify the migration contains:

- `Users.CustomerId` nullable column;
- index on `Users.CustomerId`;
- `WorkOrders`;
- `WorkOrderEstimates`;
- `WorkOrderEstimateInventoryLines`;
- `WorkOrderEstimateServiceLines`;
- foreign keys with restrictive delete where cross-aggregate references exist;
- decimal columns with `numeric(18,2)`.

- [ ] **Step 3: Build**

Run:

```bash
dotnet build GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Infrastructure/DataAccess/Migrations
git commit -m "feat: add work order migration"
```

### Task 11: Shared Builders and Integration Contracts

**Files:**
- Create: shared WorkOrder request builders and integration contracts listed in File Structure
- Modify: `Tests/Shared/Users/UserBuilder.cs`
- Test: compile through integration test project

- [ ] **Step 1: Add shared request records**

Create:

```csharp
namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record CreateWorkOrderRequest(Guid CustomerId, Guid VehicleId);
public sealed record AddEstimateInventoryItemRequest(Guid InventoryItemId, int Quantity);
public sealed record AddEstimateServiceRequest(Guid ServiceId);
```

- [ ] **Step 2: Add integration response contracts**

Create customer response contract without cost fields:

```csharp
namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record CustomerWorkOrderInventoryLineResponse(
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);
```

Create internal response contract with `UnitCost`:

```csharp
namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record WorkOrderInventoryLineResponse(
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
```

Add estimate and work-order contracts with status strings, totals, timestamps, and line collections.

- [ ] **Step 3: Build test projects**

Run:

```bash
dotnet build Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet build Tests/Unit/GarageFlow.Tests.Unit.csproj
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Tests/Shared/WorkOrders Tests/Integration/Api/WorkOrders/Contracts Tests/Shared/Users
git commit -m "test: add work order test contracts"
```

### Task 12: Work Order Integration Tests

**Files:**
- Create: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`
- Modify helper methods only inside this test file unless reuse becomes obvious

- [ ] **Step 1: Write integration tests**

Create tests covering:

```csharp
[Fact] public async Task Staff_ShouldCreateWorkOrder_WithCreatedStatus()
[Fact] public async Task Staff_ShouldReceive409_WhenVehicleBelongsToAnotherCustomer()
[Fact] public async Task Staff_ShouldAddInventoryItem_AndDecreaseStock()
[Fact] public async Task Staff_ShouldReceive409_WhenInventoryStockIsInsufficient()
[Fact] public async Task Staff_ShouldReceive409_WhenSubmittingEmptyEstimate()
[Fact] public async Task Customer_ShouldListOnlyOwnWorkOrders()
[Fact] public async Task Customer_ShouldReceive404_WhenAccessingAnotherCustomersWorkOrder()
[Fact] public async Task Customer_ShouldApproveOwnPendingEstimate()
[Fact] public async Task Customer_ShouldReceive404_WhenApprovingAnotherCustomersEstimate()
[Fact] public async Task Customer_ShouldRejectOwnPendingEstimate_AndRestoreStock()
[Fact] public async Task CustomerWorkOrderResponse_ShouldNotExposeInventoryCost()
```

Use local helper methods to:

- create active attendant client;
- create customer;
- create customer portal user;
- activate customer password;
- create vehicle dependencies;
- create inventory item;
- create service;
- create work order and estimate.

For cost-leak test, read raw JSON string and assert:

```csharp
Assert.DoesNotContain("unitCost", body, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("totalCost", body, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("\"cost\"", body, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run integration tests and verify failures or pass according to implementation completeness**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Integration.Api.WorkOrders.WorkOrdersApiTests"
```

Expected after prior tasks: PASS. If failures occur, inspect response bodies and fix endpoint mapping or handler validation.

- [ ] **Step 3: Commit**

```bash
git add Tests/Integration/Api/WorkOrders
git commit -m "test: cover work order api workflows"
```

### Task 13: Architecture and Module Convention Updates

**Files:**
- Modify: `Tests/Unit/Architecture/ModuleConventionTests.cs`
- Test: `Tests/Unit/Architecture`

- [ ] **Step 1: Add WorkOrders to module convention list**

Update:

```csharp
private static readonly string[] BusinessModules =
[
    "Customers",
    "Services",
    "Vehicles",
    "InventoryItems",
    "Users",
    "WorkOrders"
];
```

- [ ] **Step 2: Run architecture tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Tests/Unit/Architecture
git commit -m "test: enforce work order module conventions"
```

### Task 14: Full Verification and Cleanup

**Files:**
- Inspect all changed files
- No new source files should contain incomplete comments, dead code, or unused helpers

- [ ] **Step 1: Format**

Run:

```bash
dotnet format GarageFlow.slnx
```

Expected: command exits successfully. If formatting changes files, include them in the final commit.

- [ ] **Step 2: Build**

Run:

```bash
dotnet build GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 3: Unit tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
```

Expected: PASS.

- [ ] **Step 4: Integration tests**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
```

Expected: PASS.

- [ ] **Step 5: Full solution tests**

Run:

```bash
dotnet test GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 6: Final diff review**

Run:

```bash
git status --short
git diff --stat HEAD
```

Expected: only intentional files changed since the last task commit. If `dotnet format` changed files, commit them:

```bash
git add .
git commit -m "style: format work order module"
```

## Self-Review Notes

- Every acceptance criterion from `docs/superpowers/specs/2026-04-28-work-orders-design.md` maps to at least one task above.
- Customer portal activation is implemented before customer work-order commands so authorization has a concrete `CustomerId` source.
- Inventory decrease happens in `AddEstimateInventoryItemHandler`; restoration happens in `RejectMyEstimateHandler`.
- Customer response contracts are separate from internal response contracts to prevent internal cost leakage.
- Staff approval and rejection override are not included.
