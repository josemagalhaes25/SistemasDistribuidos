using System.Data.SqlClient;
using DashboardWeb.Models;

namespace DashboardWeb.Services
{
    public class SensorService
    {
        private readonly string _connectionString;

        public SensorService(IConfiguration configuration)
        {
            // Lê a string de ligação do ficheiro appsettings.json
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public List<SensorReading> GetAllReadings()
        {
            List<SensorReading> readings = new();

            using SqlConnection conn = new(_connectionString);
            conn.Open();

            string query = "SELECT Id, WavyId, Sensor, Value, Timestamp FROM SensorReadings ORDER BY Timestamp DESC";

            using SqlCommand cmd = new(query, conn);
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                readings.Add(new SensorReading
                {
                    Id = reader.GetInt32(0),
                    WavyId = reader.GetString(1),
                    Sensor = reader.GetString(2),
                    Value = reader.GetString(3),
                    Timestamp = reader.GetDateTime(4)
                });
            }

            return readings;
        }
    }
}