using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SplitIt.Tests.Integration;

/// <summary>
/// HTTP-level authorization regression tests: the real ASP.NET pipeline (JWT bearer, controllers,
/// services) running against a real SQL Server (Testcontainers). They cover cross-group reads and
/// writes by an outsider, and group-role restricted actions by a plain member. Every denied write
/// also asserts that the stored data did not change.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class HttpAuthorizationTests : IClassFixture<SqlServerFixture>, IDisposable
{
    // Program.cs builds the JWT validation parameters from the configuration available during
    // CreateBuilder(), before WebApplicationFactory's ConfigureAppConfiguration callback runs (and
    // appsettings.Development.json is git-ignored, so CI would get a random ephemeral key). Tokens
    // minted by TokenService can only be validated if the secret reaches both through the process
    // environment, as CorsTests does. The env vars are process-wide, hence the shared collection.
    private static readonly Dictionary<string, string> TestEnvironment = new()
    {
        ["JwtSettings__SecretKey"] = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456",
        ["JwtSettings__Issuer"] = "https://test-issuer",
        ["JwtSettings__Audience"] = "https://test-audience",
        ["Cors__AllowedOrigins"] = "http://localhost:4200"
    };

    private readonly SqlServerFixture _fixture;
    private readonly Dictionary<string, string?> _originalEnvironment =
        TestEnvironment.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
    private WebApplicationFactory<Program>? _factory;

    public HttpAuthorizationTests(SqlServerFixture fixture) => _fixture = fixture;

    public void Dispose()
    {
        _factory?.Dispose();
        foreach (var (key, value) in _originalEnvironment)
            Environment.SetEnvironmentVariable(key, value);
    }

    private sealed record Scenario(
        User Owner, User Member, User Outsider,
        int GroupId, int OutsiderGroupId,
        string OwnerToken, string MemberToken, string OutsiderToken);

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(_fixture.ConnectionString));
            });
        });

    /// <summary>
    /// Owner creates group G with Member and an expense (Owner paid, Member owes). Outsider is not
    /// in G and owns a separate group H. Tokens are minted with the app's own TokenService so the
    /// auth rate limiter is not involved.
    /// </summary>
    private async Task<Scenario> SeedAsync()
    {
        Skip.IfNot(_fixture.IsAvailable, "Docker not available - skipping SQL Server integration test (local). CI with Docker must run this test.");

        foreach (var (key, value) in TestEnvironment)
            Environment.SetEnvironmentVariable(key, value);
        _factory = CreateFactory();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        using var ctx = _fixture.CreateContext();
        if (!await ctx.Currencies.AnyAsync()) { ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" }); await ctx.SaveChangesAsync(); }

        var auth = new AuthService(ctx, new PasswordHasher<User>());
        foreach (var name in new[] { "owner", "member", "outsider" })
            await auth.RegisterUser($"Http {name}", $"http-{name}-{suffix}@authz.test", "StrongPass123!");
        var owner = (await auth.GetUserByEmail($"http-owner-{suffix}@authz.test"))!;
        var member = (await auth.GetUserByEmail($"http-member-{suffix}@authz.test"))!;
        var outsider = (await auth.GetUserByEmail($"http-outsider-{suffix}@authz.test"))!;

        var groups = new GroupService(ctx);
        var groupId = await groups.CreateGroup($"G-{suffix}", "owner group", false, 1, owner.Id);
        await groups.AddGroupMembers(groupId, new List<int> { owner.Id, member.Id }, owner.Id);
        var outsiderGroupId = await groups.CreateGroup($"H-{suffix}", "outsider group", false, 1, outsider.Id);
        await groups.AddGroupMembers(outsiderGroupId, new List<int> { outsider.Id }, outsider.Id);

        await new ExpensesService(ctx).AddExpenseAsync(new CreateExpenseDto
        {
            GroupId = groupId,
            Title = "Dinner",
            Amount = 100,
            Date = DateTime.UtcNow,
            PaidById = owner.Id,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = member.Id, AmountOwed = 100 } }
        }, owner.Id);

        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
        return new Scenario(owner, member, outsider, groupId, outsiderGroupId,
            tokens.GenerateAccessToken(owner), tokens.GenerateAccessToken(member), tokens.GenerateAccessToken(outsider));
    }

    private HttpClient ClientFor(string? token)
    {
        var client = _factory!.CreateClient();
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertDenied(HttpResponseMessage response)
    {
        Assert.False(response.IsSuccessStatusCode, $"Expected a denial but got {(int)response.StatusCode}.");
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 403/404/400 but got {(int)response.StatusCode}.");
    }

    private async Task AssertGroupUntouchedAsync(Scenario s)
    {
        using var ctx = _fixture.CreateContext();
        var group = await ctx.Groups.AsNoTracking().SingleAsync(g => g.Id == s.GroupId);
        Assert.StartsWith("G-", group.Name);
        Assert.Equal("owner group", group.Description);
        var members = await ctx.GroupMembers.AsNoTracking().Where(m => m.GroupId == s.GroupId).Select(m => m.UserId).ToListAsync();
        Assert.Equal(new[] { s.Owner.Id, s.Member.Id }.OrderBy(i => i), members.OrderBy(i => i));
        Assert.Equal(1, await ctx.Expense.CountAsync(e => e.GroupId == s.GroupId));
    }

    // ---- Unauthenticated ----

    [SkippableTheory]
    [InlineData("GET", "/api/groups/{g}/members")]
    [InlineData("GET", "/api/groups/{g}/details")]
    [InlineData("GET", "/api/expenses/{g}/expenses")]
    [InlineData("GET", "/api/expenses/debt-summary?groupId={g}")]
    [InlineData("GET", "/api/expenses/simplify-preview?groupId={g}")]
    [InlineData("POST", "/api/expenses/simplify")]
    [InlineData("POST", "/api/expenses/settle")]
    [InlineData("PUT", "/api/groups/{g}")]
    [InlineData("DELETE", "/api/groups/{g}")]
    public async Task Anonymous_IsUnauthorized(string method, string path)
    {
        var s = await SeedAsync();
        using var client = ClientFor(null);
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{g}", s.GroupId.ToString()));
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { groupId = s.GroupId });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Outsider: cross-group reads ----

    [SkippableTheory]
    [InlineData("/api/groups/{g}/members")]
    [InlineData("/api/groups/{g}/details")]
    [InlineData("/api/expenses/{g}/expenses")]
    [InlineData("/api/expenses/debt-summary?groupId={g}")]
    [InlineData("/api/expenses/simplify-preview?groupId={g}")]
    [InlineData("/api/expenses/remaining-debt?otherUserId=1&groupId={g}")]
    public async Task Outsider_CannotReadAnotherGroupsData(string path)
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var response = await client.GetAsync(path.Replace("{g}", s.GroupId.ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task Outsider_CannotListAnotherUsersGroups()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var response = await client.GetAsync($"/api/groups/user/{s.Owner.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Outsider: cross-group writes ----

    [SkippableFact]
    public async Task Outsider_CannotAddExpenseToAnotherGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var response = await client.PostAsJsonAsync("/api/expenses/add", new CreateExpenseDto
        {
            GroupId = s.GroupId,
            Title = "Injected",
            Amount = 10,
            Date = DateTime.UtcNow,
            PaidById = s.Outsider.Id,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = s.Outsider.Id, AmountOwed = 10 } }
        });

        AssertDenied(response);
        await AssertGroupUntouchedAsync(s);
    }

    [SkippableFact]
    public async Task Member_CannotAddExpenseToAnotherGroupTheyDoNotBelongTo()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.MemberToken);

        var response = await client.PostAsJsonAsync("/api/expenses/add", new CreateExpenseDto
        {
            GroupId = s.OutsiderGroupId,
            Title = "Injected",
            Amount = 10,
            Date = DateTime.UtcNow,
            PaidById = s.Member.Id,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = s.Member.Id, AmountOwed = 10 } }
        });

        AssertDenied(response);
        using var ctx = _fixture.CreateContext();
        Assert.Equal(0, await ctx.Expense.CountAsync(e => e.GroupId == s.OutsiderGroupId));
    }

    [SkippableFact]
    public async Task Outsider_CannotSettleDebtInAnotherGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var response = await client.PostAsJsonAsync("/api/expenses/settle",
            new RegisterPaymentDto { PayerUserId = s.Member.Id, GroupId = s.GroupId, Amount = 10 });

        AssertDenied(response);
        using var ctx = _fixture.CreateContext();
        Assert.Equal(0, await ctx.Expense.CountAsync(e => e.GroupId == s.GroupId && e.IsPayment));
    }

    [SkippableFact]
    public async Task Outsider_CannotApplySimplification()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var response = await client.PostAsJsonAsync("/api/expenses/simplify", new SimplifyDebtsDto { GroupId = s.GroupId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertGroupUntouchedAsync(s);
    }

    [SkippableFact]
    public async Task Outsider_CannotUpdateOrDeleteAnotherGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var update = await client.PutAsJsonAsync($"/api/groups/{s.GroupId}",
            new UpdateGroupDto { Name = "Hijacked", Description = "hijacked", AllowToDeleteExpenses = true });
        var delete = await client.DeleteAsync($"/api/groups/{s.GroupId}");

        AssertDenied(update);
        AssertDenied(delete);
        await AssertGroupUntouchedAsync(s);
    }

    [SkippableFact]
    public async Task Outsider_CannotManageMembersOfAnotherGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OutsiderToken);

        var invite = await client.PostAsJsonAsync($"/api/groups/{s.GroupId}/members", new InviteMemberDto { UserId = s.Outsider.Id });
        var promote = await client.PutAsJsonAsync($"/api/groups/{s.GroupId}/members/{s.Outsider.Id}/role", new UpdateGroupMemberRoleDto { Role = "admin" });
        var remove = await client.DeleteAsync($"/api/groups/{s.GroupId}/members/{s.Member.Id}");

        AssertDenied(invite);
        AssertDenied(promote);
        AssertDenied(remove);
        await AssertGroupUntouchedAsync(s);
    }

    // ---- Plain member: forbidden group-role actions ----

    [SkippableFact]
    public async Task Member_CannotUpdateOrDeleteGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.MemberToken);

        var update = await client.PutAsJsonAsync($"/api/groups/{s.GroupId}",
            new UpdateGroupDto { Name = "Renamed", Description = "renamed", AllowToDeleteExpenses = true });
        var delete = await client.DeleteAsync($"/api/groups/{s.GroupId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        await AssertGroupUntouchedAsync(s);
    }

    [SkippableFact]
    public async Task Member_CannotPromoteSelfOrRemoveTheOwner()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.MemberToken);

        var promote = await client.PutAsJsonAsync($"/api/groups/{s.GroupId}/members/{s.Member.Id}/role", new UpdateGroupMemberRoleDto { Role = "admin" });
        var removeOwner = await client.DeleteAsync($"/api/groups/{s.GroupId}/members/{s.Owner.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removeOwner.StatusCode);
        await AssertGroupUntouchedAsync(s);
        using var ctx = _fixture.CreateContext();
        Assert.NotEqual("admin", (await ctx.GroupMembers.AsNoTracking().SingleAsync(m => m.GroupId == s.GroupId && m.UserId == s.Member.Id)).Role);
    }

    [SkippableFact]
    public async Task Member_CannotApplySimplification()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.MemberToken);

        var response = await client.PostAsJsonAsync("/api/expenses/simplify", new SimplifyDebtsDto { GroupId = s.GroupId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertGroupUntouchedAsync(s);
    }

    // ---- Positive controls (the denials above are meaningful only if legitimate access works) ----

    [SkippableTheory]
    [InlineData("/api/groups/{g}/members")]
    [InlineData("/api/groups/{g}/details")]
    [InlineData("/api/expenses/{g}/expenses")]
    [InlineData("/api/expenses/debt-summary?groupId={g}")]
    [InlineData("/api/expenses/simplify-preview?groupId={g}")]
    public async Task Members_CanReadTheirOwnGroup(string path)
    {
        var s = await SeedAsync();
        var url = path.Replace("{g}", s.GroupId.ToString());

        using var owner = ClientFor(s.OwnerToken);
        using var member = ClientFor(s.MemberToken);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync(url)).StatusCode);
    }

    [SkippableFact]
    public async Task Owner_CanUpdateGroup()
    {
        var s = await SeedAsync();
        using var client = ClientFor(s.OwnerToken);

        var response = await client.PutAsJsonAsync($"/api/groups/{s.GroupId}",
            new UpdateGroupDto { Name = "Owner renamed", Description = "owner group", AllowToDeleteExpenses = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
