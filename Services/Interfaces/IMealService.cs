using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IMealService
    {
        Task<List<PlannedMeal>> GetMealsByDateAsync(DateTime date);
        Task<int> GetTotalCaloriesAsync(DateTime date);
        Task<int> GetTotalProteinAsync(DateTime date);
        Task<int> GetTotalFatAsync(DateTime date);
        Task<int> GetTotalCarbsAsync(DateTime date);
    }
}
