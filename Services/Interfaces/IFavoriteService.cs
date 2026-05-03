namespace Services.Interfaces
{
    public interface IFavoriteService
    {
        Task<IEnumerable<Recipe>> GetFavoritesAsync(Guid userId);
        Task<bool> IsFavoriteAsync(Guid userId, Guid recipeId);
        Task AddAsync(Guid userId, Guid recipeId);
        Task RemoveAsync(Guid userId, Guid recipeId);
        Task ToggleAsync(Guid userId, Guid recipeId); // добавить если нет, убрать если есть
    }
}
