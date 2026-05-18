using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Services.Jobs;
using Services.Services;

namespace Presentation.Controllers;

[Route("Admin/[action]")]
public class AdminController : Controller
{
    private readonly SeedService              _seedService;
    private readonly PriceUpdateJob           _priceJob;
    private readonly ILogger<AdminController> _log;

    public AdminController(
        SeedService              seedService,
        PriceUpdateJob           priceJob,
        ILogger<AdminController> log)
    {
        _seedService = seedService;
        _priceJob    = priceJob;
        _log         = log;
    }

    // GET /Admin/Parser
    [HttpGet("/Admin/Parser")]
    public IActionResult Parser() => View();

    // POST /Admin/SeedProducts — импортирует статичную базу ~500 продуктов
    [HttpPost("/Admin/ParseAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedProducts()
    {
        if (!User.IsInRole("Admin"))
            return Json(new { error = "Доступ запрещён" });

        try
        {
            var (added, skipped) = await _seedService.SeedProductsAsync();
            return Json(new { success = true, added, skipped,
                totalSaved = added, // View ждёт result.totalSaved
                message = $"Готово! Добавлено: {added}, обновлено: {skipped}" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при seed продуктов");
            return Json(new { error = ex.Message });
        }
    }

    // POST /Admin/UpdatePrices — обновляет цены вручную
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrices(decimal minPct = 5, decimal maxPct = 15)
    {
        if (!User.IsInRole("Admin"))
            return Json(new { error = "Доступ запрещён" });

        if (minPct < 1 || maxPct > 50 || minPct > maxPct)
            return Json(new { error = "Некорректный диапазон процентов" });

        try
        {
            await _priceJob.ExecuteAsync(minPct, maxPct);
            return Json(new
            {
                success = true,
                message = $"Цены обновлены! Диапазон: ±{minPct}–{maxPct}%"
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка при обновлении цен");
            return Json(new { error = ex.Message });
        }
    }
}