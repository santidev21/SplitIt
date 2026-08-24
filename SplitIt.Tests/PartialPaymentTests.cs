using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class PartialPaymentTests
{
    private async Task<(AppDbContext ctx, int aliceId, int bobId, int groupId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@partial.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@partial.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@partial.com");
        var bob = await auth.GetUserByEmail("bob@partial.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Partial Group", "Desc", false, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id }, alice.Id);
        return (ctx, alice.Id, bob.Id, gId);
    }

    [Fact]
    public async Task PartialPayment_30_of_100_Remaining70()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Dinner", Amount = 100, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 100 } }
        }, bobId);

        var remainingBefore = await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId);
        Assert.Equal(100, remainingBefore);

        await expSvc.RegisterPayment(aliceId, bobId, gId, 30);
        var remainingAfter = await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId);
        Assert.Equal(70, remainingAfter);

        // Verify share reduced, not fully settled
        var shares = await ctx.ExpenseShare.Include(es => es.Expense).Where(es => es.UserId == aliceId && es.Expense.GroupId == gId && !es.Expense.IsPayment).ToListAsync();
        Assert.Single(shares);
        Assert.Equal(70, shares[0].AmountOwed);
        Assert.False(shares[0].IsSettled);
    }

    [Fact]
    public async Task MultiplePartialPayments_30_20_50_ShouldSettle()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Trip", Amount = 100, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 100 } }
        }, bobId);

        await expSvc.RegisterPayment(aliceId, bobId, gId, 30);
        Assert.Equal(70, await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId));
        await expSvc.RegisterPayment(aliceId, bobId, gId, 20);
        Assert.Equal(50, await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId));
        await expSvc.RegisterPayment(aliceId, bobId, gId, 50);
        Assert.Equal(0, await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId));

        var shares = await ctx.ExpenseShare.Include(es => es.Expense).Where(es => es.UserId == aliceId && !es.Expense.IsPayment).ToListAsync();
        Assert.True(shares[0].IsSettled);
    }

    [Fact]
    public async Task ExactFinalPayment_ShouldSettle()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Lunch", Amount = 50, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 50 } }
        }, bobId);
        await expSvc.RegisterPayment(aliceId, bobId, gId, 50);
        Assert.Equal(0, await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId));
    }

    [Fact]
    public async Task PaymentGreaterThanDebt_ShouldThrow()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Coffee", Amount = 20, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 20 } }
        }, bobId);
        await Assert.ThrowsAsync<ArgumentException>(() => expSvc.RegisterPayment(aliceId, bobId, gId, 30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public async Task InvalidPayment_ShouldThrow(decimal amount)
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Test", Amount = 100, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 100 } }
        }, bobId);
        await Assert.ThrowsAsync<ArgumentException>(() => expSvc.RegisterPayment(aliceId, bobId, gId, amount));
    }

    [Fact]
    public async Task NoDebt_ShouldThrow()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => expSvc.RegisterPayment(aliceId, bobId, gId, 10));
    }

    [Fact]
    public async Task Payment_AcrossMultipleShares_ShouldDistribute()
    {
        var (ctx, aliceId, bobId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        // Two expenses: 60 and 40, total 100
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "E1", Amount = 60, Date = DateTime.UtcNow.AddDays(-1), PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 60 } }
        }, bobId);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "E2", Amount = 40, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 40 } }
        }, bobId);

        // Pay 70: should fully settle 60 and partially 10 of 40 -> remaining 30
        await expSvc.RegisterPayment(aliceId, bobId, gId, 70);
        var remaining = await expSvc.GetRemainingDebtAsync(aliceId, bobId, gId);
        Assert.Equal(30, remaining);

        var shares = await ctx.ExpenseShare.Include(es => es.Expense).Where(es => !es.Expense.IsPayment).OrderBy(es => es.Expense.Date).ToListAsync();
        Assert.True(shares[0].IsSettled);
        Assert.Equal(30, shares[1].AmountOwed);
        Assert.False(shares[1].IsSettled);
    }
}
