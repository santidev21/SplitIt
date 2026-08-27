using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using SplitIt.Tests.Helpers;

namespace SplitIt.Tests;

public class FriendshipTests
{
    private async Task<(AppDbContext ctx, FriendshipsService svc, int aliceId, int bobId, int charlieId)> SetupAsync()
    {
        var ctx = TestDbHelper.CreateInMemoryContext();
        ctx.Users.AddRange(
            new User { Name = "Alice", Email = "alice@f.com", PasswordHash = "h", RoleId = 3 },
            new User { Name = "Bob", Email = "bob@f.com", PasswordHash = "h", RoleId = 3 },
            new User { Name = "Charlie", Email = "charlie@f.com", PasswordHash = "h", RoleId = 3 });
        await ctx.SaveChangesAsync();
        var ids = await ctx.Users.Select(u => u.Id).ToListAsync();
        var svc = new FriendshipsService(ctx);
        return (ctx, svc, ids[0], ids[1], ids[2]);
    }

    [Fact]
    public async Task SendRequest_CreatesPendingRequest()
    {
        var (ctx, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);

        var incoming = await svc.GetIncomingRequestsAsync(bob);
        Assert.Single(incoming);
        Assert.Equal(alice, incoming[0].UserId);

        var sent = await svc.GetSentRequestsAsync(alice);
        Assert.Single(sent);
    }

    [Fact]
    public async Task SendRequest_ByEmail_Works()
    {
        var (_, svc, alice, _, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, null, "bob@f.com");

        var sent = await svc.GetSentRequestsAsync(alice);
        Assert.Single(sent);
    }

    [Fact]
    public async Task SendRequest_ToSelf_Throws()
    {
        var (_, svc, alice, _, _) = await SetupAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendRequestAsync(alice, alice, null));
    }

    [Fact]
    public async Task SendRequest_UnknownUser_Throws()
    {
        var (_, svc, alice, _, _) = await SetupAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.SendRequestAsync(alice, 99999, null));
    }

    [Fact]
    public async Task SendRequest_DuplicatePending_Throws()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendRequestAsync(alice, bob, null));
    }

    [Fact]
    public async Task SendRequest_ReversePending_AutoAccepts()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        // Bob tries to add Alice: her pending request should be accepted instead
        await svc.SendRequestAsync(bob, alice, null);

        var aliceFriends = await svc.GetFriendsAsync(alice);
        var bobFriends = await svc.GetFriendsAsync(bob);
        Assert.Single(aliceFriends);
        Assert.Single(bobFriends);
        Assert.Equal(bob, aliceFriends[0].Id);
        Assert.Equal(alice, bobFriends[0].Id);
    }

    [Fact]
    public async Task Accept_AddsBothToFriends()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        var request = (await svc.GetIncomingRequestsAsync(bob))[0];
        await svc.RespondAsync(request.FriendshipId, bob, accept: true);

        Assert.Single(await svc.GetFriendsAsync(alice));
        Assert.Single(await svc.GetFriendsAsync(bob));
    }

    [Fact]
    public async Task Accept_ByNonRecipient_Throws()
    {
        var (_, svc, alice, bob, charlie) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        var request = (await svc.GetIncomingRequestsAsync(bob))[0];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.RespondAsync(request.FriendshipId, charlie, accept: true));
    }

    [Fact]
    public async Task Reject_RemovesRequest_AndAllowsResending()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        var request = (await svc.GetIncomingRequestsAsync(bob))[0];
        await svc.RespondAsync(request.FriendshipId, bob, accept: false);

        Assert.Empty(await svc.GetFriendsAsync(bob));
        Assert.Empty(await svc.GetIncomingRequestsAsync(bob));

        // Alice can send a new request after rejection
        await svc.SendRequestAsync(alice, bob, null);
        Assert.Single(await svc.GetIncomingRequestsAsync(bob));
    }

    [Fact]
    public async Task RemoveFriend_DeletesFriendship()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        var request = (await svc.GetIncomingRequestsAsync(bob))[0];
        await svc.RespondAsync(request.FriendshipId, bob, accept: true);

        await svc.RemoveFriendAsync(alice, bob);
        Assert.Empty(await svc.GetFriendsAsync(alice));
        Assert.Empty(await svc.GetFriendsAsync(bob));
    }

    [Fact]
    public async Task RemoveFriend_NotFriends_Throws()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.RemoveFriendAsync(alice, bob));
    }

    [Fact]
    public async Task Search_ExcludesSelf_AndExistingRelations()
    {
        var (_, svc, alice, bob, charlie) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null); // pending counts as related

        var results = await svc.SearchUsersAsync("f.com", alice); // matches by email domain
        Assert.DoesNotContain(results, r => r.Id == alice);
        Assert.DoesNotContain(results, r => r.Id == bob);
        Assert.Contains(results, r => r.Id == charlie);
    }

    [Fact]
    public async Task Search_ShortTerm_Throws()
    {
        var (_, svc, alice, _, _) = await SetupAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SearchUsersAsync("a", alice));
    }

    [Fact]
    public async Task AreFriends_TrueOnlyForAccepted()
    {
        var (_, svc, alice, bob, _) = await SetupAsync();
        await svc.SendRequestAsync(alice, bob, null);
        Assert.False(await svc.AreFriendsAsync(alice, bob));

        var request = (await svc.GetIncomingRequestsAsync(bob))[0];
        await svc.RespondAsync(request.FriendshipId, bob, accept: true);
        Assert.True(await svc.AreFriendsAsync(alice, bob));
        Assert.True(await svc.AreFriendsAsync(bob, alice));
    }
}
