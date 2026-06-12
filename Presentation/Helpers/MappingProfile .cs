using AutoMapper;
using Domain.Entities;
using Presentation.Models;

namespace Presentation.Helpers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ── PlannedMeal ──────────────────────────────────────────────────────
        // MealViewModel — лёгкая модель для Home/Index
        CreateMap<PlannedMeal, MealViewModel>()
            // MealType теперь берётся через MealSlot
            .ForMember(d => d.MealType,  opt => opt.MapFrom(s => s.MealSlot != null ? s.MealSlot.MealType.Name : ""))
            .ForMember(d => d.RecipeName, opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.Name : "—"))
            .ForMember(d => d.Calories,  opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CaloriesPerServing * s.Servings) : 0))
            .ForMember(d => d.Protein,   opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.ProteinPerServing  * s.Servings) : 0))
            .ForMember(d => d.Fat,       opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.FatPerServing      * s.Servings) : 0))
            .ForMember(d => d.Carbs,     opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CarbsPerServing    * s.Servings) : 0));

        // PlannedMealViewModel — полная модель для Week
        CreateMap<PlannedMeal, PlannedMealViewModel>()
            // MealTypeName через MealSlot → MealType
            .ForMember(d => d.MealTypeName,   opt => opt.MapFrom(s => s.MealSlot != null ? s.MealSlot.MealType.Name : ""))
            .ForMember(d => d.RecipeId,       opt => opt.MapFrom(s => s.Recipe.Id))
            .ForMember(d => d.RecipeName,     opt => opt.MapFrom(s => s.Recipe.Name))
            .ForMember(d => d.RecipeImageUrl, opt => opt.MapFrom(s => s.Recipe.ImageUrl))
            .ForMember(d => d.Calories,       opt => opt.MapFrom(s => (int)(s.Recipe.CaloriesPerServing * s.Servings)))
            .ForMember(d => d.Protein,        opt => opt.MapFrom(s => (int)(s.Recipe.ProteinPerServing  * s.Servings)))
            .ForMember(d => d.Fat,            opt => opt.MapFrom(s => (int)(s.Recipe.FatPerServing      * s.Servings)))
            .ForMember(d => d.Carbs,          opt => opt.MapFrom(s => (int)(s.Recipe.CarbsPerServing    * s.Servings)))
            .ForMember(d => d.Cost,           opt => opt.MapFrom(s => s.Recipe.TotalCost * s.Servings));

        // ── Product ──────────────────────────────────────────────────────────
        CreateMap<Product, ProductViewModel>()
            .ForMember(d => d.CategoryName, opt => opt.Ignore())
            .ForMember(d => d.ImageUrl,     opt => opt.Ignore());

        // ── Recipe → read ViewModels ─────────────────────────────────────────
        CreateMap<Recipe, RecipeListViewModel>()
            .ForMember(d => d.CaloriesPerServing,
                opt => opt.MapFrom(s => (int)Math.Round(s.CaloriesPerServing)));

        CreateMap<Recipe, RecipeDetailViewModel>();

        // ── Recipe ↔ RecipeCreateViewModel ──────────────────────────────────
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

        // ── RecipeIngredient ↔ RecipeIngredientViewModel ─────────────────────
        CreateMap<RecipeIngredient, RecipeIngredientViewModel>()
            .ForMember(d => d.ProductName,  opt => opt.MapFrom(s => s.Product != null ? s.Product.Name : ""))
            .ForMember(d => d.PricePerUnit, opt => opt.MapFrom(s => s.Product != null ? s.Product.PricePerUnit : 0))
            .ForMember(d => d.Calories,     opt => opt.MapFrom(s => s.Calories))
            .ForMember(d => d.Protein,      opt => opt.MapFrom(s => s.Protein))
            .ForMember(d => d.Fat,          opt => opt.MapFrom(s => s.Fat))
            .ForMember(d => d.Carbs,        opt => opt.MapFrom(s => s.Carbs));

        CreateMap<RecipeIngredientViewModel, RecipeIngredient>()
            .ForMember(d => d.Id,       opt => opt.Ignore())
            .ForMember(d => d.RecipeId, opt => opt.Ignore())
            .ForMember(d => d.Recipe,   opt => opt.Ignore())
            .ForMember(d => d.Product,  opt => opt.Ignore());

        // ── RecipeStep ↔ RecipeStepViewModel ─────────────────────────────────
        CreateMap<RecipeStep, RecipeStepViewModel>();

        CreateMap<RecipeStepViewModel, RecipeStep>()
            .ForMember(d => d.Id,       opt => opt.Ignore())
            .ForMember(d => d.RecipeId, opt => opt.Ignore())
            .ForMember(d => d.Recipe,   opt => opt.Ignore());

        // ── MealPlan → HomeViewModel ─────────────────────────────────────────
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
                // Все PlannedMeal через слоты
                var allMeals = src.Slots
                    .OrderBy(s => s.DayOffset)
                    .ThenBy(s => s.MealType.Order)
                    .SelectMany(s => s.Items)
                    .ToList();

                if (!allMeals.Any())
                {
                    dest.WeekDays   = new List<DayPlanViewModel>();
                    dest.TodayMeals = new List<MealViewModel>();
                    return;
                }
                    
                dest.WeekDays = Enumerable.Range(0, 7).Select(offset =>
                {
                    var dayMeals = src.Slots
                        .Where(s => s.DayOffset == offset)
                        .OrderBy(s => s.MealType.Order)
                        .SelectMany(s => s.Items)
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
                    var todayMeals = src.Slots
                        .Where(s => s.DayOffset == todayOffset)
                        .OrderBy(s => s.MealType.Order)
                        .SelectMany(s => s.Items)
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
                    
                dest.TodaySlots = src.Slots
                    .Where(s => s.DayOffset == todayOffset)
                    .OrderBy(s => s.MealType.Order)
                    .ToList();
            });

        // ── MealPlan → WeekPlanViewModel ─────────────────────────────────────
        CreateMap<MealPlan, WeekPlanViewModel>()
            .ForMember(d => d.MealPlanId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.StartDate,  opt => opt.MapFrom(s => s.StartDate))
            .ForMember(d => d.Days,       opt => opt.Ignore())
            .AfterMap((src, dest, context) =>
            {
                dest.Days = Enumerable.Range(0, 7).Select(offset =>
                {
                    var dayMeals = src.Slots
                        .Where(s => s.DayOffset == offset)
                        .OrderBy(s => s.MealType.Order)
                        .SelectMany(s => s.Items)
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