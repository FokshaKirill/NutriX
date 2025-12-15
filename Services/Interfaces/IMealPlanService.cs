using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IMealPlanService
    {
        Task<MealPlan?> GetByIdAsync(int id);
        Task<List<MealPlan>> GetUserPlansAsync(int userId);
        Task<MealPlan> CreateAsync(MealPlan plan);
        Task UpdateAsync(MealPlan plan);
        Task DeleteAsync(int id);

        Task<List<Product>> GenerateShoppingListAsync(int mealPlanId);
    }
}
