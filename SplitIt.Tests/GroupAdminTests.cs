using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class GroupAdminTests
{
    private async Task<(AppDbContext ctx, int creatorId, int adminId, int memberId, int groupId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Currencies.Add(new Currency { Id = 1, Name = "USD", Symbol = "$" });
        await ctx.SaveChangesAsync();
        var creator = new User { Name = "Creator", Email = "creator@group.com", PasswordHash = "h", RoleId = 3 };
        var admin = new User { Name = "Admin", Email = "admin@group.com", PasswordHash = "h", RoleId = 3 };
        var member = new User { Name = "Member", Email = "member@group.com", PasswordHash = "h", RoleId = 3 };
        ctx.Users.AddRange(creator, admin, member);
        await ctx.SaveChangesAsync();
        var svc = new GroupService(ctx);
        var gId = await svc.CreateGroup("Group", "Desc", false, 1, creator.Id);
        await svc.AddGroupMembers(gId, new List<int> { creator.Id, admin.Id, member.Id }, creator.Id);
        // Promote admin to admin role for setup
        await svc.UpdateMemberRoleAsync(gId, admin.Id, "admin", creator.Id);
        return (ctx, creator.Id, admin.Id, member.Id, gId);
    }

    [Fact]
    public async Task CreatorCanPromoteMemberToAdmin()
    {
        var (ctx, creatorId, _, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await svc.UpdateMemberRoleAsync(gId, memberId, "admin", creatorId);
        Assert.Equal("admin", await svc.GetUserGroupRoleAsync(gId, memberId));
    }

    [Fact]
    public async Task AdminCannotPromoteToAdmin()
    {
        var (ctx, _, adminId, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.UpdateMemberRoleAsync(gId, memberId, "admin", adminId));
    }

    [Fact]
    public async Task MemberCannotPromote()
    {
        var (ctx, _, _, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        // member trying to promote himself
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.UpdateMemberRoleAsync(gId, memberId, "admin", memberId));
    }

    [Fact]
    public async Task CannotPromoteCreator()
    {
        var (ctx, creatorId, adminId, _, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.UpdateMemberRoleAsync(gId, creatorId, "member", adminId));
    }

    [Fact]
    public async Task CreatorCanRemoveMember()
    {
        var (ctx, creatorId, _, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await svc.RemoveMemberAsync(gId, memberId, creatorId);
        Assert.False(await svc.IsUserMemberAsync(gId, memberId));
    }

    [Fact]
    public async Task AdminCanRemoveMemberButNotAdmin()
    {
        var (ctx, _, adminId, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await svc.RemoveMemberAsync(gId, memberId, adminId);
        Assert.False(await svc.IsUserMemberAsync(gId, memberId));

        // Admin trying to remove creator should fail
        var (ctx2, creatorId2, adminId2, _, gId2) = await SetupAsync();
        var svc2 = new GroupService(ctx2);
        await Assert.ThrowsAsync<ArgumentException>(() => svc2.RemoveMemberAsync(gId2, creatorId2, adminId2));
    }

    [Fact]
    public async Task MemberCannotRemove()
    {
        var (ctx, _, _, memberId, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        // member trying to remove admin
        var adminId = (await ctx.GroupMembers.FirstAsync(gm => gm.GroupId == gId && gm.Role == "admin")).UserId;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RemoveMemberAsync(gId, adminId, memberId));
    }

    [Fact]
    public async Task OnlyCreatorCanDeleteGroup()
    {
        var (ctx, creatorId, adminId, _, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteGroupAsync(gId, adminId));
        await svc.DeleteGroupAsync(gId, creatorId);
        Assert.False(await ctx.Groups.AnyAsync(g => g.Id == gId));
    }

    [Fact]
    public async Task CannotChangeOwnRole()
    {
        var (ctx, creatorId, _, _, gId) = await SetupAsync();
        var svc = new GroupService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.UpdateMemberRoleAsync(gId, creatorId, "member", creatorId));
    }
}
