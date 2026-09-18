using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class GroupDataIntegrityTests
{
    private async Task<(AppDbContext ctx, int aliceId, int bobId, int groupId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@integrity.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@integrity.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@integrity.com");
        var bob = await auth.GetUserByEmail("bob@integrity.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Integrity", "Desc", false, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id }, alice.Id);
        return (ctx, alice.Id, bob.Id, gId);
    }

    [Fact]
    public async Task RemoveMember_WithOutstandingDebt_ShouldThrow()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Dinner", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = aliceId, AmountOwed = 50 },
                new() { UserId = bobId, AmountOwed = 50 }
            }
        }, aliceId);

        var groupSvc = new GroupService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => groupSvc.RemoveMemberAsync(gId, bobId, aliceId));

        // Still a member after the failed removal
        Assert.True(await groupSvc.IsUserMemberAsync(gId, bobId));
    }

    [Fact]
    public async Task RemoveMember_AfterSettling_ShouldSucceed()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Dinner", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = aliceId, AmountOwed = 50 },
                new() { UserId = bobId, AmountOwed = 50 }
            }
        }, aliceId);

        await expSvc.RegisterPayment(bobId, aliceId, gId, 50);

        var groupSvc = new GroupService(ctx);
        await groupSvc.RemoveMemberAsync(gId, bobId, aliceId);
        Assert.False(await groupSvc.IsUserMemberAsync(gId, bobId));
    }

    [Fact]
    public async Task SoftDeletedGroup_IsHiddenButHistoryPreserved()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Trip", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = bobId, AmountOwed = 100 } }
        }, aliceId);

        var groupSvc = new GroupService(ctx);
        await groupSvc.DeleteGroupAsync(gId, aliceId);

        // Hidden from the app
        Assert.False(await groupSvc.IsUserMemberAsync(gId, aliceId));
        Assert.Empty(await groupSvc.GetGroupsForUserAsync(aliceId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => groupSvc.GetGroupDetails(gId));

        // But the rows are preserved (soft delete, no cascade)
        var group = await ctx.Groups.IgnoreQueryFilters().FirstAsync(g => g.Id == gId);
        Assert.True(group.IsDeleted);
        Assert.NotNull(group.DeletedAt);
        Assert.Equal(aliceId, group.DeletedBy);
        var expenses = await ctx.Expense.IgnoreQueryFilters().Where(e => e.GroupId == gId).ToListAsync();
        Assert.Single(expenses);
        var shares = await ctx.ExpenseShare.IgnoreQueryFilters().Where(es => es.ExpenseId == expenses[0].Id).ToListAsync();
        Assert.Single(shares);
        var members = await ctx.GroupMembers.IgnoreQueryFilters().Where(gm => gm.GroupId == gId).ToListAsync();
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task CreateGroupWithMembers_PersistsGroupAndAllMembersAtomically()
    {
        var (ctx, aliceId, bobId, _) = await SetupAsync();
        var groupSvc = new GroupService(ctx);

        var gId = await groupSvc.CreateGroupWithMembersAsync("Atomic", "Desc", false, 1, aliceId, new[] { bobId });

        var group = await ctx.Groups.FirstOrDefaultAsync(g => g.Id == gId);
        Assert.NotNull(group);

        var members = await ctx.GroupMembers.Where(gm => gm.GroupId == gId).ToListAsync();
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.UserId == aliceId && m.Role == "creator");
        Assert.Contains(members, m => m.UserId == bobId && m.Role == "member");
    }
}
