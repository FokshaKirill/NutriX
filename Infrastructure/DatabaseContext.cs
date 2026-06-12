using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using Domain.Enums;
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
        public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; }
        public DbSet<User> Users => Set<User>();
        public DbSet<MealSlot> MealSlots => Set<MealSlot>();
        
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
                
            // Остальные настройки
            modelBuilder.Entity<RecipeIngredient>()
                .HasOne(ri => ri.Product)
                .WithMany(p => p.UsedInRecipeIngredients)
                .HasForeignKey(ri => ri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlannedMeal>()
                .HasOne(pm => pm.Recipe)
                .WithMany(r => r.PlannedMeals)
                .HasForeignKey(pm => pm.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
                
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
            
            
            // ── FavoriteRecipe: составной PK ──
            modelBuilder.Entity<FavoriteRecipe>()
                .HasKey(f => new { f.UserId, f.RecipeId });
 
            modelBuilder.Entity<FavoriteRecipe>()
                .HasOne(f => f.Recipe)
                .WithMany(r => r.FavoritedBy)
                .HasForeignKey(f => f.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
 
            // ── Recipe: автор ──
            modelBuilder.Entity<Recipe>()
                .Ignore(r => r.TotalCost)
                .HasOne(r => r.Author)
                .WithMany(u => u.Recipes)
                .HasForeignKey(r => r.AuthorId)
                .OnDelete(DeleteBehavior.SetNull); 
            
            modelBuilder.Entity<MealPlan>()
                .Ignore(p => p.Meals);
            
            modelBuilder.Entity<MealSlot>()
                .HasOne(s => s.MealPlan)
                .WithMany(p => p.Slots)
                .HasForeignKey(s => s.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MealSlot>()
                .HasOne(s => s.MealType)
                .WithMany()
                .HasForeignKey(s => s.MealTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlannedMeal>()
                .HasOne(m => m.MealSlot)
                .WithMany(s => s.Items)
                .HasForeignKey(m => m.MealSlotId)
                .OnDelete(DeleteBehavior.Cascade);

            // RecipeTag как int
            modelBuilder.Entity<PlannedMeal>()
                .Property(m => m.Role)
                .HasConversion<int>();

            modelBuilder.Entity<Recipe>()
                .Property(r => r.Tags)
                .HasConversion<int>();
        }
    }
}