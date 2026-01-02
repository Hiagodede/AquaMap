using System;
using System.Collections.Generic;
using System.Text;

namespace AquaMap.Domain.Entities
{
    public class LabAnalysis
    {
        public Guid Id { get; private set; }
        public Guid CollectionPointId { get; private set; }
        public DateTime CollectionDate { get; private set; }


        public double PH { get; private set; }
        public double Turbidity { get; private set; }
        public double TotalColiforms { get; private set; }
        public bool HasMetals { get; private set; }

        public PotabilityStatus Status { get; private set; }

        public LabAnalysis(Guid collectionPointId, double ph, double turbidity, double totalColiforms, bool hasMetals)
        {
            Id = Guid.NewGuid();
            CollectionPointId = collectionPointId;
            CollectionDate = DateTime.Now;
            PH = ph;
            Turbidity = turbidity;
            TotalColiforms = totalColiforms;
            HasMetals = hasMetals;

            ValidatePotability();
        }

        private void ValidatePotability() 
        {
            bool isPhOk = PH >= 6.0 && PH <= 9.5;
            bool isTurbidityOk = Turbidity <= 5.0;

            if (isPhOk && isTurbidityOk && !HasMetals && TotalColiforms == 0)
            {
                Status = PotabilityStatus.Potable;
            }
            else
            {
                Status = PotabilityStatus.Unfit;
            }
        }
    }
}
