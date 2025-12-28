using AutoMapper;
using Domain.Entities;
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
            Guid? categoryId,
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
                categoryId,
                minCalories,
                maxCalories,
                minPrice,
                maxPrice);

            var viewModels = _mapper.Map<List<ProductViewModel>>(products);

            foreach (var vm in viewModels)
            {
                var entity = products.First(p => p.Id == vm.Id);
                vm.CategoryName = entity.Parent?.Name;
                vm.ImageUrl = entity.ImageUrl;
            }

            // Категории для фильтра
            var categories = await _productService.GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            // Параметры фильтрации
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.MinCalories = minCalories;
            ViewBag.MaxCalories = maxCalories ?? 900;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            // Пагинация
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
        
        [HttpPost]
        public async Task<IActionResult> ImportProducts()
        {
            await _productService.ImportProductsAsync();
            return RedirectToAction("Index");
        }
    }
}