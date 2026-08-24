using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using SplitIt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SplitIt.Tests;

public class SplitMethodTests
{
    private async Task<(AppDbContext ctx, int aliceId, int bobId, int charlieId, int groupId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@split.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@split.com", "Pass12345!");
        await auth.RegisterUser("Charlie", "charlie@split.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@split.com");
        var bob = await auth.GetUserByEmail("bob@split.com");
        var charlie = await auth.GetUserByEmail("charlie@split.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Split Group", "Desc", false, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id, charlie!.Id }, alice.Id);
        return (ctx, alice.Id, bob.Id, charlie.Id, gId);
    }

    [Fact]
    public async Task EqualSplit_100_Among3_ShouldDistributeCorrectly()
    {
        var (ctx, aliceId, bobId, charlieId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        // Simulate equal split 100/3 = 33.33,33.33,33.34
        var per1 = Math.Floor((100m / 3) * 100) / 100; // 33.33
        var remainder = Math.Round((100 - per1 * 3) * 100); // 1 cent
        var participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
        {
            new() { UserId = aliceId, AmountOwed = per1 + (0 < remainder ? 0.01m : 0) },
            new() { UserId = bobId, AmountOwed = per1 + (1 < remainder ? 0.01m : 0) },
            new() { UserId = charlieId, AmountOwed = per1 + (2 < remainder ? 0.01m : 0) },
        };
        // Adjust to ensure sum
        var sum = participants.Sum(p => p.AmountOwed);
        Assert.Equal(100, sum);

        var exp = await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Equal", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId, Participants = participants
        }, aliceId);
        Assert.True(exp.Id > 0);
    }

    [Theory]
    [InlineData(100, new double[] { 50, 30, 20 })] // valid 100%
    [InlineData(90, new double[] { 30, 60 })] // valid fixed
    public async Task FixedAmount_Valid_ShouldPass(decimal total, double[] amounts)
    {
        var (ctx, aliceId, bobId, charlieId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        var ids = new[] { aliceId, bobId, charlieId };
        var participants = amounts.Select((a, i) => new SplitIt.Application.DTOs.ExpenseParticipantDto { UserId = ids[i], AmountOwed = (decimal)a }).ToList();
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Fixed", Amount = total, Date = DateTime.UtcNow, PaidById = aliceId, Participants = participants
        };
        var exp = await expSvc.AddExpenseAsync(dto, aliceId);
        Assert.True(exp.Id > 0);
    }

    [Fact]
    public async Task FixedAmount_SumNotEqual_ShouldThrow()
    {
        var (ctx, aliceId, bobId, _, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Bad", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = aliceId, AmountOwed = 60 },
                new() { UserId = bobId, AmountOwed = 30 } // sum 90 !=100
            }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => expSvc.AddExpenseAsync(dto, aliceId));
    }

    [Theory]
    [InlineData(new double[] { 50, 30, 10 })] // sum 90% not 100
    [InlineData(new double[] { 60, 60 })] // 120%
    [InlineData(new double[] { -10, 110 })] // negative
    public async Task Percentage_Invalid_ShouldFail_WhenSumNot100(double[] percentages)
    {
        var (ctx, aliceId, bobId, charlieId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        var total = 100m;
        var ids = new[] { aliceId, bobId, charlieId };
        // Simulate percentage calc as frontend does: (pct/100)*total
        var participants = percentages.Select((p, i) => new SplitIt.Application.DTOs.ExpenseParticipantDto
        {
            UserId = ids[i],
            AmountOwed = Math.Round((decimal)(p / 100) * total, 2, MidpointRounding.AwayFromZero)
        }).Where(p => p.AmountOwed > 0).ToList();

        var sum = participants.Sum(p => p.AmountOwed);
        // If percentages don't sum to 100, sum != total, so backend should throw due to sum mismatch
        if (Math.Abs(sum - total) > 0.01m)
        {
            var dto = new SplitIt.Application.DTOs.CreateExpenseDto
            {
                GroupId = gId, Title = "PctBad", Amount = total, Date = DateTime.UtcNow, PaidById = aliceId, Participants = participants
            };
            // If participants empty due to negative filter, also throw
            if (participants.Count == 0)
                await Assert.ThrowsAsync<ArgumentException>(() => expSvc.AddExpenseAsync(dto, aliceId));
            else
                await Assert.ThrowsAsync<ArgumentException>(() => expSvc.AddExpenseAsync(dto, aliceId));
        }
    }

    [Fact]
    public async Task Percentage_Valid_50_30_20_ShouldPass()
    {
        var (ctx, aliceId, bobId, charlieId, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        var total = 100m;
        var participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
        {
            new() { UserId = aliceId, AmountOwed = 50 },
            new() { UserId = bobId, AmountOwed = 30 },
            new() { UserId = charlieId, AmountOwed = 20 }
        };
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Pct", Amount = total, Date = DateTime.UtcNow, PaidById = aliceId, Participants = participants
        };
        var exp = await expSvc.AddExpenseAsync(dto, aliceId);
        Assert.True(exp.Id > 0);
    }

    [Fact]
    public async Task NegativeAllocation_ShouldThrow()
    {
        var (ctx, aliceId, bobId, _, gId) = await SetupAsync();
        var expSvc = new ExpensesService(ctx);
        var dto = new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Neg", Amount = 100, Date = DateTime.UtcNow, PaidById = aliceId,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = aliceId, AmountOwed = -10 },
                new() { UserId = bobId, AmountOwed = 110 }
            }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => expSvc.AddExpenseAsync(dto, aliceId));
    }
}
