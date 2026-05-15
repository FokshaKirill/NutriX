using System.Security.Claims;
using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
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

    public RecipeController(
        IRecipeService recipeService,
        IProductService productService,
        IRecipeParserService parserService,
        IFavoriteService favoriteService,
        IMapper mapper)
    {
        _recipeService   = recipeService;
        _productService  = productService;
        _parserService   = parserService;
        _favoriteService = favoriteService;
        _mapper          = mapper;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // GET: /Recipe
    public async Task<IActionResult> Index()
    {
        var recipes = await _recipeService.GetAllRecipesAsync();
        var model   = _mapper.Map<List<RecipeListViewModel>>(recipes);

        if (CurrentUserId.HasValue)
        {
            var favIds = (await _favoriteService.GetFavoritesAsync(CurrentUserId.Value))
                .Select(r => r.Id).ToHashSet();
            foreach (var r in model)
                r.IsFavorite = favIds.Contains(r.Id);
        }

        return View(model);
    }

    // GET: /Recipe/Details/{id}
    public async Task<IActionResult> Details(Guid id)
    {
        var recipe = await _recipeService.GetByIdAsync(id);
        if (recipe == null) return NotFound();

        var model = _mapper.Map<RecipeDetailViewModel>(recipe);

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
    public async Task<IActionResult> Create(RecipeCreateViewModel model, IFormFile? mainImage)
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
                Id = Guid.NewGuid(), ProductId = i.ProductId, Amount = i.Amount,
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? "г" : i.Unit.Trim(),
                Comment = i.Comment?.Trim()
            }).ToList();

        recipe.Steps = model.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .Select((s, idx) => new RecipeStep
            {
                Id = Guid.NewGuid(), Order = idx + 1,
                Description = s.Description.Trim(), TimerSeconds = s.TimerSeconds
            }).ToList();

        foreach (var ing in recipe.Ingredients)
            ing.Product = await _productService.GetByIdAsync(ing.ProductId);

        recipe.TotalCost = recipe.Ingredients.Sum(i => (i.Product?.PricePerUnit ?? 0) * i.Amount) / 100;

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
        var recipe = await _recipeService.GetByIdAsync(id);
        if (recipe == null) return NotFound();
        if (recipe.AuthorId != CurrentUserId) return Forbid();

        ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
        return View(_mapper.Map<RecipeCreateViewModel>(recipe));
    }

    // POST: /Recipe/Edit/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RecipeCreateViewModel model, IFormFile? mainImage)
    {
        var recipe = await _recipeService.GetByIdAsync(id);
        if (recipe == null) return NotFound();
        if (recipe.AuthorId != CurrentUserId) return Forbid();

        if (!ModelState.IsValid)
        {
            ViewBag.Products = (await _productService.GetAllProductsAsync()).OrderBy(p => p.Name).ToList();
            return View(model);
        }

        if (mainImage?.Length > 0)
            model.ImageUrl = await SaveImageAsync(mainImage);

        recipe.Name            = model.Name;
        recipe.Description     = model.Description;
        recipe.DefaultServings = model.DefaultServings;
        recipe.ImageUrl        = model.ImageUrl ?? recipe.ImageUrl;

        recipe.Ingredients = model.Ingredients
            .Where(i => i.ProductId != Guid.Empty && i.Amount > 0)
            .Select(i => new RecipeIngredient
            {
                Id = Guid.NewGuid(), RecipeId = recipe.Id, ProductId = i.ProductId,
                Amount = i.Amount, Unit = string.IsNullOrWhiteSpace(i.Unit) ? "г" : i.Unit.Trim(),
                Comment = i.Comment?.Trim()
            }).ToList();

        recipe.Steps = model.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .Select((s, idx) => new RecipeStep
            {
                Id = Guid.NewGuid(), RecipeId = recipe.Id, Order = idx + 1,
                Description = s.Description.Trim(), TimerSeconds = s.TimerSeconds
            }).ToList();

        foreach (var ing in recipe.Ingredients)
            ing.Product = await _productService.GetByIdAsync(ing.ProductId);

        recipe.TotalCost = recipe.Ingredients.Sum(i => (i.Product?.PricePerUnit ?? 0) * i.Amount) / 100;

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
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/recipes");
        Directory.CreateDirectory(uploadsFolder);

        await using var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return "/images/recipes/" + fileName;
    }
}