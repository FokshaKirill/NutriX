using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMealService _mealService;
        private readonly IMealPlanService _mealPlanService;
        private readonly IMapper _mapper;

        public HomeController(
            IMealService mealService,
            IMealPlanService mealPlanService,
            IMapper mapper)
        {
            _mealService = mealService;
            _mealPlanService = mealPlanService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            var todayMeals = await _mealService.GetMealsByDateAsync(today);

            var todayCalories = await _mealService.GetTotalCaloriesAsync(today);
            var todayProtein = await _mealService.GetTotalProteinAsync(today);
            var todayFat = await _mealService.GetTotalFatAsync(today);
            var todayCarbs = await _mealService.GetTotalCarbsAsync(today);

            var currentPlan = await _mealPlanService.GetCurrentWeekPlanAsync();

            var weekDays = new List<DayPlanViewModel>();
            if (currentPlan != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    var date = monday.AddDays(i);
                    var dayMeals = currentPlan.Meals
                        .Where(m => m.DayOffset == i)
                        .OrderBy(m => m.MealType.Order)
                        .ToList();

                    weekDays.Add(new DayPlanViewModel
                    {
                        Date = date,
                        Meals = _mapper.Map<List<PlannedMealViewModel>>(dayMeals)
                    });
                }
            }

            var model = new HomeViewModel
            {
                TodayDate = today,
                TodayMeals = _mapper.Map<List<MealViewModel>>(todayMeals),
                TodayTotalCalories = todayCalories,
                TodayTotalProtein = todayProtein,
                TodayTotalFat = todayFat,
                TodayTotalCarbs = todayCarbs,
                WeekStart = monday,
                WeekEnd = monday.AddDays(6),
                WeekDays = weekDays
            };

            return View(model);
        }
    }
}