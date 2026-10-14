using System.Globalization;

namespace AquaMap.Domain.Entities
{
    /// <summary>
    /// Readings as typed by the technician, before they become a <see cref="WaterAnalysis"/>.
    /// Only physically impossible values are rejected. A reading outside the Portaria 888 standard
    /// is valid data: it must be recorded and flagged, never blocked.
    /// </summary>
    public static class WaterAnalysisInput
    {
        /// <summary>Returns an error for values that cannot be real measurements, or null when plausible.</summary>
        public static string? GetPhysicalLimitError(double residualChlorine, double ph, double turbidity, double iron)
        {
            if (ph < 0 || ph > 14) return "pH deve estar entre 0 e 14.";
            if (residualChlorine < 0 || residualChlorine > 20) return "Cloro residual deve estar entre 0 e 20 mg/L.";
            if (turbidity < 0 || turbidity > 1000) return "Turbidez deve estar entre 0 e 1000 NTU.";
            if (iron < 0 || iron > 100) return "Ferro deve estar entre 0 e 100 mg/L.";
            return null;
        }

        /// <summary>Parses a reading typed with either ',' or '.' as decimal separator.</summary>
        public static bool TryParseReading(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            // No exponent or thousands separator: "1e3" or "1.000,5" are typos, not readings.
            const NumberStyles readingStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            return double.TryParse(text.Trim().Replace(',', '.'), readingStyle, CultureInfo.InvariantCulture, out value)
                && double.IsFinite(value);
        }

        /// <summary>
        /// Validates the form fields. Every parameter is required, including iron and the E. coli result
        /// (no default: a forgotten field must not count as "within the standard").
        /// </summary>
        public static WaterAnalysisInputResult Validate(string? residualChlorine, string? ph, string? turbidity, string? iron, bool? eColiAbsent)
        {
            if (!TryParseReading(residualChlorine, out var chlorineValue)) return WaterAnalysisInputResult.Fail("Informe o cloro residual com um número válido.");
            if (!TryParseReading(ph, out var phValue)) return WaterAnalysisInputResult.Fail("Informe o pH com um número válido.");
            if (!TryParseReading(turbidity, out var turbidityValue)) return WaterAnalysisInputResult.Fail("Informe a turbidez com um número válido.");
            if (!TryParseReading(iron, out var ironValue)) return WaterAnalysisInputResult.Fail("Informe o ferro com um número válido.");
            if (eColiAbsent is null) return WaterAnalysisInputResult.Fail("Informe o resultado do teste de E. coli (ausente ou presente).");

            var physicalError = GetPhysicalLimitError(chlorineValue, phValue, turbidityValue, ironValue);
            if (physicalError != null) return WaterAnalysisInputResult.Fail(physicalError);

            var analysis = new WaterAnalysis
            {
                ResidualChlorine = chlorineValue,
                Ph = phValue,
                Turbidity = turbidityValue,
                Iron = ironValue,
                EColiAbsent = eColiAbsent.Value
            };

            var outOfStandard = new List<string>();
            if (!analysis.IsChlorineValid) outOfStandard.Add($"Cloro residual {Format(chlorineValue)} mg/L (padrão: {Format(WaterAnalysis.ChlorineMin)} a {Format(WaterAnalysis.ChlorineMax)} mg/L)");
            if (!analysis.IsPhValid) outOfStandard.Add($"pH {Format(phValue)} (padrão: {Format(WaterAnalysis.PhMin)} a {Format(WaterAnalysis.PhMax)})");
            if (!analysis.IsTurbidityValid) outOfStandard.Add($"Turbidez {Format(turbidityValue)} NTU (padrão: até {Format(WaterAnalysis.TurbidityMax)} NTU)");
            if (!analysis.IsIronValid) outOfStandard.Add($"Ferro {Format(ironValue)} mg/L (padrão: até {Format(WaterAnalysis.IronMax)} mg/L)");
            if (!analysis.EColiAbsent) outOfStandard.Add("E. coli presente (padrão: ausente)");

            return new WaterAnalysisInputResult(analysis, null, outOfStandard);
        }

        private static string Format(double value) => value.ToString("0.0##", CultureInfo.GetCultureInfo("pt-BR"));
    }

    public sealed record WaterAnalysisInputResult(WaterAnalysis? Analysis, string? Error, IReadOnlyList<string> OutOfStandard)
    {
        public bool IsValid => Analysis != null;

        public static WaterAnalysisInputResult Fail(string error) => new(null, error, []);
    }
}
