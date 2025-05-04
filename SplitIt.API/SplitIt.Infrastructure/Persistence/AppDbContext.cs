using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<Expense> Expense{get; set;}
        public DbSet<ExpenseShare> ExpenseShare{get; set;}

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
            });

            // GroupMember table configuration
            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(gm => gm.Id);
                entity.HasOne(gm => gm.Group).WithMany(g => g.GroupMembers).HasForeignKey(gm => gm.GroupId);
                entity.HasOne(gm => gm.User).WithMany().HasForeignKey(gm => gm.UserId);
                entity.Property(gm => gm.Role).IsRequired().HasMaxLength(50);
            });

            // Expense table configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Date).HasColumnType("date");
                entity.HasOne(e => e.Group).WithMany(g => g.Expenses).HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaidBy).WithMany().HasForeignKey(e => e.PaidById).OnDelete(DeleteBehavior.Restrict);
            });

            // Expense Share table configuration
            modelBuilder.Entity<ExpenseShare>(entity =>
            {
                entity.HasKey(es => es.Id);entity.Property(es => es.AmountOwed).HasColumnType("decimal(18,2)");
                entity.HasOne(es => es.Expense).WithMany(e => e.Shares).HasForeignKey(es => es.ExpenseId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(es => es.User).WithMany().HasForeignKey(es => es.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(es => es.IsSettled).IsRequired().HasDefaultValue(false);
                entity.Property(es => es.SettledAt).HasColumnType("datetime");
            });

            SeedRoles(modelBuilder);
            SeedCurrency(modelBuilder);
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
    }
}
