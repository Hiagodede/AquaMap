using System;
using AquaMap.Domain.Entities;
using SQLite;

namespace AquaMap.Models
{
    public class LocalReservoir
    {
        [PrimaryKey]
        public int Id { get; set; } // Identificador remoto / local
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [Ignore]
        public string StatusColor { get; set; } = "Gray";
    }

    public class LocalWaterAnalysis
    {
        [PrimaryKey, AutoIncrement]
        public int LocalId { get; set; } // Chave primária local autoincrementada

        [Indexed]
        public int Id { get; set; } // Identificador da API remota (0 se gerada offline e pendente)

        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

        public double ResidualChlorine { get; set; }
        public double Ph { get; set; }
        public double Turbidity { get; set; }
        public bool EColiAbsent { get; set; }
        public double Iron { get; set; }              // GAP 7: Ferro (mg/L)
        public double? CollectionLatitude { get; set; }  // GAP 1: GPS da coleta
        public double? CollectionLongitude { get; set; } // GAP 1: GPS da coleta

        [Indexed]
        public int ReservoirId { get; set; }

        [Indexed]
        public bool IsPendingSync { get; set; }

        // Portaria 888 rules live only in the domain (CLAUDE.md, rule 3).
        [Ignore]
        public bool IsChlorineValid => WaterAnalysis.IsChlorineWithinStandard(ResidualChlorine);

        [Ignore]
        public bool IsPhValid => WaterAnalysis.IsPhWithinStandard(Ph);

        [Ignore]
        public bool IsTurbidityValid => WaterAnalysis.IsTurbidityWithinStandard(Turbidity);

        [Ignore]
        public bool IsIronValid => WaterAnalysis.IsIronWithinStandard(Iron);

        [Ignore]
        public bool IsPotable => IsChlorineValid && IsPhValid && IsTurbidityValid && IsIronValid && EColiAbsent;
    }

    public class LocalUser
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        
        [Indexed]
        public string TaxId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int Role { get; set; }

        [Ignore]
        public string RoleLabel => Role == 1 ? "Administrador" : "Técnico";
    }
}
