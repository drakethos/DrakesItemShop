using Newtonsoft.Json;

namespace DrakesItemShop.Schema;

internal sealed class NpcDefinitionDto
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("clonePrefab")]
    public string? ClonePrefab { get; set; }

    [JsonProperty("catalogId")]
    public string? CatalogId { get; set; }

    [JsonProperty("shopId")]
    public string? ShopId { get; set; }

    [JsonProperty("currency")]
    public CurrencyDto? Currency { get; set; }

    [JsonProperty("disableDialogue")]
    public bool DisableDialogue { get; set; } = true;

    [JsonProperty("spawn")]
    public NpcSpawnDto? Spawn { get; set; }

    [JsonIgnore]
    public string SourceFile { get; set; } = "";
}

internal sealed class NpcSpawnDto
{
    [JsonProperty("piece")]
    public string? Piece { get; set; }
}
