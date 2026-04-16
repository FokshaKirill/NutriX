using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Concurrent;
using HtmlAgilityPack;
using Domain.Entities;
using System.Globalization;

namespace Services.Helpers
{
    /// <summary>
    /// Быстрый параллельный парсер ВкусВилл.
    ///
    /// Что ускоряет:
    /// 1. Один общий HttpClient с пулом соединений (вместо нового на каждый запрос)
    /// 2. Параллельный обход страниц каждой категории (SemaphoreSlim)
    /// 3. Параллельный обход детальных страниц для БЖУ
    /// 4. Описание берётся прямо из карточки — лишний запрос не нужен
    /// 5. Категории обрабатываются параллельно (с ограничением)
    ///
    /// Настройки скорости (можно менять):
    ///   MaxConcurrentCategories  — сколько категорий одновременно (2-3)
    ///   MaxConcurrentDetailPages — сколько детальных страниц одновременно (5-10)
    ///   DelayBetweenPagesMs      — пауза между страницами одной категории (мс)
    /// </summary>
    public class VkusvillParser : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl    = "https://vkusvill.ru";
        private const string NoImageSvg = "no-image.svg";

        // ── Настройки параллелизма ────────────────────────────────────────────
        private readonly int _maxConcurrentCategories;
        private readonly int _maxConcurrentDetailPages;
        private readonly int _delayBetweenPagesMs;

        public VkusvillParser(
            int maxConcurrentCategories  = 2,
            int maxConcurrentDetailPages = 8,
            int delayBetweenPagesMs      = 500)
        {
            _maxConcurrentCategories  = maxConcurrentCategories;
            _maxConcurrentDetailPages = maxConcurrentDetailPages;
            _delayBetweenPagesMs      = delayBetweenPagesMs;

            // Один HttpClient на весь парсер — переиспользует TCP-соединения
            var handler = new HttpClientHandler
            {
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip |
                    System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect   = true,
                UseCookies          = true,
                MaxConnectionsPerServer = 20, // разрешаем много параллельных соединений
            };

            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПУБЛИЧНЫЙ ВХОД
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<VkusvillProduct>> ParseAllProductsAsync(bool enrichDetails = true)
        {
            var categories = new Dictionary<string, string>
            {
                { "molochnye-produkty-yaytso",  "Молочные продукты, яйцо" },
                // { "myaso-ptitsa",               "Мясо и птица" },
                // { "ryba-ikra-moreprodukty",     "Рыба, икра, морепродукты" },
                // { "ovoshchi-frukty-yagody",     "Овощи, фрукты, ягоды" },
                // { "bakaleya",                   "Бакалея" },
                // { "gotovaya-eda",               "Готовая еда" },
                // { "napitki",                    "Напитки" },
                // { "hleb-vypechka-sladosti",     "Хлеб, выпечка, сладости" },
                // { "zamorozhennye-produkty",     "Замороженные продукты" },
                // { "zdorovoe-pitanie",           "Здоровое питание" },
            };

            Console.WriteLine($"🔍 Парсинг ВкусВилл | параллельно: {_maxConcurrentCategories} кат., " +
                              $"{_maxConcurrentDetailPages} деталей");

            var allProducts = new ConcurrentBag<VkusvillProduct>();
            var semaphore   = new SemaphoreSlim(_maxConcurrentCategories);

            // Запускаем все категории параллельно (с ограничением)
            var tasks = categories.Select(async kv =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Console.WriteLine($"\n📦 Начинаем: {kv.Value}");
                    var products = await ParseCategoryAsync(kv.Key, kv.Value, enrichDetails);
                    foreach (var p in products) allProducts.Add(p);
                    Console.WriteLine($"   ✓ {kv.Value}: {products.Count} товаров");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ {kv.Value}: {ex.Message}");
                }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks);

            var result = allProducts.ToList();
            Console.WriteLine($"\n🎉 Итого: {result.Count} продуктов");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПАРСИНГ КАТЕГОРИИ
        // Сначала собираем все карточки со всех страниц, потом пачкой
        // загружаем детальные страницы параллельно.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<List<VkusvillProduct>> ParseCategoryAsync(
            string slug, string categoryName, bool enrichDetails)
        {
            // ── Шаг 1: узнаём общее число страниц ────────────────────────────
            var firstPageHtml = await FetchAsync($"{BaseUrl}/goods/{slug}/");
            if (firstPageHtml == null) return [];

            var firstDoc = ParseHtml(firstPageHtml);
            int totalPages = GetTotalPages(firstDoc);
            Console.WriteLine($"   [{categoryName}] страниц: {totalPages}");

            // ── Шаг 2: параллельно загружаем все страницы категории ──────────
            var pageHtmls = new ConcurrentDictionary<int, string>();
            pageHtmls[1] = firstPageHtml;

            if (totalPages > 1)
            {
                var pageSemaphore = new SemaphoreSlim(4); // до 4 страниц одновременно
                var pageTasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
                {
                    await pageSemaphore.WaitAsync();
                    try
                    {
                        await Task.Delay(_delayBetweenPagesMs); // вежливая пауза
                        var html = await FetchAsync($"{BaseUrl}/goods/{slug}/?PAGEN_1={page}");
                        if (html != null) pageHtmls[page] = html;
                    }
                    finally { pageSemaphore.Release(); }
                });
                await Task.WhenAll(pageTasks);
            }

            // ── Шаг 3: парсим карточки из всех страниц ───────────────────────
            var products = new List<VkusvillProduct>();
            foreach (var (_, html) in pageHtmls.OrderBy(x => x.Key))
            {
                var doc   = ParseHtml(html);
                var cards = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'ProductCard') " +
                    "and contains(@class,'js-product-cart') and @data-id]");

                if (cards == null) continue;

                foreach (var card in cards)
                {
                    var p = ParseProductCard(card, categoryName);
                    if (p != null) products.Add(p);
                }
            }

            Console.WriteLine($"   [{categoryName}] карточек собрано: {products.Count}");

            // ── Шаг 4: параллельно загружаем детальные страницы для БЖУ ──────
            if (enrichDetails && products.Count > 0)
                await EnrichBatchAsync(products);

            return products;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПАКЕТНОЕ ОБОГАЩЕНИЕ БЖУ (параллельно)
        // ─────────────────────────────────────────────────────────────────────

        private async Task EnrichBatchAsync(List<VkusvillProduct> products)
        {
            var semaphore = new SemaphoreSlim(_maxConcurrentDetailPages);
            int done = 0;

            var tasks = products
                .Where(p => !string.IsNullOrEmpty(p.DetailUrl))
                .Select(async product =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await EnrichWithNutritionAsync(product);
                        int n = Interlocked.Increment(ref done);
                        if (n % 20 == 0)
                            Console.WriteLine($"   [{product.CategoryName}] БЖУ: {n}/{products.Count}");
                    }
                    finally { semaphore.Release(); }
                });

            await Task.WhenAll(tasks);
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПАРСИНГ ОДНОЙ КАРТОЧКИ (без изменений по структуре)
        // ─────────────────────────────────────────────────────────────────────

        private static VkusvillProduct? ParseProductCard(HtmlNode card, string categoryName)
        {
            // ID товара
            var externalId = card.GetAttributeValue("data-id", null)
                          ?? card.GetAttributeValue("data-xmlid", null);

            // Название: <a class="ProductCard__link"><span itemprop="name">
            var nameNode = card.SelectSingleNode(
                ".//a[contains(@class,'ProductCard__link')]//span[@itemprop='name']");
            var name = CleanText(nameNode?.InnerText);
            if (string.IsNullOrWhiteSpace(name)) return null;

            // Описание: <span itemprop="description" class="hidden">
            var description = CleanText(
                card.SelectSingleNode(".//span[@itemprop='description']")?.InnerText);

            // Ссылка на детальную страницу
            var linkHref =
                card.SelectSingleNode(".//a[contains(@class,'ProductCard__imageLink')]")
                    ?.GetAttributeValue("href", null)
                ?? card.SelectSingleNode(".//a[contains(@class,'ProductCard__link')]")
                    ?.GetAttributeValue("href", null);
            var detailUrl = BuildAbsoluteUrl(linkHref);

            // Цена: <meta itemprop="price" content="155">
            var priceMeta = card.SelectSingleNode(
                ".//div[@itemprop='offers']//meta[@itemprop='price']");
            var priceStr  = priceMeta?.GetAttributeValue("content", null)
                         ?? card.SelectSingleNode(
                                ".//span[contains(@class,'js-datalayer-catalog-list-price')" +
                                " and not(contains(@class,'old'))]")?.InnerText;
            var price = ParseDecimal(priceStr ?? "0");

            // Единица из строки цены: «155 руб/шт», «720 руб/кг»
            var priceLabel   = CleanText(
                card.SelectSingleNode(".//span[contains(@class,'Price--label')]")?.InnerText ?? "");
            var unitFromPrice = ExtractUnitFromPriceText(priceLabel);

            // Вес: <div class="ProductCard__weight">140 г</div>
            var weightText = CleanText(
                card.SelectSingleNode(".//div[contains(@class,'ProductCard__weight')]")
                    ?.InnerText ?? "");
            var unit = !string.IsNullOrWhiteSpace(weightText)
                ? DetermineUnit(weightText)
                : unitFromPrice;

            // Изображение: data-src (lazyload) → src
            var imgNode  = card.SelectSingleNode(".//img[contains(@class,'ProductCard__imageImg')]");
            var imageUrl = imgNode?.GetAttributeValue("data-src", null)
                        ?? imgNode?.GetAttributeValue("src", null);
            if (imageUrl != null && imageUrl.Contains(NoImageSvg)) imageUrl = null;
            imageUrl = BuildAbsoluteUrl(imageUrl);

            return new VkusvillProduct
            {
                Id           = Guid.NewGuid(),
                Name         = name,
                Description  = description,
                Unit         = unit,
                PricePerUnit = price,
                ImageUrl     = imageUrl,
                DetailUrl    = detailUrl,
                ExternalId   = externalId,
                CategoryName = categoryName,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // ОБОГАЩЕНИЕ БЖУ С ДЕТАЛЬНОЙ СТРАНИЦЫ
        // ─────────────────────────────────────────────────────────────────────

        public async Task EnrichWithNutritionAsync(VkusvillProduct product)
        {
            if (string.IsNullOrEmpty(product.DetailUrl)) return;

            var html = await FetchAsync(product.DetailUrl);
            if (html == null) return;

            var doc = ParseHtml(html);

            if (string.IsNullOrEmpty(product.Description))
                product.Description = ParseDetailDescription(doc);

            ParseNutrition(doc, product);
        }

        private static string? ParseDetailDescription(HtmlDocument doc)
        {
            string[] xpaths =
            {
                "//div[@itemprop='description']",
                "//div[contains(@class,'ProductDetail__description')]",
                "//div[contains(@class,'Description__text')]",
                "//meta[@name='description']"
            };
            foreach (var xpath in xpaths)
            {
                var node = doc.DocumentNode.SelectSingleNode(xpath);
                if (node == null) continue;
                if (node.Name == "meta")
                {
                    var c = node.GetAttributeValue("content", null);
                    if (!string.IsNullOrWhiteSpace(c)) return CleanText(c);
                    continue;
                }
                var text = CleanText(node.InnerText);
                if (text.Length > 10) return text;
            }
            return null;
        }

        private static void ParseNutrition(HtmlDocument doc, VkusvillProduct product)
        {
            // Новый блок — VV23_DetailProdPageAccordion__Energy
            var energyBlock = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'VV23_DetailProdPageAccordion__Energy')]");

            if (energyBlock != null)
            {
                var items = energyBlock.SelectNodes(
                    ".//div[contains(@class,'VV23_DetailProdPageAccordion__EnergyItem')]");

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        var valueNode = item.SelectSingleNode(
                            ".//div[contains(@class,'VV23_DetailProdPageAccordion__EnergyValue')]");
                        var descNode = item.SelectSingleNode(
                            ".//div[contains(@class,'VV23_DetailProdPageAccordion__EnergyDesc')]");

                        if (valueNode == null || descNode == null) continue;

                        var value  = ParseDecimal(valueNode.InnerText);
                        var desc   = CleanText(descNode.InnerText).ToLower();

                        if (ContainsAny(desc, "ккал", "калори", "энергет")) product.CaloriesPer100 ??= value;
                        else if (ContainsAny(desc, "белк"))                  product.ProteinPer100  ??= value;
                        else if (ContainsAny(desc, "жир"))                   product.FatPer100      ??= value;
                        else if (ContainsAny(desc, "углевод"))               product.CarbsPer100    ??= value;
                    }
                }
            }

            // Старые fallback-ы
            var nutritionRoot = doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class,'nutrition')] | " +
                "//div[contains(@class,'NutritionFacts')] | " +
                "//div[contains(@class,'Nutrition')]");

            if (nutritionRoot != null)
            {
                ExtractNutritionFromNode(nutritionRoot, product);
                if (product.CaloriesPer100.HasValue) return;
            }

            if (!product.CaloriesPer100.HasValue)
                ExtractNutritionFromText(doc.DocumentNode.InnerText, product);

            if (!product.CaloriesPer100.HasValue)
                TryParseJsonLd(doc, product);
        }

        private static void ExtractNutritionFromNode(HtmlNode root, VkusvillProduct product)
        {
            ExtractNutritionFromText(root.InnerText, product);
            var rows = root.SelectNodes(".//tr | .//div[contains(@class,'row')]");
            if (rows == null) return;
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td | .//span | .//div");
                if (cells == null || cells.Count < 2) continue;
                var key   = CleanText(cells[0].InnerText).ToLower();
                var value = ParseDecimal(cells[^1].InnerText);
                if (ContainsAny(key, "энергет", "калори", "ккал")) product.CaloriesPer100 ??= value;
                else if (ContainsAny(key, "белк"))                  product.ProteinPer100  ??= value;
                else if (ContainsAny(key, "жир"))                   product.FatPer100      ??= value;
                else if (ContainsAny(key, "углевод"))               product.CarbsPer100    ??= value;
            }
        }

        private static void ExtractNutritionFromText(string text, VkusvillProduct product)
        {
            product.CaloriesPer100 ??= RegexExtract(text, @"(?:энергетическ[^\d]*ценность|калории?|ккал)[^\d]*(\d+(?:[.,]\d+)?)");
            product.ProteinPer100  ??= RegexExtract(text, @"белк[иаоу]?[^\d]*(\d+(?:[.,]\d+)?)");
            product.FatPer100      ??= RegexExtract(text, @"жир[ыа]?[^\d]*(\d+(?:[.,]\d+)?)");
            product.CarbsPer100    ??= RegexExtract(text, @"углевод[ыа]?[^\d]*(\d+(?:[.,]\d+)?)");
        }

        private static decimal? RegexExtract(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? ParseDecimal(m.Groups[1].Value) : null;
        }

        private static void TryParseJsonLd(HtmlDocument doc, VkusvillProduct product)
        {
            var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (scripts == null) return;
            foreach (var script in scripts)
            {
                try
                {
                    using var jDoc = JsonDocument.Parse(script.InnerText);
                    var root = jDoc.RootElement;
                    if (!root.TryGetProperty("nutrition", out var nutrition) &&
                        !(root.TryGetProperty("@graph", out var graph) && TryFindInGraph(graph, "nutrition", out nutrition)))
                        continue;
                    product.CaloriesPer100 ??= JsonDecimal(nutrition, "calories");
                    product.ProteinPer100  ??= JsonDecimal(nutrition, "proteinContent");
                    product.FatPer100      ??= JsonDecimal(nutrition, "fatContent");
                    product.CarbsPer100    ??= JsonDecimal(nutrition, "carbohydrateContent");
                }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ПАГИНАЦИЯ
        // ─────────────────────────────────────────────────────────────────────

        private static int GetTotalPages(HtmlDocument doc)
        {
            // <input id="js-catalog-page-param-total-products" value="509">
            // На странице 24 товара (по умолчанию), считаем страницы
            var totalNode = doc.DocumentNode.SelectSingleNode(
                "//input[@id='js-catalog-page-param-total-products']");
            if (totalNode != null &&
                int.TryParse(totalNode.GetAttributeValue("value", "0"), out var total))
            {
                return (int)Math.Ceiling(total / 24.0); // 24 товара на странице
            }

            // Fallback: ищем последний номер страницы в пагинаторе
            // <a class="VV_Pager__Item" data-page="22">22</a>
            var pagerItems = doc.DocumentNode.SelectNodes(
                "//a[contains(@class,'VV_Pager__Item') and @data-page]");
            if (pagerItems != null)
            {
                var maxPage = pagerItems
                    .Select(n => int.TryParse(n.GetAttributeValue("data-page", "0"), out var p) ? p : 0)
                    .DefaultIfEmpty(1)
                    .Max();
                if (maxPage > 1) return maxPage;
            }

            return 1; // если пагинатора нет — одна страница
        }

        // ─────────────────────────────────────────────────────────────────────
        // HTTP HELPER (с retry)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string?> FetchAsync(string url, int retries = 2)
        {
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    return await _httpClient.GetStringAsync(url);
                }
                catch (HttpRequestException ex) when (attempt < retries)
                {
                    Console.WriteLine($"   ↺ Retry {attempt + 1} для {url}: {ex.Message}");
                    await Task.Delay(1000 * (attempt + 1)); // экспоненциальная пауза
                }
                catch (TaskCanceledException) when (attempt < retries)
                {
                    Console.WriteLine($"   ↺ Timeout retry {attempt + 1} для {url}");
                    await Task.Delay(2000);
                }
            }
            Console.WriteLine($"   ❌ Не удалось загрузить: {url}");
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ─────────────────────────────────────────────────────────────────────

        private static HtmlDocument ParseHtml(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        private static string? BuildAbsoluteUrl(string? href)
        {
            if (string.IsNullOrEmpty(href)) return null;
            return href.StartsWith("http") ? href : BaseUrl + href;
        }

        private static string ExtractUnitFromPriceText(string priceText)
        {
            var match = Regex.Match(priceText, @"/(\S+)");
            if (!match.Success) return "шт";
            return match.Groups[1].Value.ToLower().TrimEnd('.') switch
            {
                "кг" => "кг",
                "л"  => "л",
                "мл" => "мл",
                "г"  => "г",
                _    => "шт"
            };
        }

        private static string DetermineUnit(string text)
        {
            text = text.ToLower();
            if (text.Contains("кг")) return "кг";
            if (text.Contains("мл")) return "мл";
            if (Regex.IsMatch(text, @"\d\s*г\b")) return "г";
            if (Regex.IsMatch(text, @"\d\s*л\b")) return "л";
            return "шт";
        }

        private static string CleanText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = System.Net.WebUtility.HtmlDecode(raw);
            return Regex.Replace(raw, @"\s+", " ").Trim();
        }

        private static decimal ParseDecimal(string value)
        {
            value = Regex.Replace(value ?? "", @"[^\d.,]", "").Replace(",", ".").Trim();
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0m;
        }

        private static bool ContainsAny(string source, params string[] values)
            => values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static bool TryFindInGraph(JsonElement graph, string key, out JsonElement found)
        {
            found = default;
            if (graph.ValueKind != JsonValueKind.Array) return false;
            foreach (var el in graph.EnumerateArray())
                if (el.TryGetProperty(key, out found)) return true;
            return false;
        }

        private static decimal? JsonDecimal(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return null;
            var raw = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
            return string.IsNullOrEmpty(raw) ? null : ParseDecimal(raw);
        }

        public void Dispose() => _httpClient.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // МОДЕЛЬ
    // ─────────────────────────────────────────────────────────────────────────

    public class VkusvillProduct : Product
    {
        public string? DetailUrl    { get; set; }
        public string? Description  { get; set; }
        public string? ExternalId   { get; set; }
        public string? CategoryName { get; set; }
    }
}