using Ctf4e.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Data
{
    /// <summary>
    /// Database context.
    /// </summary>
    public class CtfDbContext : DbContext
    {
        public CtfDbContext(DbContextOptions<CtfDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserEntity> Users { get; set; }

        public DbSet<GroupEntity> Groups { get; set; }

        public DbSet<LabEntity> Labs { get; set; }

        public DbSet<SlotEntity> Slots { get; set; }

        public DbSet<LabExecutionEntity> LabExecutions { get; set; }

        public DbSet<ExerciseEntity> Exercises { get; set; }

        public DbSet<ExerciseSubmissionEntity> ExerciseSubmissions { get; set; }

        public DbSet<FlagEntity> Flags { get; set; }

        public DbSet<FlagSubmissionEntity> FlagSubmissions { get; set; }

        public DbSet<ConfigurationItemEntity> ConfigurationItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Create keys for many-to-many relationship join tables lacking an own primary key
            builder.Entity<LabExecutionEntity>()
                .HasKey(lse => new {lse.GroupId, lse.LabId});
            builder.Entity<FlagSubmissionEntity>()
                .HasKey(fs => new {fs.FlagId, fs.GroupId});

            // Add unique index to user table
            builder.Entity<UserEntity>()
                .HasIndex(u => u.MoodleUserId)
                .IsUnique();

            base.OnModelCreating(builder);
        }
    }
}
