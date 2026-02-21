using Domain.Entities;
using Infrastructure.Interfaces;
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
            Guid? categoryId = null,
            decimal? minCalories = null,
            decimal? maxCalories = null,
            decimal? minPrice = null,
            decimal? maxPrice = null)
        {
            IQueryable<Product> query = _products.Query()
                .Include(p => p.Parent);

            // Фильтр: исключаем категории (те, у которых Unit == "категория" или нет CaloriesPer100)
            // Можно использовать любой признак, который отличает категории от продуктов
            query = query.Where(p => p.Unit != "категория" && p.CaloriesPer100 != null);

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm));
            }

            // Фильтр по категории
            if (categoryId.HasValue && categoryId != Guid.Empty)
            {
                query = query.Where(p => p.ParentId == categoryId);
            }

            // Фильтр по калорийности
            if (minCalories.HasValue)
            {
                query = query.Where(p => p.CaloriesPer100 >= minCalories.Value);
            }
            if (maxCalories.HasValue && maxCalories.Value < 900)
            {
                query = query.Where(p => p.CaloriesPer100 <= maxCalories.Value);
            }

            // Фильтр по цене
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.PricePerUnit >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.PricePerUnit <= maxPrice.Value);
            }

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
        
        public async Task ImportProductsAsync()
        {
            var parser = new ProductParser();
            var products = await parser.ParseCalorizatorAsync();

            foreach (var p in products)
            {
                var existing = await _products.Query().FirstOrDefaultAsync(x => x.Name == p.Name);
                if (existing == null)
                {
                    await _products.AddAsync(p);
                }
                else
                {
                    existing.CaloriesPer100 = p.CaloriesPer100;
                    existing.ProteinPer100 = p.ProteinPer100;
                    existing.FatPer100 = p.FatPer100;
                    existing.CarbsPer100 = p.CarbsPer100;
                    await _products.UpdateAsync(existing);
                }
            }

            await _products.SaveChangesAsync();
        }
    }
}