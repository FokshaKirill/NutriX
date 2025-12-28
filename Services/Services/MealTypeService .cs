using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class MealTypeService : IMealTypeService
    {
        private readonly IRepository<MealType> _mealTypes;

        public MealTypeService(IRepository<MealType> mealTypes)
        {
            _mealTypes = mealTypes;
        }

        public async Task<List<MealType>> GetAllMealTypesAsync()
        {
            return await _mealTypes.Query()
                .OrderBy(mt => mt.Order)
                .ToListAsync();
        }

        public async Task<MealType?> GetByIdAsync(Guid id)
        {
            return await _mealTypes.Query().FirstOrDefaultAsync(mt => mt.Id == id);
        }
    }
}