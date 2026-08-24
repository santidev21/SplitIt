using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class AppAdminTests
{
    private async Task<(AppDbContext ctx, int superId, int adminId, int userId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        var superUser = new User { Name = "Super", Email = "super@app.com", PasswordHash = "h", RoleId = 1 };
        var admin = new User { Name = "Admin", Email = "admin@app.com", PasswordHash = "h", RoleId = 2 };
        var user = new User { Name = "User", Email = "user@app.com", PasswordHash = "h", RoleId = 3 };
        ctx.Users.AddRange(superUser, admin, user);
        await ctx.SaveChangesAsync();
        return (ctx, superUser.Id, admin.Id, user.Id);
    }

    [Fact]
    public async Task IsUserAdmin_ShouldReturnCorrect()
    {
        var (ctx, superId, adminId, userId) = await SetupAsync();
        var svc = new UsersService(ctx);
        Assert.True(await svc.IsUserAdminAsync(superId));
        Assert.True(await svc.IsUserAdminAsync(adminId));
        Assert.False(await svc.IsUserAdminAsync(userId));
    }

    [Fact]
    public async Task SuperCanPromoteUserToAdmin()
    {
        var (ctx, superId, _, userId) = await SetupAsync();
        var svc = new UsersService(ctx);
        await svc.UpdateUserRoleAsync(userId, 2, superId);
        var updated = await ctx.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal(2, updated.RoleId);
    }

    [Fact]
    public async Task AdminCannotPromote()
    {
        var (ctx, _, adminId, userId) = await SetupAsync();
        var svc = new UsersService(ctx);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.UpdateUserRoleAsync(userId, 2, adminId));
    }

    [Fact]
    public async Task UserCannotPromote()
    {
        var (ctx, _, _, userId) = await SetupAsync();
        var svc = new UsersService(ctx);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.UpdateUserRoleAsync(userId, 2, userId));
    }

    [Fact]
    public async Task CannotChangeOwnRole()
    {
        var (ctx, superId, _, _) = await SetupAsync();
        var svc = new UsersService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.UpdateUserRoleAsync(superId, 2, superId));
    }

    [Fact]
    public async Task InvalidRole_ShouldThrow()
    {
        var (ctx, superId, _, userId) = await SetupAsync();
        var svc = new UsersService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.UpdateUserRoleAsync(userId, 99, superId));
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnAll()
    {
        var (ctx, _, _, _) = await SetupAsync();
        var svc = new UsersService(ctx);
        var all = await svc.GetAllUsersAsync();
        Assert.Equal(3, all.Count);
    }
}
