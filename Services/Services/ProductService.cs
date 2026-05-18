using Domain.Entities;
using Domain.Enums;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Services
{
    public class ProductService : IProductService
    {
        private readonly IRepository<Product> _products;

        public ProductService(IRepository<Product> products)
        {
            _products = products;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _products.Query()
                .Include(p => p.Parent)  
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<(List<Product> Products, int TotalCount)> GetPagedProductsAsync(
            int page, 
            int pageSize, 
            string? searchTerm = null, 
            ProductCategory? category = null,        // ← Enum
            decimal? minCalories = null,
            decimal? maxCalories = null,
            decimal? minPrice = null,
            decimal? maxPrice = null)
        {
            IQueryable<Product> query = _products.Query();

            // Фильтр по категории (Enum)
            if (category.HasValue && category.Value != ProductCategory.Other)
            {
                query = query.Where(p => p.Category == category.Value);
            }

            // Поиск по названию
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()));
            }

            // Калорийность
            if (minCalories.HasValue)
                query = query.Where(p => p.CaloriesPer100 >= minCalories.Value);

            if (maxCalories.HasValue)
                query = query.Where(p => p.CaloriesPer100 <= maxCalories.Value);

            // Цена
            if (minPrice.HasValue)
                query = query.Where(p => p.PricePerUnit >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.PricePerUnit <= maxPrice.Value);

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalCount);
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            return await _products.Query()
                .Include(p => p.Parent)
                .Include(p => p.Children)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Product> CreateAsync(Product product)
        {
            if (product.Id == Guid.Empty)
                product.Id = Guid.NewGuid();

            await _products.AddAsync(product);
            await _products.SaveChangesAsync();
            return product;
        }

        public async Task UpdateAsync(Product product)
        {
            await _products.UpdateAsync(product);
            await _products.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var product = await _products.GetByIdAsync(id);
            if (product != null)
            {
                await _products.DeleteAsync(product);
                await _products.SaveChangesAsync();
            }
        }
        
        public async Task<List<Product>> GetCategoriesAsync()
        {
            return await _products.Query()
                .Where(p => p.ParentId == null)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
        
        public async Task<List<SelectListItem>> GetCategorySelectListAsync()
        {
            return Enum.GetValues<ProductCategory>()
                .Where(c => c != ProductCategory.Other)
                .Select(c => new SelectListItem
                {
                    Value = ((int)c).ToString(),
                    Text = GetCategoryDisplayName(c)
                })
                .ToList();
        }

        private string GetCategoryDisplayName(ProductCategory category)
        {
            return category switch
            {
                ProductCategory.Meat => "Мясо",
                ProductCategory.Poultry => "Птица",
                ProductCategory.Fish => "Рыба и морепродукты",
                ProductCategory.Dairy => "Молочное",
                ProductCategory.Eggs => "Яйца",
                ProductCategory.Grains => "Крупы и зерновые",
                ProductCategory.Bread => "Хлеб и макароны",
                ProductCategory.Vegetables => "Овощи",
                ProductCategory.Fruits => "Фрукты",
                ProductCategory.Nuts => "Орехи и семена",
                ProductCategory.Oils => "Масла и жиры",
                ProductCategory.Spices => "Специи и приправы",
                ProductCategory.Sweets => "Сладкое",
                ProductCategory.Canned => "Консервы",
                ProductCategory.SemiFinished => "Полуфабрикаты",
                ProductCategory.Soy => "Соевые продукты",
                ProductCategory.Beverages => "Напитки",
                _ => category.ToString()
            };
        }

        /// <summary>
        /// Нормализация названия для сопоставления
        /// </summary>
        private string NormalizeName(string name)
        {
            name = name.ToLower().Trim();
            
            // Убираем лишние слова
            var wordsToRemove = new[] 
            { 
                "свежий", "свежая", "свежее",
                "замороженный", "замороженная",
                "охлажденный", "охлажденная"
            };
            
            foreach (var word in wordsToRemove)
            {
                name = name.Replace(word, "");
            }
            
            // Убираем множественные пробелы
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");
            
            return name.Trim();
        }
    }
}