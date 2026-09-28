using Godot;

namespace Battlegrounds.Game;

internal static class PreparationDragPayload
{
    private const string OfferPrefix = "offer:";
    private const string FieldPrefix = "field:";
    private const string ReserveUnitPrefix = "reserve-unit:";

    internal static string Offer(int slot) => $"{OfferPrefix}{slot}";

    internal static string Field(int index) => $"{FieldPrefix}{index}";

    internal static string ReserveUnit(int slot) => $"{ReserveUnitPrefix}{slot}";

    internal static bool TryReadOffer(Variant data, out int slot) =>
        TryReadIndex(data, OfferPrefix, out slot);

    internal static bool TryReadField(Variant data, out int index) =>
        TryReadIndex(data, FieldPrefix, out index);

    internal static bool TryReadReserveUnit(Variant data, out int slot) =>
        TryReadIndex(data, ReserveUnitPrefix, out slot);

    private static bool TryReadIndex(Variant data, string prefix, out int value)
    {
        value = -1;
        if (data.VariantType != Variant.Type.String)
            return false;

        var text = data.AsString();
        return text.StartsWith(prefix, StringComparison.Ordinal) &&
               int.TryParse(text[prefix.Length..], out value) &&
               value >= 0;
    }
}
