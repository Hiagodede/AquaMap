using System.Collections.Generic;

namespace AquaMap.Domain.Entities
{
    public class Reservoir
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        // Coordenadas Geográficas
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        // Relacionamento 1:N com Bairros
        public ICollection<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();
        
        // Relacionamento 1:N com Análises de Água
        public ICollection<WaterAnalysis> WaterAnalyses { get; set; } = new List<WaterAnalysis>();
    }
}
