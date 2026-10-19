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
        // Physical limits: beyond these a value is a typing or instrument error, not a water quality alert.
        public static string? ChlorinePhysicalError(double value) => value < 0 || value > 20 ? "Cloro residual deve estar entre 0 e 20 mg/L." : null;
        public static string? PhPhysicalError(double value) => value < 0 || value > 14 ? "pH deve estar entre 0 e 14." : null;
        public static string? TurbidityPhysicalError(double value) => value < 0 || value > 1000 ? "Turbidez deve estar entre 0 e 1000 NTU." : null;
        public static string? IronPhysicalError(double value) => value < 0 || value > 100 ? "Ferro deve estar entre 0 e 100 mg/L." : null;

        /// <summary>Returns an error for values that cannot be real measurements, or null when plausible.</summary>
        public static string? GetPhysicalLimitError(double residualChlorine, double ph, double turbidity, double iron) =>
            PhPhysicalError(ph) ?? ChlorinePhysicalError(residualChlorine) ?? TurbidityPhysicalError(turbidity) ?? IronPhysicalError(iron);

        public static string ChlorineStandardText => $"{Format(WaterAnalysis.ChlorineMin)} a {Format(WaterAnalysis.ChlorineMax)} mg/L";
        public static string PhStandardText => $"{Format(WaterAnalysis.PhMin)} a {Format(WaterAnalysis.PhMax)}";
        public static string TurbidityStandardText => $"até {Format(WaterAnalysis.TurbidityMax)} NTU";
        public static string IronStandardText => $"até {Format(WaterAnalysis.IronMax)} mg/L";

        // Live hints while typing: physical error, out-of-standard notice, or null when within the standard.
        public static string? DescribeChlorine(double value) => ChlorinePhysicalError(value) ?? (WaterAnalysis.IsChlorineWithinStandard(value) ? null : OutOfStandardHint(ChlorineStandardText));
        public static string? DescribePh(double value) => PhPhysicalError(value) ?? (WaterAnalysis.IsPhWithinStandard(value) ? null : OutOfStandardHint(PhStandardText));
        public static string? DescribeTurbidity(double value) => TurbidityPhysicalError(value) ?? (WaterAnalysis.IsTurbidityWithinStandard(value) ? null : OutOfStandardHint(TurbidityStandardText));
        public static string? DescribeIron(double value) => IronPhysicalError(value) ?? (WaterAnalysis.IsIronWithinStandard(value) ? null : OutOfStandardHint(IronStandardText));

        /// <summary>Parses a reading typed with either ',' or '.' as decimal separator.</summary>
        public static bool TryParseReading(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            // No exponent or thousands separator: "1e3" or "1 000" are typos, not readings.
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
            if (!analysis.IsChlorineValid) outOfStandard.Add($"Cloro residual {Format(chlorineValue)} mg/L (padrão: {ChlorineStandardText})");
            if (!analysis.IsPhValid) outOfStandard.Add($"pH {Format(phValue)} (padrão: {PhStandardText})");
            if (!analysis.IsTurbidityValid) outOfStandard.Add($"Turbidez {Format(turbidityValue)} NTU (padrão: {TurbidityStandardText})");
            if (!analysis.IsIronValid) outOfStandard.Add($"Ferro {Format(ironValue)} mg/L (padrão: {IronStandardText})");
            if (!analysis.EColiAbsent) outOfStandard.Add("E. coli presente (padrão: ausente)");

            return new WaterAnalysisInputResult(analysis, null, outOfStandard);
        }

        private static string OutOfStandardHint(string standard) => $"Fora do padrão da Portaria 888 ({standard}). Será registrado como alerta.";

        private static string Format(double value) => value.ToString("0.0##", CultureInfo.GetCultureInfo("pt-BR"));
    }

    public sealed record WaterAnalysisInputResult(WaterAnalysis? Analysis, string? Error, IReadOnlyList<string> OutOfStandard)
    {
        public bool IsValid => Analysis != null;

        public static WaterAnalysisInputResult Fail(string error) => new(null, error, []);
    }
}
