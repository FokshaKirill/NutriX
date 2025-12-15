using Infrastructure.Interfaces;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Services
{
    public class MealTypeService : IMealTypeService
    {
        private readonly IRepository<MealType> _mealTypes;

        public MealTypeService(IRepository<MealType> mealTypes)
        {
            _mealTypes = mealTypes;
        }

        public Task<List<MealType>> GetUserMealTypesAsync(int userId)
            => _mealTypes.Query()
                .Where(mt => mt.UserId == userId)
                .OrderBy(mt => mt.Order)
                .ToListAsync();
    }
}
