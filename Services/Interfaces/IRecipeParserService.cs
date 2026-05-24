using Services.DTO;
using Services.Services;

namespace Services.Interfaces;

public interface IRecipeParserService
{
    Task<ParsedRecipeDto> ParseRecipeFromUrlAsync(string url);
    Task<Recipe> ConvertToRecipeAsync(ParsedRecipeDto parsedRecipe);  
}