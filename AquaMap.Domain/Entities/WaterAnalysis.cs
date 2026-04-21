using System;

namespace AquaMap.Domain.Entities
{
    public class WaterAnalysis
    {
        public int Id { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

        // Parâmetros avaliados
        public double ResidualChlorine { get; set; } // 0.2 a 5.0
        public double Ph { get; set; } // 6.0 a 9.5
        public double Turbidity { get; set; } // Máx 5.0
        public bool EColiAbsent { get; set; } // Deve ser true para ausência

        // Foreign Key
        public int ReservoirId { get; set; }
        public Reservoir Reservoir { get; set; } = null!;

        // Cálculo de Potabilidade encapsulado
        public bool IsPotable => 
            ResidualChlorine >= 0.2 && ResidualChlorine <= 5.0 &&
            Ph >= 6.0 && Ph <= 9.5 &&
            Turbidity <= 5.0 &&
            EColiAbsent;
    }
}
