using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;
        private const int PageSize = 30;

        public ProductController(IProductService productService, IMapper mapper)
        {
            _productService = productService;
            _mapper = mapper;
        }

        // GET: /Product
        public async Task<IActionResult> Index(
            string searchTerm, 
            ProductCategory? category, 
            decimal? minCalories,
            decimal? maxCalories,
            decimal? minPrice,
            decimal? maxPrice,
            int page = 1)
        {
            if (page < 1) page = 1;

            var (products, totalCount) = await _productService.GetPagedProductsAsync(
                page, 
                PageSize, 
                searchTerm, 
                category,                   
                minCalories,
                maxCalories,
                minPrice,
                maxPrice);

            var viewModels = _mapper.Map<List<ProductViewModel>>(products);

            // Категории для фильтра (Enum)
            ViewBag.Categories = await _productService.GetCategorySelectListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedCategory = category;   // Enum
            ViewBag.MinCalories = minCalories;
            ViewBag.MaxCalories = maxCalories ?? 900;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            ViewBag.TotalCount = totalCount;

            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> CreateModal()
        {
            var categories = await _productService.GetCategoriesAsync();
            ViewBag.ParentCategories = new SelectList(categories, "Id", "Name");

            return PartialView("_CreateProductModal", new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModal(Product product, IFormFile? image)
        {
            if (image != null && image.Length > 0)
            {
                if (image.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("image", "Изображение не должно превышать 5 МБ.");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("image", "Допустимые форматы: JPG, PNG, GIF, WEBP.");
                }
            }

            if (ModelState.IsValid)
            {
                if (image != null && image.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/products");
                    Directory.CreateDirectory(uploadsFolder);

                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    product.ImageUrl = "/images/products/" + fileName;
                }

                await _productService.CreateAsync(product);

                return Json(new { success = true });
            }

            var categories = await _productService.GetCategoriesAsync();
            ViewBag.ParentCategories = new SelectList(categories, "Id", "Name");

            return PartialView("_CreateProductModal", product);
        }
        
        public async Task<IActionResult> Details(Guid id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();

            var vm = _mapper.Map<ProductViewModel>(product);
            vm.ImageUrl = product.ImageUrl;
    
            // Категория из enum, не из Parent
            vm.CategoryName = product.Category != ProductCategory.Other
                ? GetCategoryDisplayName(product.Category)
                : null;

            var recipes = await _productService.GetRecipesUsingProductAsync(id);
            ViewBag.RelatedRecipes = recipes;

            return View(vm);
        }

        private string GetCategoryDisplayName(ProductCategory category) => category switch
        {
            ProductCategory.Meat         => "Мясо",
            ProductCategory.Poultry      => "Птица",
            ProductCategory.Fish         => "Рыба и морепродукты",
            ProductCategory.Dairy        => "Молочное",
            ProductCategory.Eggs         => "Яйца",
            ProductCategory.Grains       => "Крупы и зерновые",
            ProductCategory.Bread        => "Хлеб и макароны",
            ProductCategory.Vegetables   => "Овощи",
            ProductCategory.Fruits       => "Фрукты",
            ProductCategory.Nuts         => "Орехи и семена",
            ProductCategory.Oils         => "Масла и жиры",
            ProductCategory.Spices       => "Специи и приправы",
            ProductCategory.Sweets       => "Сладкое",
            ProductCategory.Canned       => "Консервы",
            ProductCategory.SemiFinished => "Полуфабрикаты",
            ProductCategory.Soy          => "Соевые продукты",
            ProductCategory.Beverages    => "Напитки",
            _                            => null
        };
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategoryModal(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Название не может быть пустым" });
            }

            var category = new Product
            {
                Name = name.Trim(),
                ParentId = null,
                Unit = "категория",
                PricePerUnit = 0
            };

            await _productService.CreateAsync(category);

            return Json(new { success = true, id = category.Id, name = category.Name });
        }
        // GET: /Product/EditModal?id=...
        [HttpGet]
        public async Task<IActionResult> EditModal(Guid id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();

            // Передаем категории в ViewBag, если они нужны внутри формы редактирования
            ViewBag.Products = await _productService.GetAllProductsAsync(); 

            return PartialView("_EditProductModal", product); // Твой файл формы редактирования
        }

        // POST: /Product/EditModal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModal(Product model, IFormFile? image)
        {
            if (!ModelState.IsValid)
                return PartialView("_EditProductModal", model);

            if (image != null && image.Length > 0)
            {
                if (image.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("image", "Изображение не должно превышать 5 МБ.");
                    return PartialView("_EditProductModal", model);
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("image", "Допустимые форматы: JPG, PNG, GIF, WEBP.");
                    return PartialView("_EditProductModal", model);
                }

                var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/products");
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                model.ImageUrl = "/images/products/" + fileName;
            }

            await _productService.UpdateAsync(model);
            return Json(new { success = true });
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null) return Json(new { success = false, message = "Продукт не найден" });

            await _productService.DeleteAsync(id);
            return Json(new { success = true });
        }
    }
}