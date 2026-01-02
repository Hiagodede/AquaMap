using System;
using System.Collections.Generic;
using System.Text;

namespace AquaMap.Domain.Entities
{
    public class CollectionPoint
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public string Description { get; private set; }

        // Relationships: One point has many analyses
        public ICollection<LabAnalysis> AnalysisHistory { get; private set; } 

        public CollectionPoint(string name , double latitude, double longitude, string description)
        {
            Id = Guid.NewGuid();
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
            Description = description;
            AnalysisHistory = new List<LabAnalysis>();
        }
    }
}
