using Microsoft.AspNetCore.Mvc;
using Services.Helpers;

namespace Presentation.Controllers
{
    public class PriceRuController : Controller
    {
        private readonly PriceRuParser _parser;
        public PriceRuController(PriceRuParser parser) => _parser = parser;

        public IActionResult Index() => View();

        [HttpGet("PriceRu/Api/categories")]
        public async Task<IActionResult> ApiCategories()
        {
            try
            {
                var cats = await _parser.GetCategoriesAsync();
                return Json(cats.Select(c => new { slug = c.Slug, name = c.Name }));
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("PriceRu/Api/products")]
        public async Task<IActionResult> ApiProducts([FromBody] ProductsReq req)
        {
            try
            {
                var json = await _parser.ProxyGetProductsAsync(req.Slug, req.Page);
                return Content(json, "application/json");
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("PriceRu/Api/search")]
        public async Task<IActionResult> ApiSearch([FromBody] SearchReq req)
        {
            try
            {
                var json = await _parser.SearchAsync(req.Query, req.Page);
                return Content(json, "application/json");
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }

    public record ProductsReq(string Slug, int Page = 1);
    public record SearchReq(string Query, int Page = 1);
}