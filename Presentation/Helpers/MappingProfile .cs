using AutoMapper;
using Presentation.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Presentation.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // PlannedMeal -> MealViewModel
            CreateMap<PlannedMeal, MealViewModel>()
                .ForMember(d => d.MealType, opt => opt.MapFrom(s => s.MealType.Name))
                .ForMember(d => d.RecipeName, opt => opt.MapFrom(s => s.Recipe != null ? s.Recipe.Name : "—"))
                .ForMember(d => d.Calories, opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CaloriesPerServing * s.Servings) : 0))
                .ForMember(d => d.Protein, opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.ProteinPerServing * s.Servings) : 0))
                .ForMember(d => d.Fat, opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.FatPerServing * s.Servings) : 0))
                .ForMember(d => d.Carbs, opt => opt.MapFrom(s => s.Recipe != null ? (int)Math.Round(s.Recipe.CarbsPerServing * s.Servings) : 0));

            // MealPlan -> HomeViewModel (основной маппинг для главной страницы)
            CreateMap<MealPlan, HomeViewModel>()
                .ForMember(d => d.TodayDate, opt => opt.MapFrom(s => DateTime.Today))
                .ForMember(d => d.WeekStart, opt => opt.MapFrom(s => s.StartDate))
                .ForMember(d => d.WeekEnd, opt => opt.MapFrom(s => s.StartDate.AddDays(6)))
                .ForMember(d => d.TodayMeals, opt => opt.Ignore()) // рассчитаем вручную ниже
                .ForMember(d => d.WeekDays, opt => opt.Ignore())   // рассчитаем вручную ниже
                .ForMember(d => d.TodayTotalCalories, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalProtein, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalFat, opt => opt.Ignore())
                .ForMember(d => d.TodayTotalCarbs, opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    var allMeals = src.Meals.OrderBy(m => m.DayOffset).ThenBy(m => m.MealType.Order).ToList();

                    // WeekDays
                    dest.WeekDays = Enumerable.Range(0, 7)
                        .Select(offset =>
                        {
                            var date = src.StartDate.AddDays(offset);
                            var dayMeals = allMeals.Where(m => m.DayOffset == offset).Select(m => context.Mapper.Map<MealViewModel>(m)).ToList();
                            return new DayViewModel { Date = date, Meals = dayMeals };
                        })
                        .ToList();

                    // Today (15 декабря 2025 — понедельник, DayOffset 0 для сегодня)
                    var todayOffset = (DateTime.Today - src.StartDate).Days; // если сегодня в пределах недели
                    if (todayOffset >= 0 && todayOffset <= 6)
                    {
                        dest.TodayMeals = dest.WeekDays[(int)todayOffset].Meals;

                        dest.TodayTotalCalories = dest.TodayMeals.Sum(m => m.Calories);
                        dest.TodayTotalProtein = dest.TodayMeals.Sum(m => m.Protein);
                        dest.TodayTotalFat = dest.TodayMeals.Sum(m => m.Fat);
                        dest.TodayTotalCarbs = dest.TodayMeals.Sum(m => m.Carbs);
                    }
                    else
                    {
                        dest.TodayMeals = new List<MealViewModel>();
                        // totals = 0
                    }
                });

            // Если нужно отдельно маппинг DayViewModel (не через Home)
            CreateMap<MealPlan, DayViewModel>()
                .ForMember(d => d.Date, opt => opt.Ignore()) // задаётся вручную
                .ForMember(d => d.Meals, opt => opt.MapFrom((src, dest, _, context) =>
                    src.Meals
                        .Where(m => m.DayOffset == (src.StartDate - dest.Date).Days) // лучше передавать offset
                        .OrderBy(m => m.MealType.Order)
                        .Select(m => context.Mapper.Map<MealViewModel>(m))
                        .ToList()));
        }
    }
}