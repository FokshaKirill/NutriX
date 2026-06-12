namespace Services.Helpers;

public static class CalorieCalculator
{
    public static int CalcBmr(decimal weight, decimal height, int age, string gender)
    {
        // Миффлин-Сан Жеор
        double bmr = 10 * (double)weight
                     + 6.25 * (double)height
                     - 5 * age
                     + (gender == "male" ? 5 : -161);
        return (int)Math.Round(bmr);
    }

    public static int CalcTdee(int bmr, string activity) => activity switch
    {
        "sedentary"  => (int)(bmr * 1.2),
        "light"      => (int)(bmr * 1.375),
        "moderate"   => (int)(bmr * 1.55),
        "active"     => (int)(bmr * 1.725),
        "veryactive" => (int)(bmr * 1.9),
        _            => bmr
    };

    // Макросы по цели
    public static (int protein, int fat, int carbs) CalcMacros(int kcal, string goal) => goal switch
    {
        "lose"     => (protein: (int)(kcal * 0.35 / 4),
            fat:     (int)(kcal * 0.30 / 9),
            carbs:   (int)(kcal * 0.35 / 4)),
        "gain"     => (protein: (int)(kcal * 0.25 / 4),
            fat:     (int)(kcal * 0.25 / 9),
            carbs:   (int)(kcal * 0.50 / 4)),
        _          => (protein: (int)(kcal * 0.30 / 4),
            fat:     (int)(kcal * 0.30 / 9),
            carbs:   (int)(kcal * 0.40 / 4)),
    };
}