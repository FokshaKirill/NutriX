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

            // Фильтр: исключаем категории
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
        
        /// <summary>
        /// Импорт продуктов из Calorizator.ru (БЖУ база)
        /// </summary>
        public async Task ImportProductsAsync()
        {
            Console.WriteLine("🔄 Запуск импорта из Calorizator...");
            
            var parser = new ProductParser();
            var products = await parser.ParseCalorizatorAsync();

            int added = 0;
            int updated = 0;

            foreach (var p in products)
            {
                var existing = await _products.Query()
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == p.Name.ToLower());
                
                if (existing == null)
                {
                    p.Unit = "г"; // Calorizator даёт данные на 100г
                    await _products.AddAsync(p);
                    added++;
                }
                else
                {
                    // Обновляем только БЖУ, оставляем цену и другие поля
                    existing.CaloriesPer100 = p.CaloriesPer100;
                    existing.ProteinPer100 = p.ProteinPer100;
                    existing.FatPer100 = p.FatPer100;
                    existing.CarbsPer100 = p.CarbsPer100;
                    await _products.UpdateAsync(existing);
                    updated++;
                }
            }

            await _products.SaveChangesAsync();
            
            Console.WriteLine($"✅ Импорт завершен: добавлено {added}, обновлено {updated}");
        }

        /// <summary>
        /// Импорт продуктов из Пятёрочки (полный парсинг сайта)
        /// </summary>
        public async Task ImportFromPyaterochkaAsync()
        {
            Console.WriteLine("🔄 Запуск импорта из Пятёрочки (полный парсинг)...");
            
            using var parser = new VkusvillParser(maxConcurrentCategories: 3, maxConcurrentDetailPages: 15);
            var products = await parser.ParseAllProductsAsync(enrichDetails: true);


            int added = 0;
            int updated = 0;

            foreach (var p in products)
            {
                try
                {
                    // Нормализуем название для поиска
                    var normalizedName = NormalizeName(p.Name);
                    
                    var existing = await _products.Query()
                        .FirstOrDefaultAsync(x => NormalizeName(x.Name) == normalizedName);
                    
                    if (existing == null)
                    {
                        await _products.AddAsync(p);
                        added++;
                    }
                    else
                    {
                        // Обновляем все поля
                        existing.PricePerUnit = p.PricePerUnit > 0 ? p.PricePerUnit : existing.PricePerUnit;
                        existing.ImageUrl = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : existing.ImageUrl;
                        existing.CaloriesPer100 = p.CaloriesPer100 ?? existing.CaloriesPer100;
                        existing.ProteinPer100 = p.ProteinPer100 ?? existing.ProteinPer100;
                        existing.FatPer100 = p.FatPer100 ?? existing.FatPer100;
                        existing.CarbsPer100 = p.CarbsPer100 ?? existing.CarbsPer100;
                        
                        await _products.UpdateAsync(existing);
                        updated++;
                    }
                    
                    // Сохраняем периодически
                    if ((added + updated) % 50 == 0)
                    {
                        await _products.SaveChangesAsync();
                        Console.WriteLine($"   💾 Сохранено: {added + updated} продуктов");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка при обработке {p.Name}: {ex.Message}");
                }
            }

            await _products.SaveChangesAsync();
            
            Console.WriteLine($"✅ Импорт из Пятёрочки завершен:");
            Console.WriteLine($"   • Добавлено: {added}");
            Console.WriteLine($"   • Обновлено: {updated}");
        }

        /// <summary>
        /// Комбинированный импорт - УДАЛЕНО, используем только Пятёрочку
        /// </summary>
        public async Task ImportCombinedAsync()
        {
            // Теперь используем только Пятёрочку
            await ImportFromPyaterochkaAsync();
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