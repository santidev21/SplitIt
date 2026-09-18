namespace SplitIt.Infrastructure.Services
{
    /// <summary>
    /// Ambient access to the authenticated actor, used by the audit trail.
    /// Returns null when there is no HTTP request (tests, migrations, background work).
    /// </summary>
    public interface ICurrentUserService
    {
        int? UserId { get; }
        string? IpAddress { get; }
    }
}
