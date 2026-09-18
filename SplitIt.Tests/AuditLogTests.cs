using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class AuditLogTests
{
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public int? UserId { get; set; }
        public string? IpAddress { get; set; } = "127.0.0.1";
    }

    [Fact]
    public async Task CreateAndDeleteGroup_AreAuditedWithActor()
    {
        var actor = new FakeCurrentUser();
        var ctx = TestDbHelper.CreateInMemoryContext(currentUser: actor);
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Alice", "alice@audit.com", "Pass12345!");
        var alice = await auth.GetUserByEmail("alice@audit.com");
        actor.UserId = alice!.Id;

        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("Audited", "Desc", false, 1, alice.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { alice.Id }, alice.Id);
        await groupSvc.DeleteGroupAsync(gId, alice.Id);

        var logs = await ctx.AuditLogs.Where(l => l.EntityName == "Group").ToListAsync();
        Assert.Contains(logs, l => l.Action == "create" && l.ActorUserId == alice.Id && l.EntityId == gId.ToString());
        Assert.Contains(logs, l => l.Action == "delete" && l.ActorUserId == alice.Id && l.EntityId == gId.ToString());
    }

    [Fact]
    public async Task UpdateDetails_DoNotLeakSensitiveFields()
    {
        var actor = new FakeCurrentUser();
        var ctx = TestDbHelper.CreateInMemoryContext(currentUser: actor);
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("Bob", "bob@audit.com", "Pass12345!");
        var bob = await auth.GetUserByEmail("bob@audit.com");

        bob!.Name = "Bobby";
        bob.PasswordHash = "SUPER_SECRET_HASH";
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLogs
            .Where(l => l.EntityName == "User" && l.Action == "update")
            .OrderByDescending(l => l.Id)
            .FirstAsync();

        Assert.Contains("Name", log.Details);
        Assert.DoesNotContain("PasswordHash", log.Details);
        Assert.DoesNotContain("SUPER_SECRET_HASH", log.Details);
    }

    [Fact]
    public async Task Payment_IsAudited()
    {
        var actor = new FakeCurrentUser();
        var ctx = TestDbHelper.CreateInMemoryContext(currentUser: actor);
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();

        var hasher = new PasswordHasher<User>();
        var auth = new AuthService(ctx, hasher);
        await auth.RegisterUser("A", "a@audit2.com", "Pass12345!");
        await auth.RegisterUser("B", "b@audit2.com", "Pass12345!");
        var a = await auth.GetUserByEmail("a@audit2.com");
        var b = await auth.GetUserByEmail("b@audit2.com");
        actor.UserId = b!.Id;

        var groupSvc = new GroupService(ctx);
        var gId = await groupSvc.CreateGroup("G", "D", false, 1, b.Id);
        await groupSvc.AddGroupMembers(gId, new List<int> { b.Id, a!.Id }, b.Id);

        var expSvc = new ExpensesService(ctx);
        await expSvc.AddExpenseAsync(new SplitIt.Application.DTOs.CreateExpenseDto
        {
            GroupId = gId, Title = "E", Amount = 100, Date = DateTime.UtcNow, PaidById = b.Id,
            Participants = new List<SplitIt.Application.DTOs.ExpenseParticipantDto> { new() { UserId = a.Id, AmountOwed = 100 } }
        }, b.Id);

        await expSvc.RegisterPayment(a.Id, b.Id, gId, 40);

        var expenseCreates = await ctx.AuditLogs.CountAsync(l => l.EntityName == "Expense" && l.Action == "create");
        // one real expense + one payment expense
        Assert.Equal(2, expenseCreates);
        Assert.True(await ctx.AuditLogs.AnyAsync(l => l.EntityName == "ExpenseShare" && l.Action == "update"));
    }
}
