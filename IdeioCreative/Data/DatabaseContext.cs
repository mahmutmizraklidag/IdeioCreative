using IdeioCreative.Entities;
using Microsoft.EntityFrameworkCore;
using IdeioCreative.Seo;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace IdeioCreative.Data
{
    public class DatabaseContext : DbContext
    {
        private readonly IdeioSeoOptions _seoOptions;
        private readonly TimeProvider _timeProvider;

        public DatabaseContext(DbContextOptions<DatabaseContext> options,
            IOptions<IdeioSeoOptions>? seoOptions = null, TimeProvider? timeProvider = null) : base(options)
        {
            _seoOptions = seoOptions?.Value ?? new IdeioSeoOptions();
            _timeProvider = timeProvider ?? TimeProvider.System;
        }
        public DbSet<Team> Teams { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }
        public DbSet<ContactForm> ContactForms { get; set; }
        public DbSet<About> Abouts { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServiceFaq> ServiceFaqs { get; set; }
        public DbSet<Reference> References { get; set; }
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<SeoPageModification> SeoPageModifications { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            TrackSeoPageModifications();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            TrackSeoPageModifications();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void TrackSeoPageModifications()
        {
            var now = _timeProvider.GetUtcNow();
            var paths = SeoPageModificationTracker.ChangedPagePaths(
                ChangeTracker, _seoOptions, _timeProvider.GetLocalNow().DateTime);

            foreach (var path in paths)
            {
                // Reuse pending revisions when a failed save is retried.
                var pending = SeoPageModifications.Local.FirstOrDefault(revision =>
                    revision.PagePath == path && Entry(revision).State == EntityState.Added);
                if (pending is not null)
                    pending.LastModifiedUtc = now.UtcDateTime;
                else
                    SeoPageModifications.Add(new SeoPageModification
                    {
                        PagePath = path,
                        LastModifiedUtc = now.UtcDateTime
                    });
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ServiceFaq>(faq =>
            {
                faq.Property(item => item.Question).IsRequired();
                faq.Property(item => item.Answer).IsRequired();
                faq.HasIndex(item => new { item.ServiceId, item.SortOrder });
                faq.HasOne(item => item.Service).WithMany(service => service.Faqs)
                    .HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Cascade);
            });


            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                Username = "admin",
                password = "xyz123456"
            });
        }

    }
}
