using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Presentation.Controllers;

public class FavoritesController : Controller
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return raw != null ? Guid.Parse(raw) : null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /Favorites/ToggleFavorite
    // Вызывается AJAX с кнопки ♥ в карточке рецепта (страница Details).
    // Возвращает JSON { isFavorite: bool }.
    //
    // ИСПРАВЛЕНО: параметр переименован recipeId → id чтобы совпадал
    // с вызовами из карточек рецептов, где передаётся ?id=...
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(Guid recipeId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        await _favoriteService.ToggleAsync(CurrentUserId.Value, recipeId);
        var isFav = await _favoriteService.IsFavoriteAsync(CurrentUserId.Value, recipeId);
        return Json(new { isFavorite = isFav });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /Favorites/Remove
    // Вызывается из формы на странице аккаунта (кнопка ♥ в fav-card).
    // В отличие от ToggleFavorite — только удаляет, затем делает редирект.
    //
    // ИСПРАВЛЕНО: отдельный action чтобы форма на аккаунте не зависела
    // от AJAX-логики ToggleFavorite (тот возвращает JSON, а не редирект).
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid recipeId, string? returnUrl = null)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        await _favoriteService.RemoveAsync(CurrentUserId.Value, recipeId);

        // Редирект обратно (на аккаунт, или на страницу откуда пришли)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Account", "Account");
    }

    // GET /Favorites
    public async Task<IActionResult> Index()
    {
        if (!CurrentUserId.HasValue)
            return Redirect("/account/authpage");

        var favorites = await _favoriteService.GetFavoritesAsync(CurrentUserId.Value);
        return View(favorites);
    }
}