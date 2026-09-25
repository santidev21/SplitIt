using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

/// <summary>
/// Verifies that monetary values respect the currency's decimal scale (USD cents vs
/// whole units for COP) and that expense shares always conserve the exact total —
/// a cent is never silently created or dropped.
/// </summary>
public class CurrencyPrecisionTests
{
    private async Task<(AppDbContext ctx, int aliceId, int bobId, int usdGroupId, int copGroupId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "Dólar", Symbol = "USD", DecimalPlaces = 2 });
        ctx.Currencies.Add(new Currency { Id = 2, Name = "Peso Colombiano", Symbol = "COP", DecimalPlaces = 0 });
        await ctx.SaveChangesAsync();

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@precision.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@precision.com", "Pass12345!");
        var alice = (await auth.GetUserByEmail("alice@precision.com"))!;
        var bob = (await auth.GetUserByEmail("bob@precision.com"))!;

        var groupSvc = new GroupService(ctx);
        var usdGroup = await groupSvc.CreateGroupWithMembersAsync("USD Group", "d", false, 1, alice.Id, new[] { bob.Id });
        var copGroup = await groupSvc.CreateGroupWithMembersAsync("COP Group", "d", false, 2, alice.Id, new[] { bob.Id });

        return (ctx, alice.Id, bob.Id, usdGroup, copGroup);
    }

    private static CreateExpenseDto Expense(int groupId, decimal amount, int paidById, params (int UserId, decimal AmountOwed)[] participants)
    {
        return new CreateExpenseDto
        {
            GroupId = groupId,
            Title = "Test",
            Amount = amount,
            Date = DateTime.UtcNow,
            PaidById = paidById,
            Participants = participants.Select(p => new ExpenseParticipantDto { UserId = p.UserId, AmountOwed = p.AmountOwed }).ToList()
        };
    }

    [Fact]
    public async Task UsdGroup_AcceptsCents()
    {
        var (ctx, aliceId, bobId, usdGroup, _) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        var expense = await svc.AddExpenseAsync(Expense(usdGroup, 100.01m, bobId, (aliceId, 50.00m), (bobId, 50.01m)), bobId);
        Assert.True(expense.Id > 0);
    }

    [Fact]
    public async Task CopGroup_RejectsFractionalAmount()
    {
        var (ctx, aliceId, bobId, _, copGroup) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddExpenseAsync(Expense(copGroup, 100.5m, bobId, (aliceId, 60m), (bobId, 40m)), bobId));
        Assert.Contains("decimal place", ex.Message);
    }

    [Fact]
    public async Task CopGroup_RejectsFractionalShare()
    {
        var (ctx, aliceId, bobId, _, copGroup) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddExpenseAsync(Expense(copGroup, 100m, bobId, (aliceId, 60.5m), (bobId, 39.5m)), bobId));
        Assert.Contains("decimal place", ex.Message);
    }

    [Fact]
    public async Task CopGroup_WholeUnits_Succeeds()
    {
        var (ctx, aliceId, bobId, _, copGroup) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        var expense = await svc.AddExpenseAsync(Expense(copGroup, 100m, bobId, (aliceId, 60m), (bobId, 40m)), bobId);
        Assert.True(expense.Id > 0);
    }

    [Fact]
    public async Task SumMustEqualTotal_Exactly()
    {
        // 33.33 + 33.33 + 33.33 = 99.99, which is NOT 100: must be rejected (no phantom cent).
        var (ctx, aliceId, bobId, usdGroup, _) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddExpenseAsync(Expense(usdGroup, 100m, bobId, (aliceId, 33.33m), (bobId, 33.33m), (aliceId, 33.33m)), bobId));
        Assert.Contains("must equal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopPayment_Fractional_Throws()
    {
        var (ctx, aliceId, bobId, _, copGroup) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        await svc.AddExpenseAsync(Expense(copGroup, 100m, bobId, (aliceId, 100m)), bobId);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.RegisterPayment(aliceId, bobId, copGroup, 50.5m));
        Assert.Contains("decimal place", ex.Message);
    }

    [Fact]
    public async Task Payment_OneCentOverRemainingDebt_Throws()
    {
        var (ctx, aliceId, bobId, usdGroup, _) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        await svc.AddExpenseAsync(Expense(usdGroup, 100m, bobId, (aliceId, 100m)), bobId);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RegisterPayment(aliceId, bobId, usdGroup, 100.01m));
    }

    [Fact]
    public async Task Payment_ExactlyRemainingDebt_Succeeds()
    {
        var (ctx, aliceId, bobId, usdGroup, _) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        await svc.AddExpenseAsync(Expense(usdGroup, 100m, bobId, (aliceId, 100m)), bobId);
        await svc.RegisterPayment(aliceId, bobId, usdGroup, 100m);
        Assert.Equal(0m, await svc.GetRemainingDebtAsync(aliceId, bobId, usdGroup));
    }

    [Fact]
    public async Task Payment_StrictlyBelowDebt_StillAccepted()
    {
        var (ctx, aliceId, bobId, usdGroup, _) = await SetupAsync();
        var svc = new ExpensesService(ctx);
        await svc.AddExpenseAsync(Expense(usdGroup, 100m, bobId, (aliceId, 100m)), bobId);
        await svc.RegisterPayment(aliceId, bobId, usdGroup, 30m);
        Assert.Equal(70m, await svc.GetRemainingDebtAsync(aliceId, bobId, usdGroup));
    }
}