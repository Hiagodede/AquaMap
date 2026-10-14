using System;

namespace AquaMap.Domain.Entities
{
    public class WaterAnalysis
    {
        public int Id { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public bool IsPendingSync { get; set; } = false;

        // Parâmetros avaliados (Portaria 888/2021)
        public double ResidualChlorine { get; set; } // 0.2 a 5.0 mg/L
        public double Ph { get; set; }               // 6.0 a 9.5
        public double Turbidity { get; set; }        // Máx 5.0 NTU
        public bool EColiAbsent { get; set; }        // Deve ser true para ausência
        public double Iron { get; set; }             // Máx 0.3 mg/L (Portaria 888)

        // Georreferenciamento do ponto de coleta (GAP 1)
        public double? CollectionLatitude { get; set; }
        public double? CollectionLongitude { get; set; }
        public bool HasLocation => CollectionLatitude.HasValue && CollectionLongitude.HasValue;

        // Foreign Key
        public int ReservoirId { get; set; }
        public Reservoir Reservoir { get; set; } = null!;

        // Potability standard (Portaria GM/MS 888/2021). Single source for apps, API and PDF.
        public const double ChlorineMin = 0.2;   // mg/L
        public const double ChlorineMax = 5.0;   // mg/L
        public const double PhMin = 6.0;
        public const double PhMax = 9.5;
        public const double TurbidityMax = 5.0;  // NTU
        public const double IronMax = 0.3;       // mg/L

        public static bool IsChlorineWithinStandard(double value) => value >= ChlorineMin && value <= ChlorineMax;
        public static bool IsPhWithinStandard(double value) => value >= PhMin && value <= PhMax;
        public static bool IsTurbidityWithinStandard(double value) => value <= TurbidityMax;
        public static bool IsIronWithinStandard(double value) => value <= IronMax;

        // Validações individuais para destaque na UI
        public bool IsChlorineValid => IsChlorineWithinStandard(ResidualChlorine);
        public bool IsPhValid => IsPhWithinStandard(Ph);
        public bool IsTurbidityValid => IsTurbidityWithinStandard(Turbidity);
        public bool IsIronValid => IsIronWithinStandard(Iron);

        // Cálculo de Potabilidade encapsulado
        public bool IsPotable =>
            IsChlorineValid &&
            IsPhValid &&
            IsTurbidityValid &&
            IsIronValid &&
            EColiAbsent;
    }
}
