using AutoMapper;
using Domain.Entities;
using Presentation.Models;
using System;
using System.Linq;

namespace Presentation.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // PlannedMeal → MealViewModel (главная страница)
            CreateMap<PlannedMeal, MealViewModel>()
                .ForMember(d => d.MealType, opt => opt.MapFrom(s => s.MealType.Name))
                .ForMember(d => d.RecipeName, opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.Name : "—"))
                .ForMember(d => d.Servings, opt => opt.MapFrom(s => s.Servings))
                .ForMember(d => d.Calories, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.CaloriesPerServing * s.Servings) : 0))
                .ForMember(d => d.Protein, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.ProteinPerServing * s.Servings) : 0))
                .ForMember(d => d.Fat, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.FatPerServing * s.Servings) : 0))
                .ForMember(d => d.Carbs, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.CarbsPerServing * s.Servings) : 0));

            // PlannedMeal → PlannedMealViewModel (страница недели)
            CreateMap<PlannedMeal, PlannedMealViewModel>()
                .ForMember(d => d.MealTypeName, opt => opt.MapFrom(s => s.MealType.Name))
                .ForMember(d => d.RecipeId, opt => opt.MapFrom(s => s.RecipeId))
                .ForMember(d => d.RecipeName, opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.Name : null))
                .ForMember(d => d.RecipeImageUrl, opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.ImageUrl : null))
                .ForMember(d => d.Calories, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.CaloriesPerServing * s.Servings) : 0))
                .ForMember(d => d.Protein, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.ProteinPerServing * s.Servings) : 0))
                .ForMember(d => d.Fat, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.FatPerServing * s.Servings) : 0))
                .ForMember(d => d.Carbs, opt => opt.MapFrom(s => s.Recipe != null 
                    ? (int)Math.Round(s.Recipe.CarbsPerServing * s.Servings) : 0));

            // Product → ProductViewModel
            CreateMap<Product, ProductViewModel>()
                .ForMember(d => d.CategoryName, opt => opt.Ignore())
                .ForMember(d => d.ImageUrl, opt => opt.Ignore());

            // Recipe → RecipeListViewModel
            CreateMap<Recipe, RecipeListViewModel>()
                .ForMember(d => d.CaloriesPerServing, 
                    opt => opt.MapFrom(s => (int)Math.Round(s.CaloriesPerServing)));

            // Recipe → RecipeDetailViewModel
            CreateMap<Recipe, RecipeDetailViewModel>();

            // RecipeIngredient → RecipeIngredientViewModel (для отображения)
            CreateMap<RecipeIngredient, RecipeIngredientViewModel>()
                .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
                .ForMember(d => d.Calories, opt => opt.MapFrom(s => s.Calories));

            // RecipeStep → RecipeStepViewModel
            CreateMap<RecipeStep, RecipeStepViewModel>();

            // *** ВАЖНО: Маппинг для создания рецепта ***
            // RecipeCreateViewModel → Recipe
            CreateMap<RecipeCreateViewModel, Recipe>()
                .ForMember(d => d.Id, opt => opt.Ignore()) // генерируем вручную
                .ForMember(d => d.Ingredients, opt => opt.Ignore()) // заполняем вручную
                .ForMember(d => d.Steps, opt => opt.Ignore())       // заполняем вручную
                .ForMember(d => d.PlannedMeals, opt => opt.Ignore());

            // RecipeIngredientViewModel → RecipeIngredient (для создания)
            CreateMap<RecipeIngredientViewModel, RecipeIngredient>()
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RecipeId, opt => opt.Ignore())
                .ForMember(d => d.Recipe, opt => opt.Ignore())
                .ForMember(d => d.Product, opt => opt.Ignore())
                .ForMember(d => d.Calories, opt => opt.Ignore())
                .ForMember(d => d.Protein, opt => opt.Ignore())
                .ForMember(d => d.Fat, opt => opt.Ignore())
                .ForMember(d => d.Carbs, opt => opt.Ignore());

            // RecipeStepViewModel → RecipeStep
            CreateMap<RecipeStepViewModel, RecipeStep>()
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RecipeId, opt => opt.Ignore())
                .ForMember(d => d.Recipe, opt => opt.Ignore());

            // MealPlan → HomeViewModel
            CreateMap<MealPlan, HomeViewModel>()
                .ForMember(d => d.TodayDate, opt => opt.MapFrom(_ => DateTime.Today))
                .ForMember(d => d.WeekStart, opt => opt.MapFrom(s => s.StartDate))
                .ForMember(d => d.WeekEnd, opt => opt.MapFrom(s => s.StartDate.AddDays(6)))
                .ForMember(d => d.TodayMeals, opt => opt.Ignore())
                .ForMember(d => d.WeekDays, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalCalories, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalProtein, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalFat, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalCarbs, opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    if (src?.Meals == null || !src.Meals.Any())
                    {
                        dest.WeekDays = new List<DayPlanViewModel>();
                        dest.TodayMeals = new List<MealViewModel>();
                        return;
                    }

                    var allMeals = src.Meals
                        .OrderBy(m => m.DayOffset)
                        .ThenBy(m => m.MealType.Order)
                        .ToList();

                    dest.WeekDays = Enumerable.Range(0, 7)
                        .Select(offset =>
                        {
                            var date = src.StartDate.AddDays(offset);
                            var dayMeals = allMeals
                                .Where(m => m.DayOffset == offset)
                                .Select(m => context.Mapper.Map<MealViewModel>(m))
                                .ToList();

                            return new DayPlanViewModel
                            {
                                Date = date,
                                Meals = dayMeals.Select(m => new PlannedMealViewModel
                                {
                                    MealTypeName = m.MealType,
                                    RecipeName = m.RecipeName,
                                    Servings = m.Servings,
                                    Calories = m.Calories,
                                    Protein = m.Protein,
                                    Fat = m.Fat,
                                    Carbs = m.Carbs
                                }).ToList()
                            };
                        })
                        .ToList();

                    var todayOffset = (DateTime.Today - src.StartDate).Days;
                    if (todayOffset >= 0 && todayOffset <= 6)
                    {
                        var todayMealsList = allMeals
                            .Where(m => m.DayOffset == todayOffset)
                            .Select(m => context.Mapper.Map<MealViewModel>(m))
                            .ToList();

                        dest.TodayMeals = todayMealsList;
                        dest.TodayTotalCalories = todayMealsList.Sum(m => m.Calories);
                        dest.TodayTotalProtein = todayMealsList.Sum(m => m.Protein);
                        dest.TodayTotalFat = todayMealsList.Sum(m => m.Fat);
                        dest.TodayTotalCarbs = todayMealsList.Sum(m => m.Carbs);
                    }
                    else
                    {
                        dest.TodayMeals = new List<MealViewModel>();
                    }
                });

            // MealPlan → WeekPlanViewModel
            CreateMap<MealPlan, WeekPlanViewModel>()
                .ForMember(d => d.MealPlanId, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.StartDate, opt => opt.MapFrom(s => s.StartDate))
                .ForMember(d => d.Days, opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    if (src?.Meals == null || !src.Meals.Any())
                    {
                        dest.Days = new List<DayPlanViewModel>();
                        return;
                    }

                    var allMeals = src.Meals
                        .OrderBy(m => m.DayOffset)
                        .ThenBy(m => m.MealType.Order)
                        .ToList();

                    dest.Days = Enumerable.Range(0, 7)
                        .Select(offset =>
                        {
                            var date = src.StartDate.AddDays(offset);
                            var dayMeals = allMeals
                                .Where(m => m.DayOffset == offset)
                                .Select(m => context.Mapper.Map<PlannedMealViewModel>(m))
                                .ToList();

                            return new DayPlanViewModel
                            {
                                Date = date,
                                Meals = dayMeals
                            };
                        })
                        .ToList();
                });
            
            CreateMap<Recipe, RecipeListViewModel>();
            CreateMap<Recipe, RecipeDetailViewModel>();

            // ←←← Добавь эти два маппинга:
            CreateMap<Recipe, RecipeCreateViewModel>()
                .ForMember(dest => dest.Ingredients, opt => opt.MapFrom(src => src.Ingredients))
                .ForMember(dest => dest.Steps, opt => opt.MapFrom(src => src.Steps));

            CreateMap<RecipeCreateViewModel, Recipe>()
                .ForMember(dest => dest.Ingredients, opt => opt.Ignore())
                .ForMember(dest => dest.Steps, opt => opt.Ignore())
                .ForMember(dest => dest.Author, opt => opt.Ignore())
                .ForMember(dest => dest.AuthorId, opt => opt.Ignore());

            // Маппинги для вложенных объектов
            CreateMap<RecipeIngredient, RecipeIngredientViewModel>();
            CreateMap<RecipeStep, RecipeStepViewModel>();

            CreateMap<RecipeIngredientViewModel, RecipeIngredient>()
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            CreateMap<RecipeStepViewModel, RecipeStep>();
        }
    }
}