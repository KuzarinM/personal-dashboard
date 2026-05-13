using DashboardApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Dashboard> Dashboards { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<CalendarSource> Calendars { get; set; }
        public DbSet<ManualEvent> ManualEvents { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<DashboardStatus> Statuses { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<DashboardAccess> DashboardAccesses { get; set; }
        public DbSet<Integration> Integrations { get; set; }
        public DbSet<MonitorItem> Monitors { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Глобальная уникальность имени тега
            modelBuilder.Entity<Tag>().HasIndex(t => t.Name).IsUnique();
        }
    }
}
