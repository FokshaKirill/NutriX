using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Presentation.Controllers
{
    /// <summary>
    /// Контроллер для тестирования парсера рецептов
    /// </summary>
    public class ParserTestController : Controller
    {
        private readonly IRecipeParserService _parserService;
        private readonly IRecipeService _recipeService;

        public ParserTestController(
            IRecipeParserService parserService,
            IRecipeService recipeService)
        {
            _parserService = parserService;
            _recipeService = recipeService;
        }

        // GET: /ParserTest/Test?url=...
        public async Task<IActionResult> Test(string url = "https://1000.menu/cooking/2797-bliny-s-myasom-i-risom")
        {
            try
            {
                // Парсим рецепт
                var parsed = await _parserService.ParseRecipeFromUrlAsync(url);

                // Формируем отчет
                var report = new
                {
                    Success = true,
                    Recipe = new
                    {
                        parsed.Name,
                        parsed.Description,
                        parsed.Servings,
                        parsed.ImageUrl,
                        IngredientsCount = parsed.Ingredients.Count,
                        StepsCount = parsed.Steps.Count,
                        parsed.TotalCalories,
                        parsed.TotalProtein,
                        parsed.TotalFat,
                        parsed.TotalCarbs
                    },
                    Ingredients = parsed.Ingredients.Select(i => new
                    {
                        i.Name,
                        i.Amount,
                        i.Unit,
                        i.Comment,
                        i.OriginalText
                    }),
                    Steps = parsed.Steps.Select(s => new
                    {
                        s.Order,
                        Description = s.Description.Length > 100 
                            ? s.Description.Substring(0, 100) + "..." 
                            : s.Description,
                        s.TimerSeconds,
                        HasImage = !string.IsNullOrEmpty(s.ImageUrl)
                    })
                };

                return Json(report);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        // GET: /ParserTest/TestAndSave?url=...
        public async Task<IActionResult> TestAndSave(string url = "https://1000.menu/cooking/2797-bliny-s-myasom-i-risom")
        {
            try
            {
                // Парсим рецепт
                var parsed = await _parserService.ParseRecipeFromUrlAsync(url);

                // Конвертируем в Recipe
                var recipe = await _parserService.ConvertToRecipeAsync(parsed);

                // Сохраняем
                await _recipeService.CreateAsync(recipe);

                return Json(new
                {
                    Success = true,
                    Message = $"Рецепт '{recipe.Name}' успешно импортирован и сохранен!",
                    RecipeId = recipe.Id,
                    IngredientsCount = recipe.Ingredients.Count,
                    StepsCount = recipe.Steps.Count,
                    TotalCost = recipe.TotalCost,
                    TotalCalories = recipe.TotalCalories
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        // GET: /ParserTest/DebugIngredients?url=...
        public async Task<IActionResult> DebugIngredients(string url = "https://1000.menu/cooking/2797-bliny-s-myasom-i-risom")
        {
            try
            {
                var parsed = await _parserService.ParseRecipeFromUrlAsync(url);

                var ingredientsReport = parsed.Ingredients.Select(i => new
                {
                    i.Name,
                    Amount = $"{i.Amount} {i.Unit}",
                    i.Comment,
                    Original = i.OriginalText
                }).ToList();

                return Json(new
                {
                    Success = true,
                    TotalIngredients = ingredientsReport.Count,
                    Ingredients = ingredientsReport
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        // GET: /ParserTest/DebugSteps?url=...
        public async Task<IActionResult> DebugSteps(string url = "https://1000.menu/cooking/2797-bliny-s-myasom-i-risom")
        {
            try
            {
                var parsed = await _parserService.ParseRecipeFromUrlAsync(url);

                var stepsReport = parsed.Steps.Select(s => new
                {
                    s.Order,
                    s.Description,
                    TimerMinutes = s.TimerSeconds.HasValue ? s.TimerSeconds.Value / 60.0 : (double?)null,
                    HasImage = !string.IsNullOrEmpty(s.ImageUrl),
                    s.ImageUrl
                }).ToList();

                return Json(new
                {
                    Success = true,
                    TotalSteps = stepsReport.Count,
                    Steps = stepsReport
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}

// Пример использования в браузере:
// 
// 1. Тест парсинга (без сохранения):
//    https://localhost:5001/ParserTest/Test?url=https://1000.menu/cooking/2797-bliny-s-myasom-i-risom
//
// 2. Парсинг + сохранение в базу:
//    https://localhost:5001/ParserTest/TestAndSave?url=https://1000.menu/cooking/2797-bliny-s-myasom-i-risom
//
// 3. Отладка ингредиентов:
//    https://localhost:5001/ParserTest/DebugIngredients?url=https://1000.menu/cooking/2797-bliny-s-myasom-i-risom
//
// 4. Отладка шагов:
//    https://localhost:5001/ParserTest/DebugSteps?url=https://1000.menu/cooking/2797-bliny-s-myasom-i-risom
//
// Другие рецепты для тестирования:
// - https://1000.menu/cooking/1-olivje
// - https://1000.menu/cooking/12-borshh
// - https://1000.menu/cooking/8-pelmeni