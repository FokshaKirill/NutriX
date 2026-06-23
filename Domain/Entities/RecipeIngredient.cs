using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Unit { get; set; } = null!;
    public string? Comment { get; set; }

    // ── Конвертация в граммы ────────────────────────────────────
    // Стандартные кухонные меры. Для "шт" используем Product.WeightGrams,
    // если он указан — иначе считаем как 100г по умолчанию (грубое допущение).
    private static readonly Dictionary<string, decimal> UnitToGrams = new()
    {
        ["г"]     = 1,
        ["g"]     = 1,
        ["кг"]    = 1000,
        ["мл"]    = 1,      // приближение: 1 мл ≈ 1 г для большинства жидкостей
        ["л"]     = 1000,
        ["ст.л."] = 15,     // столовая ложка
        ["ст.ложка"] = 15,
        ["ч.л."]  = 5,      // чайная ложка
        ["ч.ложка"] = 5,
        ["стакан"] = 200,
    };

    public decimal GetGrams()
    {
        var unitKey = Unit?.Trim().ToLower() ?? "";

        if (UnitToGrams.TryGetValue(unitKey, out var factor))
            return Amount * factor;

        // "шт" / "штука" — берём вес единицы из продукта
        if (unitKey is "шт" or "штука" or "pcs")
            return Amount * (Product?.WeightGrams ?? 100);

        // Неизвестная единица — считаем как граммы (без искажения хотя бы не умножаем на 100 ошибочно)
        return Amount;
    }

    public decimal? Calories => Product?.CaloriesPer100 * (GetGrams() / 100);
    public decimal? Protein  => Product?.ProteinPer100  * (GetGrams() / 100);
    public decimal? Fat      => Product?.FatPer100      * (GetGrams() / 100);
    public decimal? Carbs    => Product?.CarbsPer100     * (GetGrams() / 100);
}