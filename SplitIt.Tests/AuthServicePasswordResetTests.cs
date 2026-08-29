using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class AuthServicePasswordResetTests
{
    private static AuthService CreateService(AppDbContext ctx)
        => new(ctx, new PasswordHasher<User>());

    [Fact]
    public async Task GenerateResetToken_Returns6DigitCode()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var code = await svc.GenerateResetTokenAsync("alice@test.com");

        Assert.NotNull(code);
        Assert.Equal(6, code!.Length);
        Assert.Matches(@"^\d{6}$", code);
    }

    [Fact]
    public async Task GenerateResetToken_ReturnsNull_WhenEmailNotFound()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);

        var code = await svc.GenerateResetTokenAsync("nobody@test.com");

        Assert.Null(code);
    }

    [Fact]
    public async Task GenerateResetToken_InvalidatesPreviousTokens()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var code1 = await svc.GenerateResetTokenAsync("alice@test.com");
        var code2 = await svc.GenerateResetTokenAsync("alice@test.com");

        Assert.NotNull(code1);
        Assert.NotNull(code2);
        Assert.NotEqual(code1, code2);

        var tokens = await ctx.PasswordResetTokens.Where(t => t.UserId == 1).ToListAsync();
        Assert.Single(tokens);
        Assert.False(tokens[0].Used);
        Assert.Equal(code2, tokens[0].Token);
    }

    [Fact]
    public async Task ResetPassword_WithValidCode_Succeeds()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var code = await svc.GenerateResetTokenAsync("alice@test.com");

        var result = await svc.ResetPasswordAsync(code!, "NewPassword456!");

        Assert.True(result);
        Assert.True(await svc.ValidateUser("alice@test.com", "NewPassword456!"));
        Assert.False(await svc.ValidateUser("alice@test.com", "StrongPass123!"));
    }

    [Fact]
    public async Task ResetPassword_WithInvalidCode_ReturnsFalse()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var result = await svc.ResetPasswordAsync("000000", "NewPassword456!");

        Assert.False(result);
    }

    [Fact]
    public async Task ResetPassword_WithUsedCode_ReturnsFalse()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var code = await svc.GenerateResetTokenAsync("alice@test.com");
        await svc.ResetPasswordAsync(code!, "NewPassword456!");

        var result = await svc.ResetPasswordAsync(code!, "AnotherPassword789!");

        Assert.False(result);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredCode_ReturnsFalse()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = CreateService(ctx);
        await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");

        var code = await svc.GenerateResetTokenAsync("alice@test.com");
        var token = await ctx.PasswordResetTokens.FirstAsync(t => t.Token == code);
        token.ExpiresAt = DateTime.UtcNow.AddHours(-1);
        await ctx.SaveChangesAsync();

        var result = await svc.ResetPasswordAsync(code!, "NewPassword456!");

        Assert.False(result);
    }
}
