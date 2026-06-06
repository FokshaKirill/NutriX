using AutoMapper;
using Domain.Entities;
using Presentation.Models;

namespace Presentation.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // ── PlannedMeal ──────────────────────────────────────────────
            CreateMap<PlannedMeal, MealViewModel>()
                .ForMember(d => d.MealType,    opt => opt.MapFrom(s => s.MealType.Name))
                .ForMember(d => d.RecipeName,  opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.Name : "—"))
                .ForMember(d => d.Calories,    opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CaloriesPerServing * s.Servings) : 0))
                .ForMember(d => d.Protein,     opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.ProteinPerServing  * s.Servings) : 0))
                .ForMember(d => d.Fat,         opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.FatPerServing      * s.Servings) : 0))
                .ForMember(d => d.Carbs,       opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CarbsPerServing    * s.Servings) : 0));

            CreateMap<PlannedMeal, PlannedMealViewModel>()
                .ForMember(d => d.MealTypeName,   opt => opt.MapFrom(s => s.MealType.Name))
                .ForMember(d => d.RecipeId,       opt => opt.MapFrom(s => s.Recipe.Id))
                .ForMember(d => d.RecipeName,     opt => opt.MapFrom(s => s.Recipe.Name))
                .ForMember(d => d.RecipeImageUrl, opt => opt.MapFrom(s => s.Recipe.ImageUrl))
                .ForMember(d => d.Calories,       opt => opt.MapFrom(s => (int)(s.Recipe.CaloriesPerServing * s.Servings)))
                .ForMember(d => d.Protein,        opt => opt.MapFrom(s => (int)(s.Recipe.ProteinPerServing  * s.Servings)))
                .ForMember(d => d.Fat,            opt => opt.MapFrom(s => (int)(s.Recipe.FatPerServing      * s.Servings)))
                .ForMember(d => d.Carbs,          opt => opt.MapFrom(s => (int)(s.Recipe.CarbsPerServing    * s.Servings)))
                .ForMember(d => d.Cost,           opt => opt.MapFrom(s => s.Recipe.TotalCost * s.Servings));

            // ── Product ──────────────────────────────────────────────────
            CreateMap<Product, ProductViewModel>()
                .ForMember(d => d.CategoryName, opt => opt.Ignore())
                .ForMember(d => d.ImageUrl,     opt => opt.Ignore());

            // ── Recipe → read ViewModels ─────────────────────────────────
            CreateMap<Recipe, RecipeListViewModel>()
                .ForMember(d => d.CaloriesPerServing,
                    opt => opt.MapFrom(s => (int)Math.Round(s.CaloriesPerServing)));

            CreateMap<Recipe, RecipeDetailViewModel>();

            // ── Recipe ↔ RecipeCreateViewModel ───────────────────────────
            CreateMap<Recipe, RecipeCreateViewModel>()
                .ForMember(d => d.Ingredients, opt => opt.MapFrom(s => s.Ingredients))
                .ForMember(d => d.Steps,       opt => opt.MapFrom(s => s.Steps.OrderBy(x => x.Order)));

            CreateMap<RecipeCreateViewModel, Recipe>()
                .ForMember(d => d.Id,          opt => opt.Ignore())
                .ForMember(d => d.Ingredients, opt => opt.Ignore())
                .ForMember(d => d.Steps,       opt => opt.Ignore())
                .ForMember(d => d.PlannedMeals,opt => opt.Ignore())
                .ForMember(d => d.Author,      opt => opt.Ignore())
                .ForMember(d => d.AuthorId,    opt => opt.Ignore());

            // ── RecipeIngredient ↔ RecipeIngredientViewModel ─────────────
            // ЕДИНСТВЕННАЯ регистрация — маппит все поля включая Unit и Amount
            CreateMap<RecipeIngredient, RecipeIngredientViewModel>()
                .ForMember(d => d.ProductName,  opt => opt.MapFrom(s => s.Product != null ? s.Product.Name : ""))
                .ForMember(d => d.PricePerUnit, opt => opt.MapFrom(s => s.Product != null ? s.Product.PricePerUnit : 0))
                .ForMember(d => d.Calories,     opt => opt.MapFrom(s => s.Calories))
                .ForMember(d => d.Protein,      opt => opt.MapFrom(s => s.Protein))
                .ForMember(d => d.Fat,          opt => opt.MapFrom(s => s.Fat))
                .ForMember(d => d.Carbs,        opt => opt.MapFrom(s => s.Carbs));
                // Amount, Unit, Comment, ProductId маппятся автоматически по имени

            CreateMap<RecipeIngredientViewModel, RecipeIngredient>()
                .ForMember(d => d.Id,       opt => opt.Ignore())
                .ForMember(d => d.RecipeId, opt => opt.Ignore())
                .ForMember(d => d.Recipe,   opt => opt.Ignore())
                .ForMember(d => d.Product,  opt => opt.Ignore());
                // Amount, Unit, Comment, ProductId маппятся автоматически

            // ── RecipeStep ↔ RecipeStepViewModel ─────────────────────────
            CreateMap<RecipeStep, RecipeStepViewModel>();
                // Id, Order, Description, TimerSeconds, ImageUrl — всё по имени

            CreateMap<RecipeStepViewModel, RecipeStep>()
                .ForMember(d => d.Id,       opt => opt.Ignore())
                .ForMember(d => d.RecipeId, opt => opt.Ignore())
                .ForMember(d => d.Recipe,   opt => opt.Ignore());

            // ── MealPlan ─────────────────────────────────────────────────
            CreateMap<MealPlan, HomeViewModel>()
                .ForMember(d => d.TodayDate,          opt => opt.MapFrom(_ => DateTime.Today))
                .ForMember(d => d.WeekStart,          opt => opt.MapFrom(s => s.StartDate))
                .ForMember(d => d.WeekEnd,            opt => opt.MapFrom(s => s.StartDate.AddDays(6)))
                .ForMember(d => d.TodayMeals,         opt => opt.Ignore())
                .ForMember(d => d.WeekDays,           opt => opt.Ignore())
                .ForMember(d => d.TodayTotalCalories, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalProtein,  opt => opt.Ignore())
                .ForMember(d => d.TodayTotalFat,      opt => opt.Ignore())
                .ForMember(d => d.TodayTotalCarbs,    opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    if (src?.Meals == null || !src.Meals.Any())
                    {
                        dest.WeekDays   = new List<DayPlanViewModel>();
                        dest.TodayMeals = new List<MealViewModel>();
                        return;
                    }

                    var allMeals = src.Meals
                        .OrderBy(m => m.DayOffset)
                        .ThenBy(m => m.MealType.Order)
                        .ToList();

                    dest.WeekDays = Enumerable.Range(0, 7).Select(offset =>
                    {
                        var dayMeals = allMeals
                            .Where(m => m.DayOffset == offset)
                            .Select(m => context.Mapper.Map<MealViewModel>(m))
                            .ToList();

                        return new DayPlanViewModel
                        {
                            Date  = src.StartDate.AddDays(offset),
                            Meals = dayMeals.Select(m => new PlannedMealViewModel
                            {
                                MealTypeName = m.MealType,
                                RecipeName   = m.RecipeName,
                                Servings     = m.Servings,
                                Calories     = m.Calories,
                                Protein      = m.Protein,
                                Fat          = m.Fat,
                                Carbs        = m.Carbs
                            }).ToList()
                        };
                    }).ToList();

                    var todayOffset = (DateTime.Today - src.StartDate).Days;
                    if (todayOffset is >= 0 and <= 6)
                    {
                        var todayMeals = allMeals
                            .Where(m => m.DayOffset == todayOffset)
                            .Select(m => context.Mapper.Map<MealViewModel>(m))
                            .ToList();

                        dest.TodayMeals         = todayMeals;
                        dest.TodayTotalCalories = todayMeals.Sum(m => m.Calories);
                        dest.TodayTotalProtein  = todayMeals.Sum(m => m.Protein);
                        dest.TodayTotalFat      = todayMeals.Sum(m => m.Fat);
                        dest.TodayTotalCarbs    = todayMeals.Sum(m => m.Carbs);
                    }
                    else
                    {
                        dest.TodayMeals = new List<MealViewModel>();
                    }
                });

            CreateMap<MealPlan, WeekPlanViewModel>()
                .ForMember(d => d.MealPlanId, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.StartDate,  opt => opt.MapFrom(s => s.StartDate))
                .ForMember(d => d.Days,       opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    if (src?.Meals == null || !src.Meals.Any())
                    {
                        dest.Days = new List<DayPlanViewModel>();
                        return;
                    }

                    dest.Days = Enumerable.Range(0, 7).Select(offset =>
                    {
                        var dayMeals = src.Meals
                            .Where(m => m.DayOffset == offset)
                            .OrderBy(m => m.MealType.Order)
                            .Select(m => context.Mapper.Map<PlannedMealViewModel>(m))
                            .ToList();

                        return new DayPlanViewModel
                        {
                            Date  = src.StartDate.AddDays(offset),
                            Meals = dayMeals
                        };
                    }).ToList();
                });
        }
    }
}