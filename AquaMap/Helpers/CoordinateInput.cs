using System.Globalization;
using System.Text;

namespace AquaMap.Helpers
{
    public enum CoordinateInputStatus { Empty, Invalid, Valid }

    public readonly record struct CoordinateInputResult(CoordinateInputStatus Status, double Latitude, double Longitude);

    /// <summary>
    /// Coordinates typed or filled in the latitude/longitude fields. The fields are the only source of truth:
    /// GPS and the map picker write into them, so whatever the technician types afterwards is what gets saved
    /// (QA EST-12: a GPS fix used to override the typed values silently).
    /// </summary>
    public static class CoordinateInput
    {
        /// <summary>Keeps an optional leading "-", digits and one decimal separator (stored as "."), up to 6 decimals.</summary>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            bool hasNegative = false, hasDecimal = false;
            int decimalDigits = 0;
            var result = new StringBuilder();
            foreach (char c in input)
            {
                if (c == '-' && !hasNegative && result.Length == 0) { hasNegative = true; result.Append(c); }
                else if ((c == '.' || c == ',') && !hasDecimal) { hasDecimal = true; result.Append('.'); }
                else if (char.IsDigit(c)) { if (hasDecimal) { if (decimalDigits < 6) { result.Append(c); decimalDigits++; } } else result.Append(c); }
            }
            return result.ToString();
        }

        /// <summary>
        /// Text that GPS, the map picker and an edit write into the fields: at most 6 decimals (~10 cm), so it fits
        /// the field and what the technician sees is exactly what is saved.
        /// </summary>
        public static string Format(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>
        /// Empty when both fields are blank; Invalid when only one is filled, a value does not parse, or it is
        /// outside the ranges the API enforces (latitude -90..90, longitude -180..180).
        /// </summary>
        public static CoordinateInputResult Parse(string? latitude, string? longitude)
        {
            bool noLat = string.IsNullOrWhiteSpace(latitude), noLng = string.IsNullOrWhiteSpace(longitude);
            if (noLat && noLng) return new(CoordinateInputStatus.Empty, 0, 0);
            if (noLat || noLng) return new(CoordinateInputStatus.Invalid, 0, 0);

            if (!TryParseNumber(latitude!, out double lat) || !TryParseNumber(longitude!, out double lng)
                || lat is < -90 or > 90 || lng is < -180 or > 180)
                return new(CoordinateInputStatus.Invalid, 0, 0);

            return new(CoordinateInputStatus.Valid, lat, lng);
        }

        private static bool TryParseNumber(string text, out double value) =>
            double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }
}
