using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;
using AutoMapper;

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
            int userId = 1;

            var today = DateTime.Today;
            var monday = StartOfWeek(today, DayOfWeek.Monday);

            var todayMeals = await _mealService.GetMealsByDateAsync(today, userId);
            var todayCalories = await _mealService.GetTotalCaloriesAsync(today, userId);
            var todayProtein = await _mealService.GetTotalProteinAsync(today, userId);
            var todayFat = await _mealService.GetTotalFatAsync(today, userId);
            var todayCarbs = await _mealService.GetTotalCarbsAsync(today, userId);

            var weekPlans = await _mealPlanService.GetUserPlansAsync(userId);

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
                WeekDays = _mapper.Map<List<DayViewModel>>(weekPlans)
            };

            return View(model);
        }

        private static DateTime StartOfWeek(DateTime date, DayOfWeek startOfWeek)
        {
            int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
            return date.AddDays(-diff).Date;
        }
    }
}
