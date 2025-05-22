using Grpc.Core;                  // Biblioteca para gRPC (comunicação remota)
using PreprocessingService;        // Namespace gerado pelo protobuf
using System.Text.Json;            // Para trabalhar com JSON

// Classe que implementa o serviço gRPC definido no arquivo .proto
public class PreprocessorService : Preprocessor.PreprocessorBase
{
    // Método que processa os dados do sensor (definido no contrato gRPC)
    public override Task<ProcessedData> FormatSensorData(SensorData request, ServerCallContext context)
    {
        // 1. Organiza os dados recebidos num formato estruturado:
        var formattedObject = new
        {
            wavy_id = request.WavyId,      // ID único da medição
            sensor = request.Sensor,       // Tipo do sensor (ex: "temperatura")
            value = request.Value,         // Valor lido (ex: "23.5")
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")  // Data/hora atual
        };

        // 2. Converte o objeto para formato JSON (string):
        string json = JsonSerializer.Serialize(formattedObject);

        // 3. Retorna a resposta ao cliente:
        return Task.FromResult(new ProcessedData { Formatted = json });
    }
}