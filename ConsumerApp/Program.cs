// ConsumerApp - Lê mensagens da fila RabbitMQ e insere na base de dados SQL Server

using System;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Consumidor à escuta da fila 'sensor_data'...");

        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "sensor_data",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var mensagem = Encoding.UTF8.GetString(body);
            Console.WriteLine($"[Recebido] {mensagem}");

            // Parse da mensagem JSON
            try
            {
                var dado = JsonSerializer.Deserialize<SensorData>(mensagem);
                InserirNaBaseDeDados(dado);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao processar JSON: " + ex.Message);
            }
        };

        channel.BasicConsume(queue: "sensor_data",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine("A ouvir... Pressiona [Enter] para sair.");
        Console.ReadLine();
    }

    // Modelo para o JSON
    public class SensorData
    {
        public string wavy_id { get; set; }
        public string sensor { get; set; }
        public string value { get; set; }
        public DateTime timestamp { get; set; }
    }

    // Função para inserir na base de dados
    static void InserirNaBaseDeDados(SensorData data)
    {
        string connectionString = "Server=localhost\\SQLEXPRESS;Database=SensorDB;Trusted_Connection=True;";

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        string sql = "INSERT INTO SensorReadings (WavyId, Sensor, Value, Timestamp) VALUES (@WavyId, @Sensor, @Value, @Timestamp)";
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@WavyId", data.wavy_id);
        command.Parameters.AddWithValue("@Sensor", data.sensor);
        command.Parameters.AddWithValue("@Value", data.value);
        command.Parameters.AddWithValue("@Timestamp", data.timestamp);

        command.ExecuteNonQuery();

        Console.WriteLine("[BD] Inserido com sucesso.");
    }
}