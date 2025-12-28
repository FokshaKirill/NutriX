using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        
        Task<(List<Product> Products, int TotalCount)> GetPagedProductsAsync(
            int page, 
            int pageSize, 
            string? searchTerm = null, 
            Guid? categoryId = null,
            decimal? minCalories = null,
            decimal? maxCalories = null,
            decimal? minPrice = null,
            decimal? maxPrice = null);
        
        Task<Product?> GetByIdAsync(Guid id);
        
        Task<Product> CreateAsync(Product product);
        
        Task UpdateAsync(Product product);
        
        Task DeleteAsync(Guid id);
        
        Task<List<Product>> GetCategoriesAsync();
        
        Task ImportProductsAsync();
    }
}
