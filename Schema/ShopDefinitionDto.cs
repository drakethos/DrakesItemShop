using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrakesItemShop.Schema;

internal sealed class ShopDefinitionDto
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("catalogId")]
    public string? CatalogId { get; set; }

    [JsonProperty("currency")]
    public CurrencyDto? Currency { get; set; }

    [JsonProperty("altCurrencies")]
    public List<AltCurrencyDto>? AltCurrencies { get; set; }

    [JsonProperty("ward")]
    public WardDto? Ward { get; set; }

    [JsonProperty("sign")]
    public SignDto? Sign { get; set; }

    [JsonProperty("permissions")]
    public PermissionsDto? Permissions { get; set; }

    [JsonProperty("stockSource")]
    public string? StockSource { get; set; }

    [JsonIgnore]
    public string SourceFile { get; set; } = "";
}

internal sealed class CurrencyDto
{
    [JsonProperty("type")]
    public string? Type { get; set; }
}

internal sealed class AltCurrencyDto
{
    [JsonProperty("item")]
    public string? Item { get; set; }

    [JsonProperty("rate")]
    public int Rate { get; set; } = 1;
}

internal sealed class WardDto
{
    [JsonProperty("mode")]
    public string? Mode { get; set; }

    [JsonProperty("requirePermitted")]
    public bool RequirePermitted { get; set; } = true;
}

internal sealed class SignDto
{
    [JsonProperty("maxLines")]
    public int MaxLines { get; set; } = 8;

    [JsonProperty("template")]
    public string? Template { get; set; }
}

internal sealed class PermissionsDto
{
    [JsonProperty("open")]
    public string? Open { get; set; }

    [JsonProperty("manage")]
    public string? Manage { get; set; }
}
