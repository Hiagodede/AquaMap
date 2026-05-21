using System;
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

        [Indexed]
        public int ReservoirId { get; set; }

        [Indexed]
        public bool IsPendingSync { get; set; }

        [Ignore]
        public bool IsChlorineValid => ResidualChlorine >= 0.2 && ResidualChlorine <= 2.0;

        [Ignore]
        public bool IsPhValid => Ph >= 6.0 && Ph <= 9.5;

        [Ignore]
        public bool IsTurbidityValid => Turbidity <= 5.0;

        [Ignore]
        public bool IsPotable => 
            ResidualChlorine >= 0.2 && ResidualChlorine <= 5.0 &&
            Ph >= 6.0 && Ph <= 9.5 &&
            Turbidity <= 5.0 &&
            EColiAbsent;
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
