using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using HtmlAgilityPack;
using Domain.Entities;

namespace Services
{
    public class ProductParser
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<List<Product>> ParseCalorizatorAsync()
        {
            var products = new List<Product>();
            
            for (int page = 0; page < 87; page++)
            {
                try
                {
                    string url = page == 0 
                        ? "https://calorizator.ru/product/all" 
                        : $"https://calorizator.ru/product/all?page={page}";
                    
                    Console.WriteLine($"Загружаем страницу: {url}");
                    string html = await _httpClient.GetStringAsync(url);
                    
                    var pageProducts = ParsePageWithHtmlAgilityPack(html);
                    
                    if (pageProducts.Count == 0)
                    {
                        Console.WriteLine("Продукты не найдены, пробуем regex метод...");
                        pageProducts = ParsePageWithRegex(html);
                    }
                    
                    Console.WriteLine($"Найдено продуктов на странице {page}: {pageProducts.Count}");
                    products.AddRange(pageProducts);
                    
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при парсинге страницы {page}: {ex.Message}");
                    break;
                }
            }

            return products;
        }

        private List<Product> ParsePageWithHtmlAgilityPack(string html)
        {
            var products = new List<Product>();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Ищем все строки таблицы
            var rows = htmlDoc.DocumentNode.SelectNodes("//table//tr");
            
            if (rows == null)
            {
                Console.WriteLine("Таблица не найдена");
                return products;
            }

            Console.WriteLine($"Найдено строк в таблице: {rows.Count}");

            foreach (var row in rows)
            {
                try
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 6) continue;

                    var nameNode = cells[1].SelectSingleNode(".//a");
                    if (nameNode == null) continue;

                    var name = CleanText(nameNode.InnerText);
                    var proteinStr = CleanText(cells[2].InnerText);
                    var fatStr = CleanText(cells[3].InnerText);
                    var carbsStr = CleanText(cells[4].InnerText);
                    var caloriesStr = CleanText(cells[5].InnerText);

                    if (TryParseDecimal(proteinStr, out var protein) &&
                        TryParseDecimal(fatStr, out var fat) &&
                        TryParseDecimal(carbsStr, out var carbs) &&
                        TryParseDecimal(caloriesStr, out var calories))
                    {
                        products.Add(new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            CaloriesPer100 = calories,
                            ProteinPer100 = protein,
                            FatPer100 = fat,
                            CarbsPer100 = carbs,
                            Unit = "100 г",
                            PricePerUnit = 0,
                            ParentId = null,
                            ImageUrl = null
                        });
                        
                        Console.WriteLine($"✓ {name}: Б{protein} Ж{fat} У{carbs} К{calories}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки строки: {ex.Message}");
                }
            }

            return products;
        }

        private List<Product> ParsePageWithRegex(string html)
        {
            var products = new List<Product>();

            var pattern = @"<a[^>]+>([^<]+)</a>\s*</td>\s*<td[^>]*>([\d.,]+)</td>\s*<td[^>]*>([\d.,]+)</td>\s*<td[^>]*>([\d.,]+)</td>\s*<td[^>]*>([\d.,]+)</td>";
            
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            Console.WriteLine($"Regex найдено совпадений: {matches.Count}");

            foreach (Match match in matches)
            {
                try
                {
                    var name = CleanText(match.Groups[1].Value);
                    var proteinStr = match.Groups[2].Value.Trim();
                    var fatStr = match.Groups[3].Value.Trim();
                    var carbsStr = match.Groups[4].Value.Trim();
                    var caloriesStr = match.Groups[5].Value.Trim();

                    if (TryParseDecimal(proteinStr, out var protein) &&
                        TryParseDecimal(fatStr, out var fat) &&
                        TryParseDecimal(carbsStr, out var carbs) &&
                        TryParseDecimal(caloriesStr, out var calories))
                    {
                        products.Add(new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            CaloriesPer100 = calories,
                            ProteinPer100 = protein,
                            FatPer100 = fat,
                            CarbsPer100 = carbs,
                            Unit = "100 г",
                            PricePerUnit = 0,
                            ParentId = null,
                            ImageUrl = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка regex обработки: {ex.Message}");
                }
            }

            return products;
        }

        private bool TryParseDecimal(string str, out decimal result)
        {
            str = str.Trim().Replace(",", ".");
            return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        private string CleanText(string text)
        {
            text = Regex.Replace(text, @"\s+", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            return text.Trim();
        }
    }
}