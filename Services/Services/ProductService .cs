using Infrastructure.Interfaces;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Services
{
    public class ProductService : IProductService
    {
        private readonly IRepository<Product> _products;

        public ProductService(IRepository<Product> products)
        {
            _products = products;
        }

        public Task<Product?> GetByIdAsync(int id)
            => _products.Query()
                .Include(p => p.Children)
                .FirstOrDefaultAsync(p => p.Id == id);

        public Task<List<Product>> GetUserProductsAsync(int userId)
            => _products.Query()
                .Where(p => p.UserId == userId)
                .ToListAsync();

        public async Task<Product> CreateAsync(Product product)
        {
            await _products.AddAsync(product);
            await _products.SaveChangesAsync();
            return product;
        }

        public async Task UpdateAsync(Product product)
        {
            await _products.UpdateAsync(product);
            await _products.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var product = await _products.GetByIdAsync(id);
            if (product == null) return;

            await _products.DeleteAsync(product);
            await _products.SaveChangesAsync();
        }
    }
}
