using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(
            DbContextOptions<RaceDayDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Event> Events => Set<Event>();

        public DbSet<EventType> EventTypes => Set<EventType>();

        public DbSet<Category> Categories => Set<Category>();

        public DbSet<Enrolment> Enrolments => Set<Enrolment>();

        public DbSet<EnrolmentStatus> EnrolmentStatuses
            => Set<EnrolmentStatus>();

        public DbSet<Result> Results => Set<Result>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Users ----------
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.FirstName).HasMaxLength(50);
                entity.Property(u => u.LastName).HasMaxLength(50);
                entity.Property(u => u.Email).HasMaxLength(100);
                entity.Property(u => u.PasswordHash).HasMaxLength(255);
                entity.Property(u => u.PhoneNumber).HasMaxLength(20);
                entity.Property(u => u.Role).HasMaxLength(20);

                entity.HasIndex(u => u.Email).IsUnique();

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Users_Role",
                    "[Role] IN ('Organiser', 'Participant')"));
            });

            // ---------- EventTypes ----------
            modelBuilder.Entity<EventType>(entity =>
            {
                entity.Property(t => t.TypeName).HasMaxLength(30);
                entity.Property(t => t.Description).HasMaxLength(255);

                entity.HasIndex(t => t.TypeName).IsUnique();

                // Starting data. The API needs these rows to exist.
                entity.HasData(
                    new EventType
                    {
                        EventTypeID = 1,
                        TypeName = "Run",
                        Description = "Running events organised through RaceDay"
                    },
                    new EventType
                    {
                        EventTypeID = 2,
                        TypeName = "Walk",
                        Description = "Walking events organised through RaceDay"
                    },
                    new EventType
                    {
                        EventTypeID = 3,
                        TypeName = "Cycle",
                        Description = "Cycling events organised through RaceDay"
                    });
            });

            // ---------- EnrolmentStatuses ----------
            modelBuilder.Entity<EnrolmentStatus>(entity =>
            {
                entity.Property(s => s.StatusName).HasMaxLength(30);
                entity.Property(s => s.Description).HasMaxLength(255);

                entity.HasIndex(s => s.StatusName).IsUnique();

                entity.HasData(
                    new EnrolmentStatus
                    {
                        EnrolmentStatusID = 1,
                        StatusName = "Pending",
                        Description =
                            "The enrolment has been created but is awaiting confirmation"
                    },
                    new EnrolmentStatus
                    {
                        EnrolmentStatusID = 2,
                        StatusName = "Confirmed",
                        Description =
                            "The participant enrolment has been confirmed"
                    },
                    new EnrolmentStatus
                    {
                        EnrolmentStatusID = 3,
                        StatusName = "Cancelled",
                        Description =
                            "The participant enrolment has been cancelled"
                    });
            });

            // ---------- Events ----------
            modelBuilder.Entity<Event>(entity =>
            {
                entity.Property(e => e.EventName).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Location).HasMaxLength(150);
                entity.Property(e => e.EventDate).HasColumnType("date");
                entity.Property(e => e.Distance).HasPrecision(6, 2);

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Events_Distance",
                    "[Distance] > 0"));

                entity.HasOne(e => e.Organiser)
                    .WithMany(u => u.OrganisedEvents)
                    .HasForeignKey(e => e.OrganiserID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.EventType)
                    .WithMany(t => t.Events)
                    .HasForeignKey(e => e.EventTypeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- Categories ----------
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.CategoryName).HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(255);

                // A category name can only be used once per event.
                entity.HasIndex(c => new { c.EventID, c.CategoryName })
                    .IsUnique();

                entity.HasOne(c => c.Event)
                    .WithMany(e => e.Categories)
                    .HasForeignKey(c => c.EventID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- Enrolments ----------
            modelBuilder.Entity<Enrolment>(entity =>
            {
                entity.Property(e => e.EnrolmentDate)
                    .HasColumnType("date")
                    .HasDefaultValueSql("CAST(GETDATE() AS DATE)");

                // A participant can only enrol once per event.
                entity.HasIndex(e => new { e.ParticipantID, e.EventID })
                    .IsUnique();

                entity.HasOne(e => e.Participant)
                    .WithMany(u => u.Enrolments)
                    .HasForeignKey(e => e.ParticipantID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Event)
                    .WithMany(e => e.Enrolments)
                    .HasForeignKey(e => e.EventID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Enrolments)
                    .HasForeignKey(e => e.CategoryID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.EnrolmentStatus)
                    .WithMany(s => s.Enrolments)
                    .HasForeignKey(e => e.EnrolmentStatusID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- Results ----------
            modelBuilder.Entity<Result>(entity =>
            {
                entity.Property(r => r.FinishTime).HasColumnType("time");

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Results_FinishPosition",
                    "[FinishPosition] > 0"));

                // One result per enrolment.
                entity.HasIndex(r => r.EnrolmentID).IsUnique();

                entity.HasOne(r => r.Enrolment)
                    .WithOne(e => e.Result)
                    .HasForeignKey<Result>(r => r.EnrolmentID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
