// Program.cs - Aplicação Agregador com integração RabbitMQ, gRPC, validação e logs de erro

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using PreprocessingService; // Namespace gerado a partir do ficheiro .proto (gRPC)
using RabbitMQ.Client;      // Biblioteca RabbitMQ

class Program
{
    // Configuração da porta do servidor TCP
    static int porta = 5000;
    static TcpListener listener;

    // Dicionários para gerir concorrência e registos por sensor
    static readonly Dictionary<string, object> locksPorSensor = new();
    static readonly Dictionary<string, HashSet<string>> wavysPorSensor = new();

    // Lista de sensores válidos (hardcoded para validação)
    static readonly HashSet<string> sensoresValidos = new()
    {
        "Temperatura", "Salinidade", "Ondas", "Vento", "Turbidez", "PH", "Oxigenio", "Corrente",
        "Nitratos", "Clorofila", "RadiacaoSolar", "TransparenciaAgua", "CO2Dissolvido", "CondutividadeEletrica",
        "PressaoAtmosferica", "AlturaMare", "TemperaturaSuperficial", "NivelNitritos", "CargaOrganica",
        "PresencaMetaisPesados", "VelocidadeCorrenteza", "DensidadePlankton", "TemperaturaFundo",
        "NivelRuidoSubaquatico"
    };

    static void Main(string[] args)
    {
        // Inicia o servidor TCP
        listener = new TcpListener(IPAddress.Any, porta);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {porta}...");

        // Loop infinito para aceitar conexões de clientes WAVY
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            // Cria uma thread separada para cada cliente (atenção: em produção, usar ThreadPool ou async/await)
            Thread thread = new(() => TratarWavyAsync(client).Wait());
            thread.Start();
        }
    }

    // Método assíncrono para processar mensagens de um WAVY
    static async Task TratarWavyAsync(TcpClient client)
    {
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[256];
        string wavyId = "";

        try
        {
            while (true)
            {
                // Lê dados do cliente
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;

                string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine("[WAVY] " + msg);

                string[] partes = msg.Split(';');

                // Processa o tipo de mensagem
                switch (partes[0])
                {
                    case "HELLO":
                        wavyId = partes[1];
                        Send(stream, "HELLO_ACK"); // Confirmação de handshake
                        break;

                    case "REGISTER":
                        Send(stream, "REGISTER_ACK"); // Confirmação de registo
                        break;

                    case "DATA":
                        string sensor = partes[1];
                        string valor = partes[2];

                        // Validação do sensor
                        if (!sensoresValidos.Contains(sensor))
                        {
                            LogErro($"Sensor inválido: {sensor}");
                            Send(stream, "ERROR_SENSOR");
                            break;
                        }
                        // Validação do valor (conversão para float)
                        if (!float.TryParse(valor.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                        {
                            LogErro($"Valor inválido: {valor} para sensor {sensor}");
                            Send(stream, "ERROR_VALOR");
                            break;
                        }

                        // Processamento dos dados:
                        GravarEmCsv(wavyId, sensor, valor); // Armazena localmente
                        string jsonFormatado = await ChamarServicoGrpc(wavyId, sensor, valor, "http://localhost:5122"); // Formata via gRPC
                        PublicarParaRabbitMQ(jsonFormatado); // Envia para fila RabbitMQ
                        Send(stream, "RECEIVED"); // Confirma receção
                        break;

                    case "BYE":
                        Send(stream, "BYE_ACK"); // Confirmação de despedida
                        return;

                    default:
                        Send(stream, "ACK"); // Resposta genérica
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LogErro("Erro na WAVY: " + ex.Message); // Registo de erros
        }
    }

    // Guarda dados num ficheiro CSV organizado por sensor
    static void GravarEmCsv(string wavyId, string sensor, string valor)
    {
        string pasta = "sensores";
        string ficheiro = Path.Combine(pasta, $"{sensor}.csv");
        string linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{wavyId};{valor}";

        Directory.CreateDirectory(pasta); // Cria pasta se não existir

        // Bloqueio por sensor para evitar concorrência
        lock (locksPorSensor)
        {
            if (!locksPorSensor.ContainsKey(sensor))
                locksPorSensor[sensor] = new object();
        }

        lock (locksPorSensor[sensor])
        {
            // Regista WAVY se for a primeira vez
            if (!wavysPorSensor.ContainsKey(sensor))
                wavysPorSensor[sensor] = new HashSet<string>();

            bool primeiraVez = wavysPorSensor[sensor].Add(wavyId);

            // Adiciona cabeçalho se for novo WAVY
            if (primeiraVez)
            {
                File.AppendAllText(ficheiro, $"# --- {wavyId} ---{Environment.NewLine}");
            }

            File.AppendAllText(ficheiro, linha + Environment.NewLine); // Escreve linha
        }
    }

    // Envia resposta ao cliente WAVY
    static void Send(NetworkStream stream, string resposta)
    {
        byte[] respostaBytes = Encoding.UTF8.GetBytes(resposta);
        stream.Write(respostaBytes, 0, respostaBytes.Length);
    }

    // Chama o serviço gRPC para formatar os dados
    static async Task<string> ChamarServicoGrpc(string wavyId, string sensor, string valor, string grpcUrl)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(grpcUrl);
            var client = new Preprocessor.PreprocessorClient(channel);

            var request = new SensorData
            {
                Sensor = sensor,
                Value = valor,
                WavyId = wavyId
            };

            var reply = await client.FormatSensorDataAsync(request);
            Console.WriteLine($"[gRPC JSON] {reply.Formatted}");
            return reply.Formatted;
        }
        catch (Exception ex)
        {
            LogErro("Erro ao chamar gRPC: " + ex.Message);
            return $"AGG_DATA;{wavyId};{sensor};{valor}"; // Formato de fallback
        }
    }

    // Publica mensagem na fila RabbitMQ
    static void PublicarParaRabbitMQ(string json)
    {
        try
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest", // Credenciais padrão (alterar em produção!)
                Password = "guest"
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Declara a fila (cria se não existir)
            channel.QueueDeclare(queue: "sensor_data",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(exchange: "",
                                 routingKey: "sensor_data",
                                 basicProperties: null,
                                 body: body);

            Console.WriteLine("[RabbitMQ] Publicado: " + json);
        }
        catch (Exception ex)
        {
            LogErro("Erro ao publicar no RabbitMQ: " + ex.Message);
        }
    }

    // Regista erros num ficheiro de log
    static void LogErro(string mensagem)
    {
        string path = "erros.log";
        string linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensagem}";
        File.AppendAllText(path, linha + Environment.NewLine);
    }
}