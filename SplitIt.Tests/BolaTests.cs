using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class BolaTests
{
    private async Task<(AppDbContext ctx, User userA, User userB, Group groupA, Group groupB)> SeedAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        // Needed seed: currencies
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var userA = new User { Name = "Alice", Email = "alice@bola.com", PasswordHash = "hash", RoleId = 3 };
        var userB = new User { Name = "Bob", Email = "bob@bola.com", PasswordHash = "hash", RoleId = 3 };
        ctx.Users.AddRange(userA, userB);
        await ctx.SaveChangesAsync();

        var groupService = new GroupService(ctx);
        var gA = await groupService.CreateGroup("GroupA", "Desc A", false, 1, userA.Id);
        await groupService.AddGroupMembers(gA, new List<int> { userA.Id }, userA.Id);

        var gB = await groupService.CreateGroup("GroupB", "Desc B", false, 1, userB.Id);
        await groupService.AddGroupMembers(gB, new List<int> { userB.Id }, userB.Id);

        var groupA = await ctx.Groups.FirstAsync(g => g.Id == gA);
        var groupB = await ctx.Groups.FirstAsync(g => g.Id == gB);
        return (ctx, userA, userB, groupA, groupB);
    }

    [Fact]
    public async Task IsUserMemberAsync_UserA_NotMemberOfGroupB()
    {
        var (ctx, userA, userB, _, groupB) = await SeedAsync();
        var svc = new GroupService(ctx);
        Assert.True(await svc.IsUserMemberAsync(groupB.Id, userB.Id));
        Assert.False(await svc.IsUserMemberAsync(groupB.Id, userA.Id));
    }

    [Fact]
    public async Task AddExpense_UserNotMember_ShouldFail()
    {
        var (ctx, userA, userB, _, groupB) = await SeedAsync();
        var expSvc = new ExpensesService(ctx);
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = groupB.Id,
            Title = "Hack",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = userB.Id, AmountOwed = 100 }
            }
        };
        // userA trying to create expense in groupB (belongs to B) should throw Unauthorized
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expSvc.AddExpenseAsync(dto, userA.Id));
    }

    [Fact]
    public async Task AddExpense_ParticipantNotMember_ShouldFail()
    {
        var (ctx, _, userB, _, groupB) = await SeedAsync();
        // Need another user C not in groupB
        var userC = new User { Name = "Charlie", Email = "charlie@bola.com", PasswordHash = "hash", RoleId = 3 };
        ctx.Users.Add(userC);
        await ctx.SaveChangesAsync();

        // Debug: ensure membership is as expected
        var groupSvc = new GroupService(ctx);
        Assert.True(await groupSvc.IsUserMemberAsync(groupB.Id, userB.Id));
        Assert.False(await groupSvc.IsUserMemberAsync(groupB.Id, userC.Id));

        var expSvc = new ExpensesService(ctx);
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = groupB.Id,
            Title = "Bad participant",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = userC.Id, AmountOwed = 100 }
            }
        };
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => expSvc.AddExpenseAsync(dto, userB.Id));
        Assert.Contains("not members", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddExpense_SumMismatch_ShouldFail()
    {
        var (ctx, _, userB, _, groupB) = await SeedAsync();
        var groupSvc = new GroupService(ctx);
        Assert.True(await groupSvc.IsUserMemberAsync(groupB.Id, userB.Id));
        var expSvc = new ExpensesService(ctx);
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = groupB.Id,
            Title = "Mismatch",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = userB.Id, AmountOwed = 60 }
            }
        };
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => expSvc.AddExpenseAsync(dto, userB.Id));
        Assert.Contains("does not match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
