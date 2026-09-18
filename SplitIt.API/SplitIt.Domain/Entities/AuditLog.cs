namespace SplitIt.Domain.Entities
{
    /// <summary>
    /// Append-only audit trail (Ley 1581 traceability): who did what to which entity and when.
    /// Never store secrets or raw tokens here.
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }

        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;

        /// <summary>create | update | delete</summary>
        public string Action { get; set; } = string.Empty;

        public int? ActorUserId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>JSON with the changed fields (sensitive fields are excluded).</summary>
        public string? Details { get; set; }
    }
}
