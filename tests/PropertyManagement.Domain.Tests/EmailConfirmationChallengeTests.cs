using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Domain.Tests;

public sealed class EmailConfirmationChallengeTests
{
    [Fact]
    public void Create_ShouldThrow_WhenExpirationIsNotAfterCreation()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var createdAt = new DateTime(
            2026,
            10,
            12,
            18,
            0,
            0,
            DateTimeKind.Utc);

        var expiresAt = createdAt;

        // Act
        var action = () =>
            EmailConfirmationChallenge.Create(
                userId: userId,
                protectedCode: "protected-code",
                createdAt: createdAt,
                expiresAt: expiresAt);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void RegisterFailedAttempt_ShouldIncreaseFailedAttempts()
    {
        // Arrange
        var createdAt = new DateTime(
            2026,
            10,
            12,
            18,
            0,
            0,
            DateTimeKind.Utc);

        var challenge =
            EmailConfirmationChallenge.Create(
                userId: Guid.NewGuid(),
                protectedCode: "protected-code",
                createdAt: createdAt,
                expiresAt: createdAt.AddMinutes(10));

        var now = createdAt.AddMinutes(1);

        // Act
        challenge.RegisterFailedAttempt(now);

        // Assert
        Assert.Equal(1, challenge.FailedAttempts);
    }
    [Fact]
    public void Revoke_ShouldMakeChallengeInactive()
    {
        // Arrange
        var createdAt = new DateTime(
            2026,
            10,
            12,
            19,
            0,
            0,
            DateTimeKind.Utc);

        var challenge =
            EmailConfirmationChallenge.Create(
                userId: Guid.NewGuid(),
                protectedCode: "protected-code",
                createdAt: createdAt,
                expiresAt: createdAt.AddMinutes(10));

        var revokedAt = createdAt.AddMinutes(1);

        // Act
        challenge.Revoke(
            revokedAt: revokedAt,
            reason: "NewCodeIssued");

        // Assert
        Assert.True(challenge.IsRevoked);
        Assert.False(
            challenge.IsActive(
                revokedAt.AddSeconds(1)));
        Assert.Equal(
            "NewCodeIssued",
            challenge.RevocationReason);
    }
    [Fact]
    public void Challenge_ShouldBecomeInactive_AfterMaximumFailedAttempts()
    {
        // Arrange
        var createdAt = new DateTime(
            2026,
            10,
            12,
            19,
            5,
            0,
            DateTimeKind.Utc);

        var challenge =
            EmailConfirmationChallenge.Create(
                userId: Guid.NewGuid(),
                protectedCode: "protected-code",
                createdAt: createdAt,
                expiresAt: createdAt.AddMinutes(10));

        // Act
        for (var attempt = 1;
             attempt <= EmailConfirmationChallenge.MaxFailedAttempts;
             attempt++)
        {
            challenge.RegisterFailedAttempt(
                createdAt.AddMinutes(attempt));
        }

        // Assert
        Assert.Equal(
            EmailConfirmationChallenge.MaxFailedAttempts,
            challenge.FailedAttempts);

        Assert.True(challenge.HasExceededAttempts());

        Assert.False(
            challenge.IsActive(
                createdAt.AddMinutes(6)));
    }
    [Fact]
    public void MarkAsUsed_ShouldMarkChallengeAsUsedAndInactive()
    {
        // Arrange
        var createdAt = new DateTime(
            2026,
            10,
            12,
            19,
            10,
            0,
            DateTimeKind.Utc);

        var challenge =
            EmailConfirmationChallenge.Create(
                userId: Guid.NewGuid(),
                protectedCode: "protected-code",
                createdAt: createdAt,
                expiresAt: createdAt.AddMinutes(10));

        var usedAt = createdAt.AddMinutes(1);

        // Act
        challenge.MarkAsUsed(usedAt);

        // Assert
        Assert.True(challenge.IsUsed);
        Assert.Equal(usedAt, challenge.UsedAt);
        Assert.False(challenge.IsActive(usedAt));
    }

}
