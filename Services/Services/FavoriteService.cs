using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IRepository<FavoriteRecipe> _favRepo;
    private readonly IRepository<Recipe> _recipeRepo;

    public FavoriteService(
        IRepository<FavoriteRecipe> favRepo, 
        IRepository<Recipe> recipeRepo)
    {
        _favRepo    = favRepo;
        _recipeRepo = recipeRepo;
    }

    public async Task<IEnumerable<Recipe>> GetFavoritesAsync(Guid userId)
    {
        return await _favRepo.Query()
            .Where(f => f.UserId == userId)
            .Include(f => f.Recipe)
            .ThenInclude(r => r.Ingredients)
            .ThenInclude(i => i.Product)
            .Select(f => f.Recipe)
            .ToListAsync();
    }

    public async Task<bool> IsFavoriteAsync(Guid userId, Guid recipeId)
    {
        return await _favRepo.Query()
            .AnyAsync(f => f.UserId == userId && f.RecipeId == recipeId);
    }

    public async Task AddAsync(Guid userId, Guid recipeId)
    {
        var exists = await IsFavoriteAsync(userId, recipeId);
        if (exists) return;

        await _favRepo.AddAsync(new FavoriteRecipe
        {
            UserId   = userId,
            RecipeId = recipeId,
            AddedAt  = DateTime.UtcNow
        });
        
        await _favRepo.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid userId, Guid recipeId)
    {
        var fav = await _favRepo.Query()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId);

        if (fav != null)
        {
            await _favRepo.DeleteAsync(fav);
            await _favRepo.SaveChangesAsync();
        }
    }

    public async Task ToggleAsync(Guid userId, Guid recipeId)
    {
        if (await IsFavoriteAsync(userId, recipeId))
            await RemoveAsync(userId, recipeId);
        else
            await AddAsync(userId, recipeId);
    }
}