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
            vm.CategoryName = product.Parent?.Name;
            vm.ImageUrl = product.ImageUrl;

            return View(vm);
        }
        
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
        
        [HttpGet]
        public async Task<IActionResult> EditModal(Guid id)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();
            return PartialView("_EditProductModal", product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModal(Product product, IFormFile? image)
        {
            if (image != null && image.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (image.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError("image", "Изображение не должно превышать 5 МБ.");
                else if (!allowedExtensions.Contains(extension))
                    ModelState.AddModelError("image", "Допустимые форматы: JPG, PNG, GIF, WEBP.");
            }

            if (!ModelState.IsValid)
                return PartialView("_EditProductModal", product);

            var existing = await _productService.GetByIdAsync(product.Id);
            if (existing == null) return NotFound();

            existing.Name          = product.Name;
            existing.PricePerUnit  = product.PricePerUnit;
            existing.Unit          = product.Unit;
            existing.WeightGrams   = product.WeightGrams;
            existing.Category      = product.Category;
            existing.CaloriesPer100 = product.CaloriesPer100;
            existing.ProteinPer100  = product.ProteinPer100;
            existing.FatPer100      = product.FatPer100;
            existing.CarbsPer100    = product.CarbsPer100;
            existing.UpdatedAt      = DateTime.UtcNow;

            if (image != null && image.Length > 0)
            {
                var fileName      = Guid.NewGuid() + Path.GetExtension(image.FileName);
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/products");
                Directory.CreateDirectory(uploadsFolder);
                await using var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create);
                await image.CopyToAsync(stream);
                existing.ImageUrl = "/images/products/" + fileName;
            }

            await _productService.UpdateAsync(existing);
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