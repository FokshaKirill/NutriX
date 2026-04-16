using Microsoft.AspNetCore.Mvc;
using Services.Helpers;

namespace Presentation.Controllers
{
    /// <summary>
    /// Контроллер для тестирования парсера Пятёрочки
    /// </summary>
    public class ParserTestController : Controller
    {
        // GET: /ParserTest
        public IActionResult Index()
        {
            return View();
        }

        // GET: /ParserTest/TestSingleProduct?url=...
        public async Task<IActionResult> TestSingleProduct(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Json(new 
                { 
                    success = false, 
                    error = "URL не указан" 
                });
            }

            try
            {
                var parser = new PyaterochkaParser();
                var product = await parser.ParseProductByUrlAsync(url);

                if (product == null)
                {
                    return Json(new 
                    { 
                        success = false, 
                        error = "Не удалось спарсить продукт" 
                    });
                }

                return Json(new
                {
                    success = true,
                    product = new
                    {
                        product.Name,
                        Price = $"{product.PricePerUnit}₽",
                        Unit = product.Unit,
                        Calories = product.CaloriesPer100,
                        Protein = product.ProteinPer100,
                        Fat = product.FatPer100,
                        Carbs = product.CarbsPer100,
                        HasImage = !string.IsNullOrEmpty(product.ImageUrl),
                        product.ImageUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // GET: /ParserTest/TestCategory?category=moloko-syr-yajca
        public async Task<IActionResult> TestCategory(string category = "moloko-syr-yajca")
        {
            try
            {
                var parser = new PyaterochkaParser();
                
                // Парсим только первую страницу для теста
                var url = $"https://5ka.ru/catalog/{category}";
                var html = await new HttpClient().GetStringAsync(url);
                
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Ищем карточки товаров
                var productCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-card')]")
                                  ?? doc.DocumentNode.SelectNodes("//article[contains(@class, 'product')]")
                                  ?? doc.DocumentNode.SelectNodes("//div[@data-product-id]");

                if (productCards == null)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Карточки товаров не найдены",
                        hint = "Возможно структура сайта изменилась",
                        htmlSample = html.Substring(0, Math.Min(500, html.Length))
                    });
                }

                var results = new List<object>();
                
                foreach (var card in productCards.Take(5)) // Берем первые 5 для теста
                {
                    try
                    {
                        var nameNode = card.SelectSingleNode(".//span[@class='product-name']")
                                      ?? card.SelectSingleNode(".//h3")
                                      ?? card.SelectSingleNode(".//a[@class='product-link']");
                        
                        var priceNode = card.SelectSingleNode(".//span[@class='product-price']")
                                       ?? card.SelectSingleNode(".//div[contains(@class, 'price')]");

                        results.Add(new
                        {
                            name = nameNode?.InnerText.Trim() ?? "Не найдено",
                            price = priceNode?.InnerText.Trim() ?? "Не найдено",
                            hasImage = card.SelectSingleNode(".//img") != null
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            error = ex.Message
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    category,
                    totalCards = productCards.Count,
                    parsedSamples = results
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // GET: /ParserTest/InspectHtml?category=moloko-syr-yajca
        public async Task<IActionResult> InspectHtml(string category = "moloko-syr-yajca")
        {
            try
            {
                var url = $"https://5ka.ru/catalog/{category}";
                var html = await new HttpClient().GetStringAsync(url);
                
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Content($"Ошибка: {ex.Message}", "text/plain");
            }
        }

        // GET: /ParserTest/FindSelectors?category=moloko-syr-yajca
        public async Task<IActionResult> FindSelectors(string category = "moloko-syr-yajca")
        {
            try
            {
                var url = $"https://5ka.ru/catalog/{category}";
                var html = await new HttpClient().GetStringAsync(url);
                
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var analysis = new
                {
                    // Различные варианты селекторов для поиска карточек товаров
                    possibleProductCardSelectors = new
                    {
                        productCard = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-card')]")?.Count ?? 0,
                        productItem = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-item')]")?.Count ?? 0,
                        productBox = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-box')]")?.Count ?? 0,
                        article = doc.DocumentNode.SelectNodes("//article")?.Count ?? 0,
                        dataProductId = doc.DocumentNode.SelectNodes("//div[@data-product-id]")?.Count ?? 0,
                        dataTestId = doc.DocumentNode.SelectNodes("//div[@data-testid]")?.Count ?? 0
                    },
                    
                    // Классы, которые встречаются в HTML
                    commonClasses = doc.DocumentNode.Descendants()
                        .Where(n => n.Attributes["class"] != null)
                        .SelectMany(n => n.Attributes["class"].Value.Split(' '))
                        .Where(c => c.Contains("product", StringComparison.OrdinalIgnoreCase) 
                                 || c.Contains("card", StringComparison.OrdinalIgnoreCase)
                                 || c.Contains("item", StringComparison.OrdinalIgnoreCase))
                        .Distinct()
                        .Take(20)
                        .ToList(),
                    
                    // Количество различных элементов
                    totalDivs = doc.DocumentNode.SelectNodes("//div")?.Count ?? 0,
                    totalArticles = doc.DocumentNode.SelectNodes("//article")?.Count ?? 0,
                    totalImages = doc.DocumentNode.SelectNodes("//img")?.Count ?? 0
                };

                return Json(analysis);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}

// Пример использования в браузере:
//
// 1. Проверка структуры HTML:
//    https://localhost:5001/ParserTest/InspectHtml?category=moloko-syr-yajca
//
// 2. Анализ селекторов:
//    https://localhost:5001/ParserTest/FindSelectors?category=moloko-syr-yajca
//
// 3. Тест парсинга категории:
//    https://localhost:5001/ParserTest/TestCategory?category=moloko-syr-yajca
//
// 4. Тест парсинга одного продукта:
//    https://localhost:5001/ParserTest/TestSingleProduct?url=https://5ka.ru/product/...
//
// Другие категории для тестирования:
// - moloko-syr-yajca (Молоко, сыр, яйца)
// - myaso-ptitsa-kolbasa (Мясо, птица, колбаса)
// - ovoshchi-frukty-yagody (Овощи, фрукты)