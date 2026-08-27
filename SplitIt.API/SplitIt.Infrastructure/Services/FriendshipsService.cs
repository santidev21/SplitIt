using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Infrastructure.Services
{
    public class FriendshipsService
    {
        public const string StatusPending = "pending";
        public const string StatusAccepted = "accepted";

        private readonly AppDbContext _context;

        public FriendshipsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task SendRequestAsync(int requesterId, int? targetUserId, string? targetEmail)
        {
            User? target;
            if (targetUserId.HasValue)
            {
                target = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(targetEmail))
            {
                var normalized = targetEmail.Trim().ToLowerInvariant();
                target = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
            }
            else
            {
                throw new ArgumentException("Provide a user id or an email to send a friend request.");
            }

            if (target == null)
                throw new KeyNotFoundException("User not found.");
            if (target.Id == requesterId)
                throw new ArgumentException("You cannot add yourself as a friend.");

            var existing = await GetBetweenAsync(requesterId, target.Id);
            if (existing != null)
            {
                if (existing.Status == StatusAccepted)
                    throw new ArgumentException($"You are already friends with {target.Name}.");

                if (existing.RequesterId == requesterId)
                    throw new ArgumentException("You already have a pending request to this user.");

                // The other user had already requested: accepting their request instead.
                existing.Status = StatusAccepted;
                existing.RespondedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }

            _context.Friendships.Add(new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = target.Id,
                Status = StatusPending,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task RespondAsync(int friendshipId, int userId, bool accept)
        {
            var friendship = await _context.Friendships.FirstOrDefaultAsync(f => f.Id == friendshipId);
            if (friendship == null)
                throw new KeyNotFoundException("Friend request not found.");
            if (friendship.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the recipient can respond to this request.");
            if (friendship.Status != StatusPending)
                throw new ArgumentException("This request was already handled.");

            if (accept)
            {
                friendship.Status = StatusAccepted;
                friendship.RespondedAt = DateTime.UtcNow;
            }
            else
            {
                // Rejected requests are removed so the requester can try again later.
                _context.Friendships.Remove(friendship);
            }
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFriendAsync(int userId, int friendUserId)
        {
            var friendship = await GetBetweenAsync(userId, friendUserId);
            if (friendship == null || friendship.Status != StatusAccepted)
                throw new KeyNotFoundException("You are not friends with this user.");

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
        }

        public async Task<List<FriendDto>> GetFriendsAsync(int userId)
        {
            var friendIds = await _context.Friendships
                .Where(f => f.Status == StatusAccepted && (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            return await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .Select(u => new FriendDto { Id = u.Id, Name = u.Name, Email = u.Email })
                .ToListAsync();
        }

        public async Task<List<FriendRequestDto>> GetIncomingRequestsAsync(int userId)
        {
            return await _context.Friendships
                .Where(f => f.Status == StatusPending && f.AddresseeId == userId)
                .OrderBy(f => f.CreatedAt)
                .Select(f => new FriendRequestDto
                {
                    FriendshipId = f.Id,
                    UserId = f.RequesterId,
                    Name = f.Requester.Name,
                    Email = f.Requester.Email,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<FriendRequestDto>> GetSentRequestsAsync(int userId)
        {
            return await _context.Friendships
                .Where(f => f.Status == StatusPending && f.RequesterId == userId)
                .OrderBy(f => f.CreatedAt)
                .Select(f => new FriendRequestDto
                {
                    FriendshipId = f.Id,
                    UserId = f.AddresseeId,
                    Name = f.Addressee.Name,
                    Email = f.Addressee.Email,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Searches users by name or email (case-insensitive contains), excluding the
        /// current user, existing friends and users with pending requests.
        /// </summary>
        public async Task<List<SearchUserDto>> SearchUsersAsync(string query, int currentUserId)
        {
            var term = (query ?? string.Empty).Trim().ToLowerInvariant();
            if (term.Length < 2)
                throw new ArgumentException("Search term must be at least 2 characters.");

            var relatedUserIds = await _context.Friendships
                .Where(f => f.RequesterId == currentUserId || f.AddresseeId == currentUserId)
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            return await _context.Users
                .Where(u => u.Id != currentUserId
                            && !relatedUserIds.Contains(u.Id)
                            && (u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term)))
                .OrderBy(u => u.Name)
                .Take(20)
                .Select(u => new SearchUserDto { Id = u.Id, Name = u.Name, Email = u.Email })
                .ToListAsync();
        }

        public async Task<bool> AreFriendsAsync(int userId, int otherUserId)
        {
            return await _context.Friendships.AnyAsync(f =>
                f.Status == StatusAccepted &&
                ((f.RequesterId == userId && f.AddresseeId == otherUserId) ||
                 (f.RequesterId == otherUserId && f.AddresseeId == userId)));
        }

        private Task<Friendship?> GetBetweenAsync(int userA, int userB)
        {
            return _context.Friendships.FirstOrDefaultAsync(f =>
                (f.RequesterId == userA && f.AddresseeId == userB) ||
                (f.RequesterId == userB && f.AddresseeId == userA));
        }
    }
}
