using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CMCSWeb.Models;

namespace CMCSWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Claim> Claims { get; set; }

        // Add these new DbSets
        public DbSet<ApprovalWorkflow> ApprovalWorkflows { get; set; }
        public DbSet<WorkflowHistory> WorkflowHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Claim relationships
            builder.Entity<Claim>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ApprovalWorkflow relationships
            builder.Entity<ApprovalWorkflow>()
                .HasOne(w => w.Claim)
                .WithMany()
                .HasForeignKey(w => w.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure WorkflowHistory relationships
            builder.Entity<WorkflowHistory>()
                .HasOne(h => h.ApprovalWorkflow)
                .WithMany(w => w.History)
                .HasForeignKey(h => h.ApprovalWorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            // Optional: Add indexes for better performance
            builder.Entity<ApprovalWorkflow>()
                .HasIndex(w => w.ClaimId)
                .IsUnique(); // One workflow per claim

            builder.Entity<WorkflowHistory>()
                .HasIndex(h => h.ApprovalWorkflowId);

            builder.Entity<WorkflowHistory>()
                .HasIndex(h => h.ActionDate);
        }
    }
}