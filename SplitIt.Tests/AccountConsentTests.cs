using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class AccountConsentTests
{
    [Fact]
    public async Task Register_WithConsent_StoresAuthorizationProof()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        var auth = new AuthService(ctx, new PasswordHasher<User>());

        await auth.RegisterUser("Ana", "ana@consent.com", "Pass12345!",
            acceptTerms: true, consentVersion: "1.0", consentIp: "10.0.0.1");

        var user = await ctx.Users.FirstAsync(u => u.Email == "ana@consent.com");
        Assert.NotNull(user.ConsentAt);
        Assert.Equal("1.0", user.ConsentVersion);
        Assert.Equal("10.0.0.1", user.ConsentIp);
    }

    [Fact]
    public async Task Register_WithoutConsent_LeavesConsentEmpty()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        var auth = new AuthService(ctx, new PasswordHasher<User>());

        await auth.RegisterUser("NoConsent", "no@consent.com", "Pass12345!");

        var user = await ctx.Users.FirstAsync(u => u.Email == "no@consent.com");
        Assert.Null(user.ConsentAt);
        Assert.Null(user.ConsentVersion);
    }

    [Fact]
    public async Task ExportUserData_ReturnsProfileGroupsAndExpenses()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Ex", "ex@export.com", "Pass12345!", true, "1.0", "127.0.0.1");
        await auth.RegisterUser("Other", "other@export.com", "Pass12345!", true, "1.0", "127.0.0.1");
        var ex = await auth.GetUserByEmail("ex@export.com");
        var other = await auth.GetUserByEmail("other@export.com");

        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Export", "D", false, 1, ex!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { ex.Id, other!.Id }, ex.Id);

        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Dinner", Amount = 100, Date = DateTime.UtcNow, PaidById = ex.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = other.Id, AmountOwed = 100 } }
        }, ex.Id);

        var usersService = new UsersService(ctx, hasher);
        var export = await usersService.ExportUserDataAsync(ex.Id);

        Assert.Equal("ex@export.com", export.Email);
        Assert.Equal("1.0", export.ConsentVersion);
        Assert.Single(export.Groups);
        Assert.Single(export.Expenses);
        Assert.Equal("Dinner", export.Expenses[0].Title);
    }

    [Fact]
    public async Task DeleteAccount_AnonymizesAndRevokesTokens()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Del", "del@delete.com", "Pass12345!", true, "1.0", "1.2.3.4");
        var user = await auth.GetUserByEmail("del@delete.com");

        ctx.RefreshTokens.Add(new RefreshToken { UserId = user!.Id, TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(10) });
        await ctx.SaveChangesAsync();

        var usersService = new UsersService(ctx, hasher);
        await usersService.DeleteAccountAsync(user.Id, "Pass12345!");

        var updated = await ctx.Users.FirstAsync(u => u.Id == user.Id);
        Assert.False(updated.IsActive);
        Assert.Equal("Deleted user", updated.Name);
        Assert.StartsWith("deleted+", updated.Email);
        Assert.Null(updated.ConsentIp);
        Assert.NotNull(updated.DeletedAt);

        var token = await ctx.RefreshTokens.FirstAsync(r => r.UserId == user.Id);
        Assert.NotNull(token.RevokedAt);

        // Cannot log in anymore
        Assert.False(await auth.ValidateUser("del@delete.com", "Pass12345!"));
    }

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_Throws()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Del2", "del2@delete.com", "Pass12345!");
        var user = await auth.GetUserByEmail("del2@delete.com");

        var usersService = new UsersService(ctx, hasher);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => usersService.DeleteAccountAsync(user!.Id, "WrongPassword1!"));
    }
}
