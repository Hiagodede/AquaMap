namespace AquaMap.Domain.Entities
{
    public class Neighborhood
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int ReservoirId { get; set; }
        public Reservoir Reservoir { get; set; } = null!;
    }
}
