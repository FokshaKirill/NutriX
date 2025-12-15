using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IMealTypeService
    {
        Task<List<MealType>> GetUserMealTypesAsync(int userId);
    }
}
