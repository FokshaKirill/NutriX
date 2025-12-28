using Microsoft.AspNetCore.Mvc.Rendering;

namespace Presentation.Models;

public class ProductIndexViewModel
{
    public List<ProductViewModel> Products { get; set; } = [];
    public string? SearchTerm { get; set; }
    public Guid? CategoryId { get; set; }
    public List<SelectListItem> Categories { get; set; } = [];
}