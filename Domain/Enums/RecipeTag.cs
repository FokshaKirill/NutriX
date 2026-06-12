namespace Domain.Enums
{
    /// <summary>
    /// Теги рецепта — битовые флаги, можно комбинировать через |.
    /// Пример: Tags = RecipeTag.Lunch | RecipeTag.Soup | RecipeTag.Quick
    /// </summary>
    [Flags]
    public enum RecipeTag
    {
        None       = 0,
        Breakfast  = 1 << 0,   // подходит на завтрак
        Lunch      = 1 << 1,   // подходит на обед
        Dinner     = 1 << 2,   // подходит на ужин
        Snack      = 1 << 3,   // перекус
        Soup       = 1 << 4,   // первое блюдо / суп
        Salad      = 1 << 5,   // салат
        MainCourse = 1 << 6,   // основное / второе блюдо
        Garnish    = 1 << 7,   // гарнир
        Dessert    = 1 << 8,   // десерт
        Drink      = 1 << 9,   // напиток
        Quick      = 1 << 10,  // быстрое блюдо (≤ 15 минут)
        Festive    = 1 << 11   // праздничное
    }
}