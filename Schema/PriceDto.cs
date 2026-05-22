using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DrakesItemShop.Schema;

/// <summary>Price as integer coins, or { "coins": N } / { "item": "X", "amount": N }.</summary>
internal sealed class PriceDto
{
    [JsonProperty("coins")]
    public int? Coins { get; set; }

    [JsonProperty("item")]
    public string? Item { get; set; }

    [JsonProperty("amount")]
    public int? Amount { get; set; }

    public static PriceDto? FromToken(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        if (token.Type == JTokenType.Integer)
            return new PriceDto { Coins = token.Value<int>() };

        if (token.Type == JTokenType.Float)
            return new PriceDto { Coins = (int)token.Value<double>() };

        return token.ToObject<PriceDto>();
    }

    public int ResolveCoinPrice()
    {
        if (Coins.HasValue)
            return Math.Max(0, Coins.Value);
        return 0;
    }

    public bool IsItemCurrency =>
        !string.IsNullOrWhiteSpace(Item) && Amount.HasValue && Amount.Value > 0;
}
