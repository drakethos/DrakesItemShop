using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DrakesItemShop.Schema;

internal sealed class CatalogDto
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("entries")]
    public List<CatalogEntryDto>? Entries { get; set; }

    [JsonIgnore]
    public string SourceFile { get; set; } = "";
}

internal sealed class CatalogEntryDto
{
    [JsonProperty("item")]
    public string? Item { get; set; }

    [JsonProperty("buyPrice")]
    public JToken? BuyPriceToken { get; set; }

    [JsonProperty("sellPrice")]
    public JToken? SellPriceToken { get; set; }

    /// <summary>-1 = infinite when using catalog-only stock.</summary>
    [JsonProperty("stock")]
    public int Stock { get; set; } = -1;

    [JsonIgnore]
    public PriceDto? BuyPrice => PriceDto.FromToken(BuyPriceToken);

    [JsonIgnore]
    public PriceDto? SellPrice => PriceDto.FromToken(SellPriceToken);
}
