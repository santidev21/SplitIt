using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class DeleteExpenseTests
{
    private static async Task<(AppDbContext ctx, ExpensesService svc, int aliceId, int bobId, int carolId, int groupId)> SetupAsync(bool allowToDeleteExpenses = false)
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var auth = new AuthService(ctx, new PasswordHasher<User>());
        await auth.RegisterUser("Alice", "alice@delete.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@delete.com", "Pass12345!");
        await auth.RegisterUser("Carol", "carol@delete.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@delete.com");
        var bob = await auth.GetUserByEmail("bob@delete.com");
        var carol = await auth.GetUserByEmail("carol@delete.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Delete Group", "Desc", allowToDeleteExpenses, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id, carol!.Id }, alice.Id);
        return (ctx, new ExpensesService(ctx), alice.Id, bob.Id, carol.Id, gId);
    }

    private static async Task<int> AddExpenseAsync(ExpensesService svc, int groupId, int paidById, int debtorId, int createdById)
    {
        var expense = await svc.AddExpenseAsync(new CreateExpenseDto
        {
            GroupId = groupId,
            Title = "Dinner",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = paidById,
            Participants = new List<ExpenseParticipantDto>
            {
                new() { UserId = paidById, AmountOwed = 50 },
                new() { UserId = debtorId, AmountOwed = 50 },
            },
        }, createdById);
        return expense.Id;
    }

    [Fact]
    public async Task UnpaidExpense_IsDeletedWithShares_AndSnapshotIsAudited()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);

        await svc.DeleteExpenseAsync(expenseId, bobId);

        Assert.False(await ctx.Expense.AnyAsync(e => e.Id == expenseId));
        Assert.False(await ctx.ExpenseShare.AnyAsync(s => s.ExpenseId == expenseId));
        Assert.Equal(0, await svc.GetRemainingDebtAsync(aliceId, bobId, gId));
        var snapshot = await ctx.AuditLogs.SingleAsync(a => a.Action == "delete-snapshot" && a.EntityId == expenseId.ToString());
        Assert.Contains("Dinner", snapshot.Details);
    }

    [Fact]
    public async Task ExpenseWithPartialPayment_CannotBeDeleted()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await svc.RegisterPayment(aliceId, bobId, gId, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(expenseId, bobId));
        Assert.True(await ctx.Expense.AnyAsync(e => e.Id == expenseId));
    }

    [Fact]
    public async Task FullySettledExpense_CannotBeDeleted()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await svc.SettleExpenseWithUser(aliceId, bobId, gId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(expenseId, bobId));
        Assert.True(await ctx.Expense.AnyAsync(e => e.Id == expenseId));
    }

    [Fact]
    public async Task ReciprocalExpenseOffsetByPayment_CannotBeDeleted()
    {
        // Alice owes Bob 50 and Bob owes Alice 50 on another expense: a payment between
        // them offsets Bob's debt first, which marks that share as paid.
        var (_, svc, aliceId, bobId, carolId, gId) = await SetupAsync();
        await svc.AddExpenseAsync(new CreateExpenseDto
        {
            GroupId = gId, Title = "Taxi", Amount = 100, Date = DateTime.UtcNow, PaidById = bobId,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = aliceId, AmountOwed = 100 } },
        }, bobId);
        var reciprocalId = await AddExpenseAsync(svc, gId, aliceId, bobId, aliceId);
        await svc.RegisterPayment(aliceId, bobId, gId, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(reciprocalId, aliceId));
    }

    [Fact]
    public async Task SimplifiedDebts_OriginalExpensesAndSimplificationCannotBeDeleted()
    {
        // Alice -> Bob -> Carol chain collapses into Alice -> Carol.
        var (ctx, svc, aliceId, bobId, carolId, gId) = await SetupAsync();
        var first = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        var second = await AddExpenseAsync(svc, gId, carolId, bobId, carolId);
        var result = await svc.SimplifyDebtsAsync(gId, aliceId);
        Assert.True(result.Applied);
        var simplificationId = await ctx.Expense.Where(e => e.IsSimplification).Select(e => e.Id).FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(first, aliceId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(second, aliceId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(simplificationId, aliceId));
    }

    [Fact]
    public async Task PaymentAppliedToOlderExpense_NewerUnpaidExpenseCanBeDeleted_AndBalanceStaysConsistent()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var older = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await Task.Delay(5);
        var newer = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await svc.RegisterPayment(aliceId, bobId, gId, 20); // applied oldest-first

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(older, bobId));
        await svc.DeleteExpenseAsync(newer, bobId);

        // 50 owed on the older expense minus the 20 already paid.
        Assert.Equal(30, await svc.GetRemainingDebtAsync(aliceId, bobId, gId));
        Assert.True(await ctx.Expense.AnyAsync(e => e.IsPayment));
    }

    [Fact]
    public async Task PaymentExpense_CannotBeDeleted()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await svc.RegisterPayment(aliceId, bobId, gId, 10);
        var paymentId = await ctx.Expense.Where(e => e.IsPayment).Select(e => e.Id).SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteExpenseAsync(paymentId, aliceId));
    }

    [Fact]
    public async Task GroupCreator_CanDeleteExpenseCreatedByOtherMember()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);

        await svc.DeleteExpenseAsync(expenseId, aliceId);

        Assert.False(await ctx.Expense.AnyAsync(e => e.Id == expenseId));
    }

    [Fact]
    public async Task RegularMember_CannotDeleteOthersExpense_UnlessGroupAllowsIt()
    {
        var (_, svc, aliceId, bobId, carolId, gId) = await SetupAsync(allowToDeleteExpenses: false);
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteExpenseAsync(expenseId, carolId));

        var (ctx2, svc2, alice2, bob2, carol2, g2) = await SetupAsync(allowToDeleteExpenses: true);
        var expense2 = await AddExpenseAsync(svc2, g2, bob2, alice2, bob2);
        await svc2.DeleteExpenseAsync(expense2, carol2);
        Assert.False(await ctx2.Expense.AnyAsync(e => e.Id == expense2));
    }

    [Fact]
    public async Task NonMember_CannotDelete_AndMissingExpenseIsNotFound()
    {
        var (ctx, svc, aliceId, bobId, _, gId) = await SetupAsync();
        var expenseId = await AddExpenseAsync(svc, gId, bobId, aliceId, bobId);
        var outsider = new User { Name = "Mallory", Email = "mallory@delete.com", PasswordHash = "x" };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteExpenseAsync(expenseId, outsider.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteExpenseAsync(999_999, aliceId));
    }
}
