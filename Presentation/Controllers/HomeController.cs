using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;
using Services.UserService.Services.Interfaces;

namespace Presentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IRecipeService  _recipeService;
        private readonly IMealPlanService  _mealPlanService;
        private readonly IUserService    _userService;
        private readonly IMapper         _mapper;
 
        public HomeController(
            IRecipeService  recipeService,
            IMealPlanService  mealPlanService,
            IUserService    userService,
            IMapper         mapper)
        {
            _recipeService  = recipeService;
            _mealPlanService  = mealPlanService;
            _userService    = userService;
            _mapper         = mapper;
        }
 
        private Guid? CurrentUserId =>
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
 
        public async Task<IActionResult> Index()
        {
            var allRecipes = await _recipeService.GetAllRecipesAsync();
            var latest = allRecipes.OrderByDescending(r => r.CreatedAt).Take(8).ToList();
            ViewBag.LatestRecipes = _mapper.Map<List<RecipeListViewModel>>(latest);

            if (CurrentUserId.HasValue)
            {
                var user = await _userService.GetByIdAsync(CurrentUserId.Value);
                ViewBag.CurrentUser = user;

                // Загружаем сегодняшние блюда из плана
                var todayMeals = await _mealPlanService.GetTodayMealsAsync(CurrentUserId.Value);
                ViewBag.TodayMeals = todayMeals; // ← передаём во вьюху
            }

            return View();
        }
 
        public IActionResult Error() => View();
        
        public IActionResult Contact()
        {
            return View();
        }
        
        public IActionResult Help()
        {
            return View();
        }
        
        public IActionResult Privacy()
        {
            return View();
        }
    }
}