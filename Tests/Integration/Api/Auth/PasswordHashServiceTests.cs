using GarageFlow.Adapters.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace GarageFlow.Tests.Integration.Api.Auth;

public sealed class PasswordHashServiceTests
{
    [Fact]
    public void MissingUserHash_PerformsHashVerificationButCannotAuthenticate()
    {
        var verifier = new Mock<IPasswordHasher<object>>();
        verifier.Setup(hasher => hasher.VerifyHashedPassword(
            It.IsAny<object>(), It.IsAny<string>(), "candidate"))
            .Returns(PasswordVerificationResult.Success);
        var service = new PasswordHashService(verifier.Object);

        Assert.False(service.Verify("candidate", null));

        verifier.Verify(hasher => hasher.VerifyHashedPassword(
            It.IsAny<object>(), It.Is<string>(hash => !string.IsNullOrWhiteSpace(hash)), "candidate"), Times.Once);
    }

    [Fact]
    public void ExistingHash_UsesTheStoredHashAndAcceptsRehashNeeded()
    {
        var verifier = new Mock<IPasswordHasher<object>>();
        verifier.Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<object>(), "stored-hash", "candidate"))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);
        var service = new PasswordHashService(verifier.Object);

        Assert.True(service.Verify("candidate", "stored-hash"));

        verifier.VerifyAll();
    }
}
