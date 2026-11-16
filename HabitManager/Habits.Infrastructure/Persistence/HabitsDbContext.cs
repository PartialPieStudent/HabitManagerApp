using Habits.Domain.Entities;
using Habits.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Habits.Infrastructure.Persistence;

//EF Core database context: kuras tabulas eksistē un kā tās tiek veidotas
//(Atslēgas, colonnu izmēri, habit -> completions relation, unique index).
public sealed class HabitsDbContext : DbContext
{
    public HabitsDbContext(DbContextOptions<HabitsDbContext> options)
        : base(options)
    {
    }

    public DbSet<HabitEntity> Habits => Set<HabitEntity>();

    public DbSet<HabitCompletionEntity> HabitCompletions => Set<HabitCompletionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HabitEntity>(habit =>
        {
            habit.ToTable("Habits");

            habit.HasKey(h => h.Id);
            habit.Property(h => h.Id).ValueGeneratedNever(); // id nāk no domena (Guid.NewGuid)

            habit.Property(h => h.UserId).IsRequired().HasMaxLength(450); // vienāds izmērs ar ASP.NET Core Identity
            habit.HasIndex(h => h.UserId);                                 // ātri "habits of this user"

            habit.Property(h => h.Title).IsRequired().HasMaxLength(Habit.MaxTitleLength);

            habit.Property(h => h.Frequency)
                .HasConversion<string>()   // glabāts kā "Daily" / "Weekly", ir nolasams DB iekšā
                .HasMaxLength(20);

            habit.Property(h => h.DaysOfWeek).IsRequired().HasMaxLength(100);

            // Viens ieradums -> vairāki izpildijumi.
            // nodzēsts ieradums -> izdzēš arī visus tā ieradumus.
            habit.HasMany(h => h.Completions)
                .WithOne(c => c.Habit)
                .HasForeignKey(c => c.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HabitCompletionEntity>(completion =>
        {
            completion.ToTable("HabitCompletions");

            completion.HasKey(c => c.Id);
            completion.Property(c => c.Id).ValueGeneratedNever();

            // ieradums nevar būt izpildīts vienā laikā divreizs,
            // even if two requests arrive at the same moment.
            completion.HasIndex(c => new { c.HabitId, c.Date })
                .IsUnique()
                .HasDatabaseName("UX_HabitCompletions_HabitId_Date");
        });
    }
}
