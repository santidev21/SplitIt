using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        // Fields that must never be written to the audit trail.
        private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "Password", "Token", "TokenHash", "ReplacedByTokenHash", "SecretKey"
        };

        private readonly ICurrentUserService? _currentUser;

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null) : base(options)
        {
            _currentUser = currentUser;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Expense> Expense{get; set;}
        public DbSet<ExpenseShare> ExpenseShare{get; set;}
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        /// <summary>
        /// Audits every tracked change (create/update/delete) for the domain entities.
        /// The audit rows are persisted in a second save after the main change, so the
        /// generated keys are available and sensitive fields are never serialized.
        /// </summary>
        public override int SaveChanges()
        {
            var audit = CaptureAudit();
            var result = base.SaveChanges();
            EmitAudit(audit);
            return result;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var audit = CaptureAudit();
            var result = await base.SaveChangesAsync(cancellationToken);
            await EmitAuditAsync(audit, cancellationToken);
            return result;
        }

        private void EmitAudit(List<AuditDescriptor> audit)
        {
            if (audit.Count == 0) return;
            AddAuditRows(audit);
            base.SaveChanges();
        }

        private async Task EmitAuditAsync(List<AuditDescriptor> audit, CancellationToken cancellationToken)
        {
            if (audit.Count == 0) return;
            AddAuditRows(audit);
            await base.SaveChangesAsync(cancellationToken);
        }

        private void AddAuditRows(List<AuditDescriptor> audit)
        {
            var actorId = _currentUser?.UserId;
            var ip = _currentUser?.IpAddress;
            foreach (var item in audit)
            {
                AuditLogs.Add(new AuditLog
                {
                    EntityName = item.EntityName,
                    EntityId = item.ResolveId(),
                    Action = item.Action,
                    ActorUserId = actorId,
                    IpAddress = ip,
                    Timestamp = DateTime.UtcNow,
                    Details = item.Details
                });
            }
        }

        private List<AuditDescriptor> CaptureAudit()
        {
            var audit = new List<AuditDescriptor>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified && entry.State != EntityState.Deleted)
                    continue;
                if (entry.Entity is AuditLog)
                    continue;

                var action = entry.State switch
                {
                    EntityState.Added => "create",
                    EntityState.Deleted => "delete",
                    _ => ResolveModifiedAction(entry)
                };

                var descriptor = new AuditDescriptor
                {
                    EntityName = entry.Metadata.ClrType.Name,
                    Action = action
                };

                if (entry.State == EntityState.Deleted)
                {
                    descriptor.DeletedKey = entry.Properties
                        .Where(p => p.Metadata.IsPrimaryKey())
                        .Select(p => p.OriginalValue?.ToString())
                        .FirstOrDefault();
                }
                else
                {
                    descriptor.Entry = entry;
                    if (entry.State == EntityState.Modified)
                        descriptor.Details = BuildModifiedDetails(entry);
                }

                audit.Add(descriptor);
            }
            return audit;
        }

        private static string ResolveModifiedAction(EntityEntry entry)
        {
            // Soft deletes are modifications of IsDeleted; surface them as delete/restore.
            var isDeleted = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            if (isDeleted != null && isDeleted.IsModified)
                return (bool?)isDeleted.CurrentValue == true ? "delete" : "restore";
            return "update";
        }

        private static string? BuildModifiedDetails(EntityEntry entry)
        {
            var changed = new Dictionary<string, object?>();
            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified) continue;
                if (SensitiveProperties.Contains(prop.Metadata.Name)) continue;
                changed[prop.Metadata.Name] = new { from = prop.OriginalValue, to = prop.CurrentValue };
            }
            if (changed.Count == 0) return null;
            try
            {
                return JsonSerializer.Serialize(changed);
            }
            catch
            {
                return null;
            }
        }

        private sealed class AuditDescriptor
        {
            public string EntityName { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string? Details { get; set; }
            public EntityEntry? Entry { get; set; }
            public string? DeletedKey { get; set; }

            public string ResolveId()
            {
                if (Entry != null)
                {
                    var key = Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (key?.CurrentValue != null)
                        return key.CurrentValue.ToString() ?? string.Empty;
                }
                return DeletedKey ?? string.Empty;
            }
        }

protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(u => u.ConsentVersion).HasMaxLength(20);
                entity.Property(u => u.ConsentIp).HasMaxLength(64);
                entity.Property(u => u.DeletedAt).HasColumnType("datetime2");
            });

            // Role table configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            });

            // Currency table configuration
            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100); 
                entity.Property(c => c.Symbol).IsRequired().HasMaxLength(10);
            });

            // Group table configuration
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Name).IsRequired().HasMaxLength(200);
                entity.Property(g => g.Description).IsRequired().HasMaxLength(500);
                entity.Property(g => g.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(g => g.Currency).WithMany().HasForeignKey(g => g.CurrencyId);
                entity.Property(g => g.AllowToDeleteExpenses).HasDefaultValue(false);
                entity.Property(g => g.IsDeleted).IsRequired().HasDefaultValue(false);
                entity.Property(g => g.DeletedAt).HasColumnType("datetime2");
                // Soft-deleted groups are hidden from every query by default.
                entity.HasQueryFilter(g => !g.IsDeleted);
                entity.Property(g => g.RowVersion).IsRowVersion();
            });

            // GroupMember table configuration
            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(gm => gm.Id);
                entity.HasOne(gm => gm.Group).WithMany(g => g.GroupMembers).HasForeignKey(gm => gm.GroupId);
                entity.HasOne(gm => gm.User).WithMany().HasForeignKey(gm => gm.UserId);
                entity.Property(gm => gm.Role).IsRequired().HasMaxLength(50);
                // A user can only be a member of a group once.
                entity.HasIndex(gm => new { gm.GroupId, gm.UserId }).IsUnique();
                // Hide memberships of soft-deleted groups.
                entity.HasQueryFilter(gm => !gm.Group!.IsDeleted);
            });

            // Expense table configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Date).HasColumnType("datetime2");
                entity.HasOne(e => e.Group).WithMany(g => g.Expenses).HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaidBy).WithMany().HasForeignKey(e => e.PaidById).OnDelete(DeleteBehavior.Restrict);
                // Hide expenses that belong to a soft-deleted group.
                entity.HasQueryFilter(e => !e.Group!.IsDeleted);
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            // Expense Share table configuration
            modelBuilder.Entity<ExpenseShare>(entity =>
            {
                entity.HasKey(es => es.Id);entity.Property(es => es.AmountOwed).HasColumnType("decimal(18,2)");
                entity.Property(es => es.AmountPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.HasOne(es => es.Expense).WithMany(e => e.Shares).HasForeignKey(es => es.ExpenseId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(es => es.User).WithMany().HasForeignKey(es => es.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(es => es.IsSettled).IsRequired().HasDefaultValue(false);
                entity.Property(es => es.SettledAt).HasColumnType("datetime2");
                // Hide shares that belong to a soft-deleted group's expense.
                entity.HasQueryFilter(es => !es.Expense!.Group!.IsDeleted);
                entity.Property(es => es.RowVersion).IsRowVersion();
            });

            // Friendship table configuration
            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasKey(f => f.Id);
                // Normalized ordering prevents duplicate reverse requests
                entity.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
                entity.Property(f => f.Status).IsRequired().HasMaxLength(20);
                entity.Property(f => f.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(f => f.RespondedAt).HasColumnType("datetime2");
                entity.HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Addressee).WithMany().HasForeignKey(f => f.AddresseeId).OnDelete(DeleteBehavior.Restrict);
            });

            // AppSetting table configuration
            modelBuilder.Entity<AppSetting>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.Key).IsUnique();
                entity.Property(s => s.Key).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Value).IsRequired().HasMaxLength(500);
            });

            // PasswordResetToken table configuration
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Token).IsRequired().HasMaxLength(64);
                entity.Property(p => p.ExpiresAt).IsRequired();
                entity.Property(p => p.Used).IsRequired().HasDefaultValue(false);
                entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => new { p.UserId, p.Token });
            });

            // RefreshToken table configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.TokenHash).IsUnique();
                entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(64);
                entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // AuditLog table configuration (append-only traceability)
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityId).IsRequired().HasMaxLength(64);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(20);
                entity.Property(a => a.IpAddress).HasMaxLength(64);
                entity.Property(a => a.Details).HasColumnType("nvarchar(max)");
                entity.Property(a => a.Timestamp).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(a => new { a.EntityName, a.EntityId });
                entity.HasIndex(a => a.Timestamp);
            });

            SeedRoles(modelBuilder);
            SeedCurrency(modelBuilder);
            SeedSettings(modelBuilder);
        }

        private static void SeedRoles(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "super" },
                new Role { Id = 2, Name = "admin" },
                new Role { Id = 3, Name = "user" }
            );
        }

        private static void SeedCurrency(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Currency>().HasData(
                new Currency { Id = 1, Name = "Dólar", Symbol = "USD" },
                new Currency { Id = 2, Name = "Peso Colombiano", Symbol = "COP" }
            );
        }

        private static void SeedSettings(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppSetting>().HasData(
                new AppSetting { Id = 1, Key = "RegistrationEnabled", Value = "true" },
                new AppSetting { Id = 2, Key = "MaxExpenseAmount", Value = "1000000" }
            );
        }
    }
}
