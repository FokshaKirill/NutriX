using Domain.Entities;
using Domain.Enums;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Services.Services
{
    public class SeedService
    {
        private readonly IRepository<Product> _productRepo;
        private readonly ILogger<SeedService> _logger;

        public SeedService(IRepository<Product> productRepo, ILogger<SeedService> logger)
        {
            _productRepo = productRepo;
            _logger = logger;
        }

        public async Task<(int Added, int Updated)> SeedProductsAsync()
        {
            string htmlPath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "data", "PDB.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("Файл PDB.html не найден", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            var productsFromHtml = ParseHtml(html);

            var existingProducts = await _productRepo.Query().ToListAsync();
            var existingDict = existingProducts.ToDictionary(p => p.Name.ToLower().Trim());

            int added = 0, updated = 0;

            foreach (var p in productsFromHtml)
            {
                var key = p.Name.ToLower().Trim();

                if (existingDict.TryGetValue(key, out var existing))
                {
                    existing.PricePerUnit   = p.PricePerUnit;
                    existing.CaloriesPer100 = p.CaloriesPer100;
                    existing.ProteinPer100  = p.ProteinPer100;
                    existing.FatPer100      = p.FatPer100;
                    existing.CarbsPer100    = p.CarbsPer100;
                    existing.Unit           = p.Unit;
                    existing.Category       = p.Category;
                    existing.UpdatedAt      = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    await _productRepo.AddAsync(p);
                    added++;
                }
            }

            await _productRepo.SaveChangesAsync();
            _logger.LogInformation("Sync из HTML завершён. Добавлено: {Added}, Обновлено: {Updated}", added, updated);
            return (added, updated);
        }

        /// <summary>
        /// Парсит PDB.html и обновляет ТОЛЬКО цены продуктов, не трогая остальные поля.
        /// </summary>
        public async Task<int> UpdatePricesOnlyAsync()
        {
            string htmlPath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "data", "PDB.html");

            if (!File.Exists(htmlPath))
                throw new FileNotFoundException("Файл PDB.html не найден", htmlPath);

            var html = await File.ReadAllTextAsync(htmlPath);
            var productsFromHtml = ParseHtml(html);

            var existingProducts = await _productRepo.Query().ToListAsync();
            var existingDict = existingProducts.ToDictionary(p => p.Name.ToLower().Trim());

            int updated = 0;

            foreach (var p in productsFromHtml)
            {
                var key = p.Name.ToLower().Trim();

                if (existingDict.TryGetValue(key, out var existing))
                {
                    existing.PricePerUnit = p.PricePerUnit;
                    existing.UpdatedAt    = DateTime.UtcNow;
                    updated++;
                }
            }

            await _productRepo.SaveChangesAsync();
            _logger.LogInformation("Обновление цен из HTML завершено. Обновлено: {Updated}", updated);
            return updated;
        }

        private List<Product> ParseHtml(string html)
        {
            var products = new List<Product>();

            // Берём только строки внутри <tbody>
            var tbodyMatch = Regex.Match(html, @"<tbody>(.*?)</tbody>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!tbodyMatch.Success) return products;

            var rowMatches = Regex.Matches(tbodyMatch.Groups[1].Value,
                @"<tr>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match row in rowMatches)
            {
                var cells = Regex.Matches(row.Groups[1].Value,
                    @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline);

                if (cells.Count < 9) continue;

                // [0]=ID, [1]=Название, [2]=Категория, [3]=Единица,
                // [4]=Цена, [5]=Ккал, [6]=Белки, [7]=Жиры, [8]=Углеводы
                var name     = Clean(cells[1].Groups[1].Value);
                var catStr   = Clean(cells[2].Groups[1].Value);
                var unit     = Clean(cells[3].Groups[1].Value);
                var price    = ParseDec(cells[4].Groups[1].Value);
                var kcal     = ParseDec(cells[5].Groups[1].Value);
                var protein  = ParseDec(cells[6].Groups[1].Value);
                var fat      = ParseDec(cells[7].Groups[1].Value);
                var carbs    = ParseDec(cells[8].Groups[1].Value);

                if (string.IsNullOrWhiteSpace(name)) continue;

                products.Add(new Product
                {
                    Id            = Guid.NewGuid(),
                    Name          = name,
                    Category      = GetCategory(catStr),
                    Unit          = unit,
                    PricePerUnit  = price ?? 0,
                    CaloriesPer100 = kcal,
                    ProteinPer100  = protein,
                    FatPer100      = fat,
                    CarbsPer100    = carbs,
                    Source        = "PDB.html",
                    CreatedAt     = DateTime.UtcNow
                });
            }

            return products;
        }

        private string Clean(string s) =>
            System.Web.HttpUtility.HtmlDecode(
                Regex.Replace(s, @"<[^>]+>", "").Trim()); 

        private decimal? ParseDec(string s) =>
            decimal.TryParse(
                Clean(s).Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var d) ? d : null;

        private ProductCategory GetCategory(string cat) => cat.ToLower().Trim() switch
        {
            "мясо"             => ProductCategory.Meat,
            "птица"            => ProductCategory.Poultry,
            "рыба"             => ProductCategory.Fish,
            "молочное"         => ProductCategory.Dairy,
            "яйца"             => ProductCategory.Eggs,
            "крупы"            => ProductCategory.Grains,
            "хлеб и макароны"  => ProductCategory.Bread,
            "овощи"            => ProductCategory.Vegetables,
            "фрукты"           => ProductCategory.Fruits,
            "орехи"            => ProductCategory.Nuts,
            "масла"            => ProductCategory.Oils,
            "специи"           => ProductCategory.Spices,
            "сладкое"          => ProductCategory.Sweets,
            "консервы"         => ProductCategory.Canned,
            "полуфабрикаты"    => ProductCategory.SemiFinished,
            "соевые"           => ProductCategory.Soy,
            "напитки"          => ProductCategory.Beverages,
            _                  => ProductCategory.Other
        };
    }
}