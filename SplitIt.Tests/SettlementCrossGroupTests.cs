using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class SettlementCrossGroupTests
{
    [Fact]
    public async Task Settle_GroupA_ShouldNotAffect_GroupB()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var userA = new User { Name = "Alice", Email = "a@settle.com", PasswordHash = "h", RoleId = 3 };
        var userB = new User { Name = "Bob", Email = "b@settle.com", PasswordHash = "h", RoleId = 3 };
        ctx.Users.AddRange(userA, userB);
        await ctx.SaveChangesAsync();

        var groupSvc = new GroupService(ctx);
        var gA = await groupSvc.CreateGroup("GroupA", "Desc", false, 1, userA.Id);
        await groupSvc.AddGroupMembers(gA, new List<int> { userA.Id, userB.Id }, userA.Id);

        var gB = await groupSvc.CreateGroup("GroupB", "Desc", false, 1, userA.Id);
        await groupSvc.AddGroupMembers(gB, new List<int> { userA.Id, userB.Id }, userA.Id);

        var expSvc = new ExpensesService(ctx);

        // Expense in GroupA: Bob paid 100, Alice owes 100
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gA,
            Title = "Expense A",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = userA.Id, AmountOwed = 100 } }
        }, userB.Id);

        // Expense in GroupB: Bob paid 50, Alice owes 50
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gB,
            Title = "Expense B",
            Amount = 50,
            Date = DateTime.UtcNow,
            PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = userA.Id, AmountOwed = 50 } }
        }, userB.Id);

        // Settle only GroupA: Alice owes Bob in group A (payer = Alice's debt owner? In our logic, Expense paidBy=B, userId=A owes, so settle needs payerUserId=B? Let's check: SettleExpenseWithUser finds shares where (UserId==receiver && PaidBy==payer) or vice versa. Here A owes B (A's share with PaidBy B). So to settle, payer = B (who is owed), receiver = A (who owes) or opposite? Controller does receiver = logged user. We'll call SettleExpenseWithUser(payer=B, receiver=A, group=gA) should settle GroupA only.
        var settledA = await expSvc.SettleExpenseWithUser(userB.Id, userA.Id, gA);
        Assert.Equal(1, settledA);

        // Check GroupB still has unsettled debt
        var unsettledInB = await ctx.ExpenseShare
            .Include(es => es.Expense)
            .CountAsync(es => !es.IsSettled && es.Expense.GroupId == gB);
        Assert.Equal(1, unsettledInB);

        var unsettledInA = await ctx.ExpenseShare
            .Include(es => es.Expense)
            .CountAsync(es => !es.IsSettled && es.Expense.GroupId == gA);
        Assert.Equal(0, unsettledInA);

        // Settle GroupB should now work separately
        var settledB = await expSvc.SettleExpenseWithUser(userB.Id, userA.Id, gB);
        Assert.Equal(1, settledB);
    }

    [Fact]
    public async Task Settle_WrongGroup_ShouldNotSettleOtherGroup()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var userA = new User { Name = "A", Email = "a2@settle.com", PasswordHash = "h", RoleId = 3 };
        var userB = new User { Name = "B", Email = "b2@settle.com", PasswordHash = "h", RoleId = 3 };
        ctx.Users.AddRange(userA, userB);
        await ctx.SaveChangesAsync();

        var groupSvc = new GroupService(ctx);
        var gA = await groupSvc.CreateGroup("GA", "d", false, 1, userA.Id);
        await groupSvc.AddGroupMembers(gA, new List<int> { userA.Id, userB.Id }, userA.Id);
        var gB = await groupSvc.CreateGroup("GB", "d", false, 1, userA.Id);
        await groupSvc.AddGroupMembers(gB, new List<int> { userA.Id, userB.Id }, userA.Id);

        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gA, Title = "EA", Amount = 100, Date = DateTime.UtcNow, PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = userA.Id, AmountOwed = 100 } }
        }, userB.Id);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gB, Title = "EB", Amount = 200, Date = DateTime.UtcNow, PaidById = userB.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = userA.Id, AmountOwed = 200 } }
        }, userB.Id);

        // Attempt to settle with non-existent group should throw
        await Assert.ThrowsAsync<KeyNotFoundException>(() => expSvc.SettleExpenseWithUser(userB.Id, userA.Id, 9999));
    }
}
