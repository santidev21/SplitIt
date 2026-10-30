using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class DebtSimplificationServiceTests
{
    private static async Task<(AppDbContext ctx, ExpensesService svc, GroupService groups, int groupId, int[] users)> SetupAsync(
        int memberCount, int decimalPlaces = 2)
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "TST", Symbol = "$", DecimalPlaces = decimalPlaces });
        await ctx.SaveChangesAsync();

        var auth = new AuthService(ctx, new PasswordHasher<User>());
        var users = new int[memberCount];
        for (var i = 0; i < memberCount; i++)
        {
            await auth.RegisterUser($"User{i}", $"user{i}@simplify.com", "Pass12345!");
            users[i] = (await auth.GetUserByEmail($"user{i}@simplify.com"))!.Id;
        }

        var groups = new GroupService(ctx);
        var groupId = await groups.CreateGroup("Trip", "Desc", false, 1, users[0]);
        await groups.AddGroupMembers(groupId, users.ToList(), users[0]);
        return (ctx, new ExpensesService(ctx), groups, groupId, users);
    }

    private static Task AddExpenseAsync(ExpensesService svc, int groupId, int paidBy, params (int UserId, decimal Amount)[] shares) =>
        svc.AddExpenseAsync(new CreateExpenseDto
        {
            GroupId = groupId,
            Title = "Expense",
            Amount = shares.Sum(s => s.Amount),
            Date = DateTime.UtcNow,
            PaidById = paidBy,
            Participants = shares.Select(s => new ExpenseParticipantDto { UserId = s.UserId, AmountOwed = s.Amount }).ToList(),
        }, paidBy);

    /// <summary>Net balance per member as the summary endpoint reports it (owed to - owed by).</summary>
    private static async Task<Dictionary<int, decimal>> BalancesAsync(ExpensesService svc, int groupId, int[] users)
    {
        var result = new Dictionary<int, decimal>();
        foreach (var user in users)
        {
            var summary = await svc.GetFullDebtSummaryAsync(user, groupId);
            result[user] = summary.DebtsOwedToUser.Sum(d => d.TotalAmountOwed) - summary.DebtsOwedByUser.Sum(d => d.TotalAmountOwed);
        }
        return result;
    }

    [Fact]
    public async Task Chain_MeOwesCarlos_CarlosOwesLuisa_BecomesMePaysLuisa()
    {
        var (ctx, svc, groups, gId, u) = await SetupAsync(3);
        int me = u[0], carlos = u[1], luisa = u[2];
        await AddExpenseAsync(svc, gId, paidBy: carlos, (me, 200m));
        await AddExpenseAsync(svc, gId, paidBy: luisa, (carlos, 200m));

        var preview = await svc.PreviewDebtSimplificationAsync(gId);
        Assert.True(preview.CanSimplify);
        Assert.Equal(2, preview.CurrentTransferCount);
        var transfer = Assert.Single(preview.Transfers);
        Assert.Equal((me, luisa, 200m), (transfer.FromUserId, transfer.ToUserId, transfer.Amount));
        Assert.Equal("User0", transfer.FromUserName);
        Assert.Equal(0, await ctx.Expense.CountAsync(e => e.IsSimplification)); // preview writes nothing

        var result = await svc.SimplifyDebtsAsync(gId, me);
        Assert.True(result.Applied);

        var mine = await svc.GetFullDebtSummaryAsync(me, gId);
        var debt = Assert.Single(mine.DebtsOwedByUser);
        Assert.Equal((luisa, 200m), (debt.CreditorUserId, debt.TotalAmountOwed));
        Assert.Empty(mine.DebtsOwedToUser);
        var carlosSummary = await svc.GetFullDebtSummaryAsync(carlos, gId);
        Assert.Empty(carlosSummary.DebtsOwedByUser);
        Assert.Empty(carlosSummary.DebtsOwedToUser);

        // Carlos is now free of debt and can leave; I pay Luisa in two parts.
        await groups.RemoveMemberAsync(gId, carlos, me);
        await svc.RegisterPayment(me, luisa, gId, 50m);
        Assert.Equal(150m, await svc.GetRemainingDebtAsync(me, luisa, gId));
        await svc.RegisterPayment(me, luisa, gId, 150m);
        Assert.Equal(0m, await svc.GetRemainingDebtAsync(me, luisa, gId));
        Assert.All(await BalancesAsync(svc, gId, new[] { me, luisa }), b => Assert.Equal(0m, b.Value));
    }

    [Fact]
    public async Task LargeGroup_KeepsEveryBalance_AndAllowsPayingEverythingOff()
    {
        var (_, svc, _, gId, u) = await SetupAsync(8);
        var random = new Random(7);
        for (var i = 0; i < 30; i++)
        {
            var paidBy = u[random.Next(u.Length)];
            var participants = u.Where(_ => random.Next(2) == 0).ToList();
            if (participants.Count == 0) participants.Add(u[0]);
            var split = SplitCalculator.EqualSplit(random.Next(100, 50_000) / 100m, participants);
            await AddExpenseAsync(svc, gId, paidBy, split.Select(s => (s.UserId, s.AmountOwed)).ToArray());
        }
        // A partial payment before simplifying must be respected.
        var before0 = await svc.GetFullDebtSummaryAsync(u[1], gId);
        if (before0.DebtsOwedByUser.Count > 0)
        {
            var creditor = before0.DebtsOwedByUser[0].CreditorUserId;
            var remaining = await svc.GetRemainingDebtAsync(u[1], creditor, gId);
            if (remaining > 1m) await svc.RegisterPayment(u[1], creditor, gId, 1m);
        }

        var before = await BalancesAsync(svc, gId, u);
        var result = await svc.SimplifyDebtsAsync(gId, u[0]);

        Assert.True(result.Applied);
        Assert.True(result.SimplifiedTransferCount <= before.Count(b => b.Value != 0) - 1);
        Assert.True(result.SimplifiedTransferCount < result.CurrentTransferCount);
        Assert.Equal(before, await BalancesAsync(svc, gId, u));

        // Simplifying again is a no-op.
        var again = await svc.SimplifyDebtsAsync(gId, u[0]);
        Assert.False(again.Applied);
        Assert.False(again.CanSimplify);

        // Every member pays what the plan says (in two partial payments) and everyone ends at zero.
        foreach (var t in result.Transfers)
        {
            var half = Math.Round(t.Amount / 2, 2, MidpointRounding.AwayFromZero);
            if (half > 0 && half < t.Amount)
                await svc.RegisterPayment(t.FromUserId, t.ToUserId, gId, half);
            await svc.RegisterPayment(t.FromUserId, t.ToUserId, gId, await svc.GetRemainingDebtAsync(t.FromUserId, t.ToUserId, gId));
        }
        Assert.All(await BalancesAsync(svc, gId, u), b => Assert.Equal(0m, b.Value));
    }

    [Fact]
    public async Task SinglePairOfDebts_IsNotRewritten()
    {
        var (ctx, svc, _, gId, u) = await SetupAsync(2);
        await AddExpenseAsync(svc, gId, paidBy: u[1], (u[0], 30m), (u[1], 30m));

        var result = await svc.SimplifyDebtsAsync(gId, u[0]);

        Assert.False(result.Applied);
        Assert.False(result.CanSimplify);
        Assert.Equal(1, result.SimplifiedTransferCount);
        Assert.Equal(0, await ctx.Expense.CountAsync(e => e.IsSimplification));
    }

    [Fact]
    public async Task ExpensesAddedAfterSimplifying_StackOnTheConsolidatedDebts()
    {
        var (_, svc, _, gId, u) = await SetupAsync(3);
        await AddExpenseAsync(svc, gId, paidBy: u[1], (u[0], 200m));
        await AddExpenseAsync(svc, gId, paidBy: u[2], (u[1], 200m));
        await svc.SimplifyDebtsAsync(gId, u[0]);

        await AddExpenseAsync(svc, gId, paidBy: u[0], (u[2], 50m));

        Assert.Equal(150m, await svc.GetRemainingDebtAsync(u[0], u[2], gId));
    }

    [Fact]
    public async Task WholeUnitCurrency_ProducesWholeTransfers()
    {
        var (_, svc, _, gId, u) = await SetupAsync(4, decimalPlaces: 0);
        await AddExpenseAsync(svc, gId, paidBy: u[0], (u[1], 33_333m), (u[2], 33_333m), (u[3], 33_334m));
        await AddExpenseAsync(svc, gId, paidBy: u[1], (u[2], 10_000m), (u[3], 5_000m));

        var result = await svc.SimplifyDebtsAsync(gId, u[0]);

        Assert.All(result.Transfers, t => Assert.Equal(decimal.Truncate(t.Amount), t.Amount));
        foreach (var t in result.Transfers)
            await svc.RegisterPayment(t.FromUserId, t.ToUserId, gId, t.Amount);
        Assert.All(await BalancesAsync(svc, gId, u), b => Assert.Equal(0m, b.Value));
    }

    [Fact]
    public async Task PayingTheNetAmount_SettlesBothDirections()
    {
        var (ctx, svc, groups, gId, u) = await SetupAsync(2);
        int alice = u[0], bob = u[1];
        await AddExpenseAsync(svc, gId, paidBy: bob, (alice, 100m));
        await AddExpenseAsync(svc, gId, paidBy: alice, (bob, 40m));

        Assert.Equal(60m, await svc.GetRemainingDebtAsync(alice, bob, gId));
        await svc.RegisterPayment(alice, bob, gId, 60m);

        Assert.Equal(0m, await svc.GetRemainingDebtAsync(alice, bob, gId));
        var debtShares = await ctx.ExpenseShare.Include(es => es.Expense).Where(es => !es.Expense.IsPayment).ToListAsync();
        Assert.All(debtShares, s => Assert.True(s.IsSettled));
        Assert.All(debtShares, s => Assert.Equal(s.AmountOwed, s.AmountPaid));
        await groups.RemoveMemberAsync(gId, bob, alice);
    }

    [Fact]
    public async Task MemberWhoPaidTheirOwnShare_CanBeRemovedOnceSettled()
    {
        var (_, svc, groups, gId, u) = await SetupAsync(3);
        // u[1] pays a dinner split equally with themself included.
        await AddExpenseAsync(svc, gId, paidBy: u[1], (u[0], 10m), (u[1], 10m));
        await svc.RegisterPayment(u[0], u[1], gId, 10m);

        await groups.RemoveMemberAsync(gId, u[1], u[0]);
    }

    [Fact]
    public async Task PaymentWithSubCentPrecision_IsRejectedInsteadOfRounded()
    {
        var (_, svc, _, gId, u) = await SetupAsync(2);
        await AddExpenseAsync(svc, gId, paidBy: u[1], (u[0], 100m));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.RegisterPayment(u[0], u[1], gId, 10.005m));
        Assert.Contains("decimal place", ex.Message);
    }
}

public class AddExpenseResponseTests
{
    [Fact]
    public async Task AddExpense_ReturnsASerializableBody()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var auth = new AuthService(ctx, new PasswordHasher<User>());
        await auth.RegisterUser("Ana", "ana@response.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@response.com", "Pass12345!");
        var ana = (await auth.GetUserByEmail("ana@response.com"))!.Id;
        var bob = (await auth.GetUserByEmail("bob@response.com"))!.Id;
        var groups = new GroupService(ctx);
        var gId = await groups.CreateGroup("G", "D", false, 1, ana);
        await groups.AddGroupMembers(gId, new List<int> { ana, bob }, ana);

        var controller = new SplitIt.API.Controllers.ExpensesController(
            new ExpensesService(ctx), groups, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, ana.ToString()) }, "test")),
                },
            },
        };

        var result = await controller.AddExpense(new CreateExpenseDto
        {
            GroupId = gId,
            Title = "Dinner",
            Amount = 50m,
            Date = DateTime.UtcNow,
            PaidById = ana,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = bob, AmountOwed = 50m } },
        });

        // The tracked entity has a Group.Expenses cycle; the API must not return it.
        var created = Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(created.Value);
        Assert.Contains("\"Amount\":50", json);
    }
}
