using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IMealService
    {
        Task<List<PlannedMeal>> GetMealsByDateAsync(DateTime date, int userId);
        Task<int> GetTotalCaloriesAsync(DateTime date, int userId);
        Task<int> GetTotalProteinAsync(DateTime date, int userId);
        Task<int> GetTotalFatAsync(DateTime date, int userId);
        Task<int> GetTotalCarbsAsync(DateTime date, int userId);
    }
}
