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

    // POST /Favorites/ToggleFavorite
    // Вызывается с кнопки ♥ в карточке рецепта и на странице аккаунта
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(Guid id)
    {
        if (!CurrentUserId.HasValue)
            return Unauthorized();

        await _favoriteService.ToggleAsync(CurrentUserId.Value, id);

        var isFav = await _favoriteService.IsFavoriteAsync(CurrentUserId.Value, id);

        return Json(new { isFavorite = isFav });
    }

    // GET /Favorites — страница избранного (опционально)
    public async Task<IActionResult> Index()
    {
        if (!CurrentUserId.HasValue)
            return Redirect("/account/authpage");

        var favorites = await _favoriteService.GetFavoritesAsync(CurrentUserId.Value);
        return View(favorites);
    }
}