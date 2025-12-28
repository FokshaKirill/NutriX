namespace Presentation.Models;

public class ShoppingListViewModel
{
    public Guid MealPlanId { get; set; }
    public List<ShoppingIngredientViewModel> Ingredients { get; set; } = [];
}