using Services.Services;

namespace Services.Interfaces;

public interface INutritionApiService
{
    Task<ProductNutrition?> GetNutritionInfoAsync(string productName);
    Task<Product> EnrichProductWithNutritionAsync(Product product);
}