using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace SplitIt.Tests;

public class AuthServicePasswordHashingTests
{
    private static string LegacyHash(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    [Fact]
    public async Task RegisterUser_HashesWithPasswordHasher_NotLegacy()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var hasher = new PasswordHasher<User>();
        var svc = new AuthService(ctx, hasher);

        var ok = await svc.RegisterUser("Alice", "alice@test.com", "StrongPass123!");
        Assert.True(ok);
        var user = await svc.GetUserByEmail("alice@test.com");
        Assert.NotNull(user);
        // Identity V3 hash starts with AQAAAA
        Assert.StartsWith("AQAAAA", user!.PasswordHash);
        Assert.NotEqual(LegacyHash("StrongPass123!"), user.PasswordHash);
    }

    [Fact]
    public async Task ValidateUser_CorrectPassword_Succeeds()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = new AuthService(ctx, new PasswordHasher<User>());
        await svc.RegisterUser("Bob", "bob@test.com", "MySecret123!");
        Assert.True(await svc.ValidateUser("bob@test.com", "MySecret123!"));
        Assert.False(await svc.ValidateUser("bob@test.com", "Wrong"));
    }

    [Fact]
    public async Task ValidateUser_LegacyHash_MigratesToNewHash()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        // Insert legacy user manually
        var legacyPassword = "legacyPass123";
        var legacyHash = LegacyHash(legacyPassword);
        var user = new User { Name = "Legacy", Email = "legacy@test.com", PasswordHash = legacyHash, RoleId = 3 };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var svc = new AuthService(ctx, new PasswordHasher<User>());
        Assert.True(await svc.ValidateUser("legacy@test.com", legacyPassword));

        var updated = await ctx.Users.FirstAsync(u => u.Email == "legacy@test.com");
        Assert.StartsWith("AQAAAA", updated.PasswordHash);
        // Now validate again with new hash
        Assert.True(await svc.ValidateUser("legacy@test.com", legacyPassword));
    }

    [Fact]
    public async Task ValidateUser_LegacyHash_WrongPassword_Fails()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Users.Add(new User { Name = "L", Email = "l2@test.com", PasswordHash = LegacyHash("right"), RoleId = 3 });
        await ctx.SaveChangesAsync();
        var svc = new AuthService(ctx, new PasswordHasher<User>());
        Assert.False(await svc.ValidateUser("l2@test.com", "wrong"));
    }

    [Fact]
    public async Task RegisterUser_EmailNormalization_CaseInsensitive()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var svc = new AuthService(ctx, new PasswordHasher<User>());
        await svc.RegisterUser("A", "Test@Example.COM", "Pass12345!");
        // Second attempt with different case should fail (already exists)
        Assert.False(await svc.RegisterUser("B", "test@example.com", "Pass12345!"));
        Assert.False(await svc.RegisterUser("C", "TEST@EXAMPLE.COM", "Pass12345!"));
    }
}
