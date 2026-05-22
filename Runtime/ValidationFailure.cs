namespace DrakesItemShop.Runtime;

internal sealed class ValidationFailure
{
    public string FileName { get; set; } = "";
    public string Error { get; set; } = "";
    public string? Field { get; set; }
    public string? Suggestion { get; set; }
}
