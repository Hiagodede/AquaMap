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

        // Validações individuais para destaque na UI
        public bool IsChlorineValid => ResidualChlorine >= 0.2 && ResidualChlorine <= 2.0;
        public bool IsPhValid => Ph >= 6.0 && Ph <= 9.5;
        public bool IsTurbidityValid => Turbidity <= 5.0;
        public bool IsIronValid => Iron <= 0.3;

        // Cálculo de Potabilidade encapsulado
        public bool IsPotable => 
            ResidualChlorine >= 0.2 && ResidualChlorine <= 5.0 &&
            Ph >= 6.0 && Ph <= 9.5 &&
            Turbidity <= 5.0 &&
            Iron <= 0.3 &&
            EColiAbsent;
    }
}
