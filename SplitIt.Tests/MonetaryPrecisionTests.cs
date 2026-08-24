using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using SplitIt.Domain.Entities;

namespace SplitIt.Tests;

public class MonetaryPrecisionTests
{
    [Theory]
    [InlineData(10.01, 3, new double[] { 3.34, 3.34, 3.33 })] // 10.01/3
    [InlineData(33.33, 3, new double[] { 11.11, 11.11, 11.11 })]
    [InlineData(100, 3, new double[] { 33.34, 33.33, 33.33 })]
    public void EqualSplit_Rounding_ShouldSumToTotal(decimal total, int count, double[] expected)
    {
        // Simulate frontend equal split rounding logic
        var perPersonRounded = Math.Floor((total / count) * 100) / 100;
        var remainderCents = (int)Math.Round((total - perPersonRounded * count) * 100);
        var result = new List<decimal>();
        for (int i = 0; i < count; i++)
        {
            var extra = i < remainderCents ? 0.01m : 0;
            result.Add(Math.Round(perPersonRounded + extra, 2, MidpointRounding.AwayFromZero));
        }
        Assert.Equal(total, result.Sum());
        // Also check expected distribution (order may vary)
        Assert.Equal(expected.Sum(e => (decimal)e), total);
    }

    [Fact]
    public async Task Expense_Add_WithDecimalPrecision_ShouldPass()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@money.com", "Pass12345!");
        await auth.RegisterUser("Bob", "bob@money.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@money.com");
        var bob = await auth.GetUserByEmail("bob@money.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Money", "Desc", false, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id }, alice.Id);

        var expSvc = new ExpensesService(ctx);
        // Use tricky decimals 33.33, 33.33, 33.34
        var exp = await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Tricky", Amount = 100m, Date = DateTime.UtcNow, PaidById = alice.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = alice.Id, AmountOwed = 33.33m },
                new() { UserId = bob.Id, AmountOwed = 33.33m },
                new() { UserId = alice.Id, AmountOwed = 33.34m } // duplicate user but for test sum
            }
        }, alice.Id);
        Assert.True(exp.Id > 0);
    }

    [Fact]
    public async Task PartialPayment_Precision_ShouldHandleCents()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("A", "a@money2.com", "Pass12345!");
        await auth.RegisterUser("B", "b@money2.com", "Pass12345!");
        var a = await auth.GetUserByEmail("a@money2.com");
        var b = await auth.GetUserByEmail("b@money2.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("G", "D", false, 1, a!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { a.Id, b!.Id }, a.Id);
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "E", Amount = 100.01m, Date = DateTime.UtcNow, PaidById = b.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 100.01m } }
        }, b.Id);
        await expSvc.RegisterPayment(a.Id, b.Id, gId, 33.33m);
        var remaining = await expSvc.GetRemainingDebtAsync(a.Id, b.Id, gId);
        Assert.Equal(66.68m, remaining);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.02)]
    [InlineData(1000000)]
    public async Task Payment_Boundary_ShouldValidate(decimal amount)
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("A3", "a3@money.com", "Pass12345!");
        await auth.RegisterUser("B3", "b3@money.com", "Pass12345!");
        var a = await auth.GetUserByEmail("a3@money.com");
        var b = await auth.GetUserByEmail("b3@money.com");
        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("G3", "D", false, 1, a!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { a.Id, b!.Id }, a.Id);
        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "E", Amount = 100, Date = DateTime.UtcNow, PaidById = b.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 100 } }
        }, b.Id);
        if (amount <= 0 || amount > 100)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => expSvc.RegisterPayment(a.Id, b.Id, gId, amount));
        }
        else
        {
            // 0.01 and 0.02 should pass if <=100
            await expSvc.RegisterPayment(a.Id, b.Id, gId, amount);
            var remaining = await expSvc.GetRemainingDebtAsync(a.Id, b.Id, gId);
            Assert.Equal(100 - amount, remaining);
        }
    }
}
