using Microsoft.AspNetCore.Mvc;
using Services.Helpers;

namespace Presentation.Controllers
{
    public class ParserTestController : Controller
    {
        private readonly PyaterochkaParser _parser;

        public ParserTestController(PyaterochkaParser parser)
            => _parser = parser;

        // GET /ParserTest
        public IActionResult Index() => View();

        // POST /ParserTest/Api/init
        [HttpPost("ParserTest/Api/init")]
        public async Task<IActionResult> ApiInit()
        {
            try
            {
                await _parser.InitializeStoreAsync();
                return Json(new { success = true, storeId = _parser.StoreId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST /ParserTest/Api/products
        [HttpPost("ParserTest/Api/products")]
        public async Task<IActionResult> ApiProducts([FromBody] ProductsRequest req)
        {
            try
            {
                var result = await _parser.ProxyGetAsync(
                    $"catalog/products_list/?category_id={req.CategoryId}" +
                    $"&sap_code_store_id={_parser.StoreId}" +
                    $"&page={req.Page}&records_per_page=48");

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // POST /ParserTest/Api/product
        [HttpPost("ParserTest/Api/product")]
        public async Task<IActionResult> ApiProduct([FromBody] ProductDetailRequest req)
        {
            try
            {
                var result = await _parser.ProxyGetAsync(
                    $"catalog/product/info/?plu_id={req.Plu}" +
                    $"&sap_code_store_id={_parser.StoreId}");

                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public record ProductsRequest(string CategoryId, int Page);
    public record ProductDetailRequest(string Plu);
}