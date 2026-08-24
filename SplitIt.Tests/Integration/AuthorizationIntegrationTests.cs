using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;

namespace SplitIt.Tests.Integration;

public class AuthorizationIntegrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public AuthorizationIntegrationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UserA_Cannot_Access_GroupB_RealDb()
    {
        if (!_fixture.IsAvailable) return; // skip
        using var ctx = _fixture.CreateContext();
        // Ensure currency
        if (!await ctx.Currencies.AnyAsync()) { ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" }); await ctx.SaveChangesAsync(); }
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("AuthA", "authA@test.com", "Pass12345!");
        await auth.RegisterUser("AuthB", "authB@test.com", "Pass12345!");
        var a = await auth.GetUserByEmail("authA@test.com");
        var b = await auth.GetUserByEmail("authB@test.com");
        var groupSvc = new GroupService(ctx);
        var gB = await groupSvc.CreateGroup("GroupB_Auth", "desc", false, 1, b!.Id);
        await groupSvc.AddGroupMembers(gB, new List<int> { b.Id }, b.Id);

        Assert.False(await groupSvc.IsUserMemberAsync(gB, a!.Id));
        Assert.True(await groupSvc.IsUserMemberAsync(gB, b.Id));

        var expSvc = new ExpensesService(ctx);
        // A trying to add expense to B's group should throw Unauthorized
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gB, Title = "Hack", Amount = 10, Date = DateTime.UtcNow, PaidById = a.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 10 } }
        }, a.Id));
    }
}
