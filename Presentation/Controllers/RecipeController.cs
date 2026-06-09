using System.Security.Claims;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers;

public class RecipeController : Controller
{
    private readonly IRecipeService       _recipeService;
    private readonly IProductService      _productService;
    private readonly IRecipeParserService _parserService;
    private readonly IFavoriteService     _favoriteService;
    private readonly IMapper              _mapper;
    private readonly IWebHostEnvironment _env;

    public RecipeController(
        IRecipeService recipeService,
        IProductService productService,
        IRecipeParserService parserService,
        IFavoriteService favoriteService,
        IMapper mapper,
        IWebHostEnvironment env)
    {
        _recipeService   = recipeService;
        _productService  = productService;
        _parserService   = parserService;
        _favoriteService = favoriteService;
        _mapper          = mapper;
        _env = env;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /Recipe
    public async Task<IActionResult> Index(
        string? searchTerm = null,
        RecipeCategory? category = null,
        int page = 1)
    {
        const int PageSize = 12;

        var (recipes, totalCount) = await _recipeService.GetPagedRecipesAsync(
            page, PageSize, searchTerm, category);

        var model = _mapper.Map<List<RecipeListViewModel>>(recipes);

        if (CurrentUserId.HasValue)
        {
            var favIds = (await _favoriteService.GetFavoritesAsync(CurrentUserId.Value))
                .Select(r => r.Id).ToHashSet();

            foreach (var r in model)
                r.IsFavorite = favIds.Contains(r.Id);
        }

        ViewBag.SearchTerm = searchTerm;
        ViewBag.SelectedCategory = category;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
        ViewBag.TotalCount = totalCount;

        // Список категорий для фильтра
        ViewBag.Categories = Enum.GetValues<RecipeCategory>()
            .Where(c => c != RecipeCategory.Other)
            .Select(c => new SelectListItem
            {
                Value = ((int)c).ToString(),
                Text = GetCategoryDisplayName(c),
                Selected = c == category
            })
            .ToList();

        return View(model);
    }

    private string GetCategoryDisplayName(RecipeCategory category)
    {
        return category switch
        {
            RecipeCategory.Breakfast => "Завтрак",
            RecipeCategory.Lunch => "Обед",
            RecipeCategory.Dinner => "Ужин",
            RecipeCategory.Snack => "Перекус",
            RecipeCategory.Dessert => "Десерт",
            RecipeCategory.Salad => "Салат",
            RecipeCategory.Soup => "Суп",
            RecipeCategory.MainCourse => "Основное блюдо",
            RecipeCategory.Healthy => "ПП / Здоровое",
            RecipeCategory.Quick => "Быстрый рецепт",
            RecipeCategory.Festive => "Праздничный",
            _ => category.ToString()
        };
    }
    
    public async Task<IActionResult> Import()
    {
        return View();
    }
    
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string url)
    {
        if (!CurrentUserId.HasValue)
            return Redirect("/account/authpage");

        if (string.IsNullOrWhiteSpace(url) || !url.Contains("1000.menu/cooking/"))
        {
            TempData["Error"] = "Некорректный URL. Поддерживается только 1000.menu";
            return View();
        }

        try
        {
            var parsed = await _parserService.ParseRecipeFromUrlAsync(url);
            var recipe = await _parserService.ConvertToRecipeAsync(parsed);

            recipe.AuthorId  = CurrentUserId.Value;
            recipe.CreatedAt = DateTime.UtcNow;

            await _recipeService.CreateAsync(recipe);

            TempData["Success"] = $"Рецепт «{recipe.Name}» успешно импортирован!";
            return RedirectToAction("Details", new { id = recipe.Id });
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "Не удалось загрузить страницу. Проверьте ссылку или попробуйте позже.";
            return View();
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка при импорте: {ex.Message}";
            return View();
        }
    }
    
    public async Task<IActionResult> Details(Guid id)
    {
        var recipe = await _recipeService.GetByIdAsync(id, includeIngredients: true, includeSteps: true);
        if (recipe == null) return NotFound();

        var model = _mapper.Map<RecipeDetailViewModel>(recipe);
        model.TotalCost = recipe.TotalCost;
        
        foreach (var (ing, vm) in recipe.Ingredients.Zip(model.Ingredients))
        {
            vm.PricePerUnit = ing.Product?.PricePerUnit ?? 0;
            vm.Calories = ing.Calories;
            vm.Protein  = ing.Protein;
            vm.Fat      = ing.Fat;
            vm.Carbs    = ing.Carbs;
        }

        model.ProteinPerServing = model.DefaultServings > 0
            ? model.Ingredients.Sum(i => i.Protein ?? 0) / model.DefaultServings : 0;
        model.FatPerServing = model.DefaultServings > 0
            ? model.Ingredients.Sum(i => i.Fat ?? 0) / model.DefaultServings : 0;
        model.CarbsPerServing = model.DefaultServings > 0
            ? model.Ingredients.Sum(i => i.Carbs ?? 0) / model.DefaultServings : 0;

        if (CurrentUserId.HasValue)
        {
            model.IsFavorite = await _favoriteService.IsFavoriteAsync(CurrentUserId.Value, id);
            model.IsOwner    = recipe.AuthorId == CurrentUserId.Value;
        }

        return View(model);
    }

    // GET: /Recipe/Create
    public async Task<IActionResult> Create()
    {
        if (!CurrentUserId.HasValue) return Redirect("/account/authpage");

        ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
        return View(new RecipeCreateViewModel { Ingredients = [new()], Steps = [new()] });
    }

    // POST: /Recipe/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        RecipeCreateViewModel model,
        IFormFile? mainImage,
        IFormFile[]? stepImages)
    {
        if (!CurrentUserId.HasValue) return Redirect("/account/authpage");

        if (!ModelState.IsValid)
        {
            ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
            return View(model);
        }

        if (mainImage?.Length > 0)
            model.ImageUrl = await SaveImageAsync(mainImage);

        var recipe = _mapper.Map<Recipe>(model);
        recipe.Id        = Guid.NewGuid();
        recipe.AuthorId  = CurrentUserId.Value;
        recipe.CreatedAt = DateTime.UtcNow;

        recipe.Ingredients = model.Ingredients
            .Where(i => i.ProductId != Guid.Empty && i.Amount > 0)
            .Select(i => new RecipeIngredient
            {
                Id        = Guid.NewGuid(),
                ProductId = i.ProductId,
                Amount    = i.Amount,
                Unit      = string.IsNullOrWhiteSpace(i.Unit) ? "г" : i.Unit.Trim(),
                Comment   = i.Comment?.Trim()
            }).ToList();

        // Шаги — сохраняем с фото
        var validSteps = model.Steps
            .Select((s, idx) => (step: s, idx))
            .Where(x => !string.IsNullOrWhiteSpace(x.step.Description))
            .ToList();

        recipe.Steps = new List<RecipeStep>();
        foreach (var ((stepVm, originalIdx), fileIdx) in validSteps.Select((x, i) => (x, i)))
        {
            var step = new RecipeStep
            {
                Id           = Guid.NewGuid(),
                Order        = recipe.Steps.Count + 1,
                Description  = stepVm.Description.Trim(),
                TimerSeconds = stepVm.TimerSeconds,
                ImageUrl     = stepVm.ImageUrl   // существующий (при create — null)
            };

            // Если загружено новое фото для этого шага
            if (stepImages != null && fileIdx < stepImages.Length && stepImages[fileIdx]?.Length > 0)
                step.ImageUrl = await SaveImageAsync(stepImages[fileIdx]);

            recipe.Steps.Add(step);
        }

        foreach (var ing in recipe.Ingredients)
            ing.Product = await _productService.GetByIdAsync(ing.ProductId);

        await _recipeService.CreateAsync(recipe);
        return RedirectToAction("Index");
    }

    // POST: /Recipe/ToggleFavorite  — AJAX JSON endpoint
    [HttpPost("Recipe/ToggleFavorite"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite([FromForm] Guid id)
    {
        if (!CurrentUserId.HasValue)
            return Json(new { error = "unauthorized" });

        await _favoriteService.ToggleAsync(CurrentUserId.Value, id);
        var isFav = await _favoriteService.IsFavoriteAsync(CurrentUserId.Value, id);

        return Json(new { isFavorite = isFav });
    }

    // GET: /Recipe/Favorites
    public async Task<IActionResult> Favorites()
    {
        if (!CurrentUserId.HasValue) return Redirect("/account/authpage");

        var recipes = await _favoriteService.GetFavoritesAsync(CurrentUserId.Value);
        var model   = _mapper.Map<List<RecipeListViewModel>>(recipes);
        foreach (var r in model) r.IsFavorite = true;

        ViewData["Title"] = "Избранные рецепты";
        return View("Index", model);
    }

    // GET: /Recipe/Mine
    public async Task<IActionResult> Mine()
    {
        if (!CurrentUserId.HasValue) return Redirect("/account/authpage");

        var recipes = await _recipeService.GetByAuthorAsync(CurrentUserId.Value);
        var model   = _mapper.Map<List<RecipeListViewModel>>(recipes);

        ViewData["Title"] = "Мои рецепты";
        return View("Index", model);
    }

    // GET: /Recipe/Edit/{id}
    public async Task<IActionResult> Edit(Guid id)
    {
        var recipe = await _recipeService.GetByIdAsync(id, includeIngredients: true, includeSteps: true);
        if (recipe == null) return NotFound();
        if (recipe.AuthorId != CurrentUserId) return Forbid();

        ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
        return View(_mapper.Map<RecipeCreateViewModel>(recipe));
    }


// POST: /Recipe/Edit/{id}
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(
    Guid id,
    RecipeCreateViewModel model,
    IFormFile? mainImage,
    IFormFile[]? stepImages)
{
    var recipe = await _recipeService.GetByIdAsync(id, includeIngredients: true, includeSteps: true);
    if (recipe == null) return NotFound();
    if (recipe.AuthorId != CurrentUserId) return Forbid();

    if (!ModelState.IsValid)
    {
        ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
        return View(model);
    }

    recipe.Name            = model.Name;
    recipe.Description     = model.Description;
    recipe.DefaultServings = model.DefaultServings;

    // Главное фото: меняем только если загружено новое
    if (mainImage?.Length > 0)
        recipe.ImageUrl = await SaveImageAsync(mainImage);
    // иначе recipe.ImageUrl остаётся прежним (не трогаем)

    // Ингредиенты
    recipe.Ingredients.Clear();
    foreach (var ingVm in model.Ingredients.Where(i => i.ProductId != Guid.Empty && i.Amount > 0))
    {
        recipe.Ingredients.Add(new RecipeIngredient
        {
            Id        = Guid.NewGuid(),
            RecipeId  = recipe.Id,
            ProductId = ingVm.ProductId,
            Amount    = ingVm.Amount,
            Unit      = string.IsNullOrWhiteSpace(ingVm.Unit) ? "г" : ingVm.Unit.Trim(),
            Comment   = ingVm.Comment?.Trim()
        });
    }

    // Шаги — сохраняем с фото
    var validSteps = model.Steps
        .Select((s, idx) => (step: s, idx))
        .Where(x => !string.IsNullOrWhiteSpace(x.step.Description))
        .ToList();

    recipe.Steps.Clear();
    foreach (var ((stepVm, originalIdx), fileIdx) in validSteps.Select((x, i) => (x, i)))
    {
        var step = new RecipeStep
        {
            Id           = Guid.NewGuid(),
            RecipeId     = recipe.Id,
            Order        = recipe.Steps.Count + 1,
            Description  = stepVm.Description.Trim(),
            TimerSeconds = stepVm.TimerSeconds,
            ImageUrl     = stepVm.ImageUrl   // сохранённый hidden-полем старый URL
        };

        // Если загружено новое фото — перезаписываем
        if (stepImages != null && fileIdx < stepImages.Length && stepImages[fileIdx]?.Length > 0)
            step.ImageUrl = await SaveImageAsync(stepImages[fileIdx]);

        recipe.Steps.Add(step);
    }

    foreach (var ing in recipe.Ingredients)
        ing.Product = await _productService.GetByIdAsync(ing.ProductId);

    await _recipeService.UpdateAsync(recipe);
    return RedirectToAction("Details", new { id = recipe.Id });
}
    // POST: /Recipe/Delete/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var recipe = await _recipeService.GetByIdAsync(id);
        if (recipe == null) return NotFound();
        if (recipe.AuthorId != CurrentUserId) return Forbid();

        await _recipeService.DeleteAsync(id);
        return RedirectToAction("Index");
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        var fileName      = Guid.NewGuid() + Path.GetExtension(file.FileName);
        var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "recipes");
        Directory.CreateDirectory(uploadsFolder);

        await using var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return "/images/recipes/" + fileName;
    }
}