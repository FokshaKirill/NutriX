using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IProductService
    {
        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetUserProductsAsync(int userId);
        Task<Product> CreateAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(int id);
    }
}
