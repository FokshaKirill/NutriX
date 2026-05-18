using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Services.Interfaces
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();

        Task<(List<Product> Products, int TotalCount)> GetPagedProductsAsync(
            int page,
            int pageSize,
            string? searchTerm = null,
            ProductCategory? category = null,
            decimal? minCalories = null,
            decimal? maxCalories = null,
            decimal? minPrice = null,
            decimal? maxPrice = null);

        Task<Product?> GetByIdAsync(Guid id);

        Task<Product> CreateAsync(Product product);

        Task UpdateAsync(Product product);

        Task DeleteAsync(Guid id);

        /// <summary>
        /// Получить родительские категории (для создания продукта)
        /// </summary>
        Task<List<Product>> GetCategoriesAsync();

        /// <summary>
        /// Получить Enum-категории для фильтра в виде SelectList
        /// </summary>
        Task<List<SelectListItem>> GetCategorySelectListAsync();
    }
}