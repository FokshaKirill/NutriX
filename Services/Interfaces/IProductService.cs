using Domain.Entities;

namespace Services.Interfaces
{
    public interface IProductService
    {
        /// <summary>
        /// Получить все продукты
        /// </summary>
        Task<List<Product>> GetAllProductsAsync();

        /// <summary>
        /// Получить продукты с пагинацией и фильтрами
        /// </summary>
        Task<(List<Product> Products, int TotalCount)> GetPagedProductsAsync(
            int page,
            int pageSize,
            string? searchTerm = null,
            Guid? categoryId = null,
            decimal? minCalories = null,
            decimal? maxCalories = null,
            decimal? minPrice = null,
            decimal? maxPrice = null);

        /// <summary>
        /// Получить продукт по ID
        /// </summary>
        Task<Product?> GetByIdAsync(Guid id);

        /// <summary>
        /// Создать новый продукт
        /// </summary>
        Task<Product> CreateAsync(Product product);

        /// <summary>
        /// Обновить продукт
        /// </summary>
        Task UpdateAsync(Product product);

        /// <summary>
        /// Удалить продукт
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Получить список категорий
        /// </summary>
        Task<List<Product>> GetCategoriesAsync();

        /// <summary>
        /// Импорт БЖУ базы из Calorizator.ru
        /// </summary>
        Task ImportProductsAsync();

        /// <summary>
        /// Полный импорт: БЖУ + Примерные цены (РЕКОМЕНДУЕТСЯ)
        /// </summary>
        Task ImportCombinedAsync();
    }
}