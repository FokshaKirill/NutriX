using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;
using Services.Services;

namespace Presentation.Controllers
{
    public class RecipeController : Controller
    {
        private readonly IRecipeService _recipeService;
        private readonly IProductService _productService;
        private readonly IRecipeParserService _parserService;
        private readonly IMapper _mapper;

        public RecipeController(
            IRecipeService recipeService,
            IProductService productService,
            IRecipeParserService parserService,
            IMapper mapper)
        {
            _recipeService = recipeService;
            _productService = productService;
            _parserService = parserService;
            _mapper = mapper;
        }

        // GET: /Recipe
        public async Task<IActionResult> Index()
        {
            var recipes = await _recipeService.GetAllRecipesAsync();
            var model = _mapper.Map<List<RecipeListViewModel>>(recipes);
            return View(model);
        }

        // GET: /Recipe/Details/{id}
        public async Task<IActionResult> Details(Guid id)
        {
            var recipe = await _recipeService.GetByIdAsync(id);
            if (recipe == null) return NotFound();

            var model = _mapper.Map<RecipeDetailViewModel>(recipe);
            return View(model);
        }
        
        // GET: /Recipe/Create
        public async Task<IActionResult> Create()
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.OrderBy(p => p.Name).ToList();

            var model = new RecipeCreateViewModel
            {
                Ingredients = new List<RecipeIngredientViewModel> { new() },
                Steps = new List<RecipeStepViewModel> { new() }
            };

            return View(model);
        }

        // POST: /Recipe/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecipeCreateViewModel model, IFormFile? mainImage)
        {
            if (!ModelState.IsValid)
            {
                var products = await _productService.GetAllProductsAsync();
                ViewBag.Products = products.OrderBy(p => p.Name).ToList();
                return View(model);
            }

            // Загрузка фото
            if (mainImage != null && mainImage.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(mainImage.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/recipes");
                Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await mainImage.CopyToAsync(stream);
                }

                model.ImageUrl = "/images/recipes/" + fileName;
            }

            var recipe = _mapper.Map<Recipe>(model);
            recipe.Id = Guid.NewGuid();

            recipe.Ingredients = model.Ingredients
                .Where(i => i.ProductId != Guid.Empty && i.Amount > 0)
                .Select(i => new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    ProductId = i.ProductId,
                    Amount = i.Amount,
                    Unit = string.IsNullOrWhiteSpace(i.Unit) ? "г" : i.Unit.Trim(),
                    Comment = i.Comment?.Trim()
                })
                .ToList();

            recipe.Steps = model.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Description))
                .Select((s, index) => new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    Order = index + 1,
                    Description = s.Description.Trim(),
                    TimerSeconds = s.TimerSeconds
                })
                .ToList();

            // Подгружаем продукты для расчёта цены
            foreach (var ing in recipe.Ingredients)
            {
                ing.Product = await _productService.GetByIdAsync(ing.ProductId);
            }

            recipe.TotalCost = recipe.Ingredients.Sum(i => (i.Product?.PricePerUnit ?? 0) * i.Amount) / 100;

            await _recipeService.CreateAsync(recipe);

            return RedirectToAction("Index");
        }

        // GET: /Recipe/Import - Страница импорта рецепта
        public IActionResult Import()
        {
            return View();
        }

        // POST: /Recipe/Import - Импорт рецепта по URL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                TempData["Error"] = "Введите URL рецепта";
                return View();
            }

            try
            {
                // Парсим рецепт
                var parsedRecipe = await _parserService.ParseRecipeFromUrlAsync(url);
                
                // Конвертируем в модель представления
                var model = new RecipeCreateViewModel
                {
                    Name = parsedRecipe.Name,
                    Description = parsedRecipe.Description,
                    DefaultServings = parsedRecipe.Servings,
                    ImageUrl = parsedRecipe.ImageUrl,
                    Ingredients = parsedRecipe.Ingredients.Select(i => new RecipeIngredientViewModel
                    {
                        ProductId = Guid.Empty, // Будет заполнено при сопоставлении
                        ProductName = i.Name,
                        Amount = i.Amount,
                        Unit = i.Unit,
                        Comment = i.Comment
                    }).ToList(),
                    Steps = parsedRecipe.Steps.Select(s => new RecipeStepViewModel
                    {
                        Description = s.Description,
                        TimerSeconds = s.TimerSeconds
                    }).ToList()
                };

                // Загружаем продукты для сопоставления
                var products = await _productService.GetAllProductsAsync();
                ViewBag.Products = products.OrderBy(p => p.Name).ToList();

                // Автоматическое сопоставление продуктов
                foreach (var ingredient in model.Ingredients)
                {
                    var product = products.FirstOrDefault(p => 
                        p.Name.Equals(ingredient.ProductName, StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Contains(ingredient.ProductName ?? "", StringComparison.OrdinalIgnoreCase));
                    
                    if (product != null)
                    {
                        ingredient.ProductId = product.Id;
                    }
                }

                TempData["Success"] = $"Рецепт '{parsedRecipe.Name}' успешно импортирован. Проверьте и сохраните.";
                return View("Create", model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при импорте: {ex.Message}";
                return View();
            }
        }

        // API endpoint для быстрого импорта (AJAX)
        [HttpPost]
        public async Task<IActionResult> ParseRecipeUrl([FromBody] ParseRecipeRequest request)
        {
            try
            {
                var parsedRecipe = await _parserService.ParseRecipeFromUrlAsync(request.Url);
                return Json(new { success = true, data = parsedRecipe });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET: /Recipe/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var recipe = await _recipeService.GetByIdAsync(id);
            if (recipe == null) return NotFound();

            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.OrderBy(p => p.Name).ToList();

            var model = _mapper.Map<RecipeCreateViewModel>(recipe);
            return View(model);
        }

        // POST: /Recipe/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, RecipeCreateViewModel model, IFormFile? mainImage)
        {
            var recipe = await _recipeService.GetByIdAsync(id);
            if (recipe == null) return NotFound();

            if (!ModelState.IsValid)
            {
                var products = await _productService.GetAllProductsAsync();
                ViewBag.Products = products.OrderBy(p => p.Name).ToList();
                return View(model);
            }

            // Обновление фото
            if (mainImage != null && mainImage.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(mainImage.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/recipes");
                Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await mainImage.CopyToAsync(stream);
                }

                model.ImageUrl = "/images/recipes/" + fileName;
            }

            // Обновляем поля
            recipe.Name = model.Name;
            recipe.Description = model.Description;
            recipe.DefaultServings = model.DefaultServings;
            recipe.ImageUrl = model.ImageUrl ?? recipe.ImageUrl;

            // Обновляем ингредиенты
            recipe.Ingredients.Clear();
            recipe.Ingredients = model.Ingredients
                .Where(i => i.ProductId != Guid.Empty && i.Amount > 0)
                .Select(i => new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    ProductId = i.ProductId,
                    Amount = i.Amount,
                    Unit = string.IsNullOrWhiteSpace(i.Unit) ? "г" : i.Unit.Trim(),
                    Comment = i.Comment?.Trim()
                })
                .ToList();

            // Обновляем шаги
            recipe.Steps.Clear();
            recipe.Steps = model.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Description))
                .Select((s, index) => new RecipeStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Order = index + 1,
                    Description = s.Description.Trim(),
                    TimerSeconds = s.TimerSeconds
                })
                .ToList();

            // Пересчитываем стоимость
            foreach (var ing in recipe.Ingredients)
            {
                ing.Product = await _productService.GetByIdAsync(ing.ProductId);
            }
            recipe.TotalCost = recipe.Ingredients.Sum(i => (i.Product?.PricePerUnit ?? 0) * i.Amount) / 100;

            await _recipeService.UpdateAsync(recipe);

            return RedirectToAction("Details", new { id = recipe.Id });
        }

        // POST: /Recipe/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _recipeService.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        private async Task LoadProductsToViewBag()
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.OrderBy(p => p.Name).ToList();
        }
    }

    // Request model для AJAX
    public class ParseRecipeRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}