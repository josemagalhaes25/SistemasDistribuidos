using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Grpc.Net.Client;
using System.Threading.Tasks;
using PreprocessingService; // Namespace gerado a partir do ficheiro .proto (gRPC)

class Program
{
    // Porta onde o agregador escuta conexões de dispositivos WAVY
    static int porta = 5000;
    // Listener TCP para aceitar conexões
    static TcpListener listener;

    // Dicionários para gerir concorrência e registar dispositivos por sensor
    static readonly Dictionary<string, object> locksPorSensor = new();
    static readonly Dictionary<string, HashSet<string>> wavysPorSensor = new();

    static void Main(string[] args)
    {
        // Inicia o listener na porta definida
        listener = new TcpListener(IPAddress.Any, porta);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {porta}...");

        // Loop infinito para aceitar clientes (WAVYs)
        while (true)
        {
            // Aceita uma nova conexão
            TcpClient client = listener.AcceptTcpClient();
            // Cria uma thread para tratar do dispositivo WAVY
            Thread thread = new(() => TratarWavyAsync(client).Wait());
            thread.Start();
        }
    }

    // Método assíncrono para lidar com um dispositivo WAVY
    static async Task TratarWavyAsync(TcpClient client)
    {
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[256];
        string wavyId = ""; // Identificador do dispositivo WAVY

        try
        {
            while (true)
            {
                // Lê os dados enviados pelo WAVY
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break; // Se não houver dados, termina

                string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine("[WAVY] " + msg);

                string[] partes = msg.Split(';'); // Separa a mensagem por ';'

                switch (partes[0]) // Verifica o tipo de comando
                {
                    case "HELLO":
                        // Registra o ID do WAVY
                        wavyId = partes[1];
                        Send(stream, "HELLO_ACK"); // Confirma receção
                        break;

                    case "REGISTER":
                        Send(stream, "REGISTER_ACK"); // Confirma registo
                        break;

                    case "DATA":
                        // Extrai o sensor e o valor
                        string sensor = partes[1];
                        string valor = partes[2];

                        // Guarda os dados num ficheiro CSV
                        GravarEmCsv(wavyId, sensor, valor);

                        // 🔄 Chama o serviço gRPC para pré-processar os dados (porta 5122)
                        string dadoFormatado = await ChamarServicoGrpc(wavyId, sensor, valor, "http://localhost:5122");

                        // Envia os dados para o servidor central
                        EnviarParaServidor(dadoFormatado);

                        Send(stream, "RECEIVED"); // Confirma receção
                        break;

                    case "BYE":
                        Send(stream, "BYE_ACK"); // Confirma despedida
                        return; // Termina a ligação

                    default:
                        Send(stream, "ACK"); // Resposta genérica
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro na WAVY: " + ex.Message);
        }
    }

    // Guarda os dados num ficheiro CSV organizado por sensor
    static void GravarEmCsv(string wavyId, string sensor, string valor)
    {
        string pasta = "sensores"; // Pasta onde os ficheiros são guardados
        string ficheiro = Path.Combine(pasta, $"{sensor}.csv"); // Ficheiro por sensor
        string linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{wavyId};{valor}"; // Formato CSV

        Directory.CreateDirectory(pasta); // Cria a pasta se não existir

        // Garante que cada sensor tem o seu próprio lock (evita concorrência)
        lock (locksPorSensor)
        {
            if (!locksPorSensor.ContainsKey(sensor))
                locksPorSensor[sensor] = new object();
        }

        // Bloqueia o acesso ao ficheiro para evitar escrita simultânea
        lock (locksPorSensor[sensor])
        {
            // Regista o WAVY no sensor correspondente
            if (!wavysPorSensor.ContainsKey(sensor))
                wavysPorSensor[sensor] = new HashSet<string>();

            // Se for a primeira vez que este WAVY envia dados para este sensor,
            // adiciona um cabeçalho ao ficheiro CSV
            bool primeiraVez = wavysPorSensor[sensor].Add(wavyId);

            if (primeiraVez)
            {
                File.AppendAllText(ficheiro, $"# --- {wavyId} ---{Environment.NewLine}");
            }

            // Guarda a linha no ficheiro CSV
            File.AppendAllText(ficheiro, linha + Environment.NewLine);
        }
    }

    // Envia os dados formatados para o servidor central (porta 6000)
    static void EnviarParaServidor(string msg)
    {
        try
        {
            // Estabelece ligação com o servidor
            using TcpClient serverClient = new TcpClient("127.0.0.1", 6000);
            using NetworkStream stream = serverClient.GetStream();

            // Envia a mensagem
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);

            // Lê a resposta do servidor
            byte[] buffer = new byte[256];
            int bytes = stream.Read(buffer, 0, buffer.Length);
            string resposta = Encoding.UTF8.GetString(buffer, 0, bytes);
            Console.WriteLine($"[SERVIDOR] {resposta}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao contactar servidor: " + ex.Message);
        }
    }

    // Envia uma resposta ao dispositivo WAVY
    static void Send(NetworkStream stream, string resposta)
    {
        byte[] respostaBytes = Encoding.UTF8.GetBytes(resposta);
        stream.Write(respostaBytes, 0, respostaBytes.Length);
    }

    // Chama o serviço gRPC para formatar os dados do sensor
    static async Task<string> ChamarServicoGrpc(string wavyId, string sensor, string valor, string grpcUrl)
    {
        try
        {
            // Cria um canal gRPC para comunicar com o serviço de pré-processamento
            using var channel = GrpcChannel.ForAddress(grpcUrl);
            var client = new Preprocessor.PreprocessorClient(channel);

            // Prepara os dados para enviar ao serviço gRPC
            var request = new SensorData
            {
                Sensor = sensor,
                Value = valor,
                WavyId = wavyId
            };

            // Chama o método remoto e recebe a resposta
            var reply = await client.FormatSensorDataAsync(request);
            Console.WriteLine($"[gRPC JSON] {reply.Formatted}");
            return reply.Formatted; // Retorna os dados formatados
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao chamar gRPC: " + ex.Message);
            // Se o gRPC falhar, usa um formato de fallback simples
            return $"AGG_DATA;{wavyId};{sensor};{valor}";
        }
    }
}