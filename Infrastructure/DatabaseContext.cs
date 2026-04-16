using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<MealType> MealTypes => Set<MealType>();
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
        public DbSet<MealPlan> MealPlans => Set<MealPlan>();
        public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
        public DbSet<User> Users => Set<User>();

        // Infrastructure/DatabaseContext.cs
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Убираем все User-связи
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(
                            new ValueConverter<DateTime, DateTime>(
                                v => v.ToUniversalTime(),
                                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                            ));
                    }
                }
            }

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.GoogleId).IsUnique();

                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.GoogleId).HasMaxLength(255);

                entity.Property(e => e.Role)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(e => e.SubscriptionType)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
                entity.Property(e => e.LastLoginAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            });
                
            // Product hierarchy (остаётся)
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Остальные настройки
            modelBuilder.Entity<RecipeIngredient>()
                .HasOne(ri => ri.Product)
                .WithMany(p => p.UsedInRecipeIngredients)
                .HasForeignKey(ri => ri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlannedMeal>()
                .HasIndex(pm => new { pm.MealPlanId, pm.DayOffset, pm.MealTypeId })
                .IsUnique();

            modelBuilder.Entity<RecipeStep>()
                .HasIndex(rs => new { rs.RecipeId, rs.Order })
                .IsUnique();

            // Decimal precision
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.PricePerUnit).HasPrecision(10, 2);
                entity.Property(p => p.CaloriesPer100).HasPrecision(8, 2);
                entity.Property(p => p.ProteinPer100).HasPrecision(6, 2);
                entity.Property(p => p.FatPer100).HasPrecision(6, 2);
                entity.Property(p => p.CarbsPer100).HasPrecision(6, 2);
            });

            modelBuilder.Entity<RecipeIngredient>(entity =>
            {
                entity.Property(ri => ri.Amount).HasPrecision(10, 3);
            });
        }
    }
}