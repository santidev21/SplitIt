using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using Testcontainers.MsSql;

namespace SplitIt.Tests.Integration;

/// <summary>
/// Integration tests against real SQL Server (Testcontainers).
/// These run with InMemory fallback if Docker unavailable (skipped).
/// Covers full workflow: register → group → expense → balances → settle → cross-group isolation.
/// </summary>
public class ExpenseWorkflowIntegrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public ExpenseWorkflowIntegrationTests(SqlServerFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task FullWorkflow_RealSqlServer_ShouldWork()
    {
        Skip.IfNot(_fixture.IsAvailable, "Docker not available - skipping SQL Server integration test (local). CI with Docker must run this test; if Docker+SQL unavailable it should FAIL, not skip.");


        using var ctx = _fixture.CreateContext();
        // Ensure clean DB for this test (new database per fixture, but truncate for isolation)
        // Using new fixture per test class gives same container but different data — we clean relevant tables
        await CleanAsync(ctx);

        // Seed currency
        if (!await ctx.Currencies.AnyAsync()) { ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" }); await ctx.SaveChangesAsync(); }

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@int.com", "StrongPass123!");
        await auth.RegisterUser("Bob", "bob@int.com", "StrongPass123!");
        var alice = await auth.GetUserByEmail("alice@int.com");
        var bob = await auth.GetUserByEmail("bob@int.com");
        Assert.NotNull(alice); Assert.NotNull(bob);

        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Integration Trip", "Desc", false, 1, alice!.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id, bob!.Id }, alice.Id);

        var expSvc = new ExpensesService(ctx);
        var expense = await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "Dinner", Amount = 100, Date = DateTime.UtcNow, PaidById = alice.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto>
            {
                new() { UserId = alice.Id, AmountOwed = 50 },
                new() { UserId = bob.Id, AmountOwed = 50 }
            }
        }, alice.Id);

        Assert.True(expense.Id > 0);

        var summary = await expSvc.GetFullDebtSummaryAsync(bob.Id, gId);
        // Bob owes Alice 50
        Assert.Single(summary.DebtsOwedByUser);
        Assert.Equal(50, summary.DebtsOwedByUser[0].TotalAmountOwed);

        var expenses = await expSvc.GetExpensesByGroupIdAsync(gId, alice.Id, true);
        Assert.Single(expenses);
    }

    [SkippableFact]
    public async Task CrossGroup_Isolation_RealSqlServer()
    {
        Skip.IfNot(_fixture.IsAvailable, "Docker not available - skipping SQL Server integration test");

        using var ctx = _fixture.CreateContext();
        await CleanAsync(ctx);
        if (!await ctx.Currencies.AnyAsync()) { ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" }); await ctx.SaveChangesAsync(); }

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("AInt2", "aint2@test.com", "Pass12345!");
        await auth.RegisterUser("BInt2", "bint2@test.com", "Pass12345!");
        var a = await auth.GetUserByEmail("aint2@test.com");
        var b = await auth.GetUserByEmail("bint2@test.com");

        var groupSvc = new GroupService(ctx);
        var gA = await groupSvc.CreateGroup("GA", "d", false, 1, a!.Id);
        await groupSvc.AddGroupMembers(gA, new List<int> { a.Id, b!.Id }, a.Id);
        var gB = await groupSvc.CreateGroup("GB", "d", false, 1, a.Id);
        await groupSvc.AddGroupMembers(gB, new List<int> { a.Id, b.Id }, a.Id);

        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gA, Title = "EA", Amount = 100, Date = DateTime.UtcNow, PaidById = b.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 100 } }
        }, b.Id);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gB, Title = "EB", Amount = 50, Date = DateTime.UtcNow, PaidById = b.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 50 } }
        }, b.Id);

        var settledA = await expSvc.SettleExpenseWithUser(b.Id, a.Id, gA);
        Assert.Equal(1, settledA);
        var unsettledB = await ctx.ExpenseShare.Include(es => es.Expense).CountAsync(es => !es.IsSettled && es.Expense.GroupId == gB);
        Assert.Equal(1, unsettledB);
    }

    private static async Task CleanAsync(SplitIt.Infrastructure.Persistence.AppDbContext ctx)
    {
        // Truncate in FK order
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM ExpenseShare; DELETE FROM Expense; DELETE FROM GroupMembers; DELETE FROM Groups; DELETE FROM Users WHERE Email LIKE '%@int.com' OR Email LIKE '%aint2%' OR Email LIKE '%bint2%';");
        await ctx.SaveChangesAsync();
    }
}
