namespace Presentation.Models
{
    public class MealViewModel
    {
        public string MealType { get; set; }
        public string RecipeName { get; set; }
        public int Servings { get; set; }

        public int Calories { get; set; }
        public int Protein { get; set; }
        public int Fat { get; set; }
        public int Carbs { get; set; }
    }

}
