using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers
{
    public class RecipeController : Controller
    {
        private readonly IRecipeService _recipeService;
        private readonly IProductService _productService;
        private readonly IMapper _mapper;

        public RecipeController(
            IRecipeService recipeService,
            IProductService productService,
            IMapper mapper)
        {
            _recipeService = recipeService;
            _productService = productService;
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
                    Unit = string.IsNullOrWhiteSpace(i.Unit) ? "шт" : i.Unit.Trim(),
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

            recipe.TotalCost = recipe.Ingredients.Sum(i => (i.Product?.PricePerUnit ?? 0) * i.Amount);

            await _recipeService.CreateAsync(recipe);

            return RedirectToAction("Index");
        }
        
        private async Task LoadProductsToViewBag()
        {
            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.OrderBy(p => p.Name).ToList();
        }
    }
}