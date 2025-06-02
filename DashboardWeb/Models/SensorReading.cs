namespace DashboardWeb.Models
{
    public class SensorReading
    {
        public int Id { get; set; }
        public string WavyId { get; set; }
        public string Sensor { get; set; }
        public string Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
