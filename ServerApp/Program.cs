using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

class Program
{
    // Porta onde o servidor irá escutar
    static int port = 6000;
    // Objeto para escutar conexões TCP
    static TcpListener server;
    // Lista para armazenar todos os dados recebidos (thread-safe)
    static List<string> dadosRecebidos = new();
    // Objeto para sincronização de acesso à lista de dados
    static object lockObj = new();

    static void Main(string[] args)
    {
        // Inicia o servidor numa thread separada para não bloquear a thread principal
        Thread listenerThread = new(() => StartServer());
        listenerThread.Start();

        // Entra no modo de comandos (LIST, STATS, RESET) na thread principal
        CommandLoop();
    }

    // Método para iniciar o servidor TCP
    static void StartServer()
    {
        server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor a escutar na porta {port}...");

        while (true)
        {
            // Aceita clientes de forma bloqueante
            TcpClient client = server.AcceptTcpClient();
            // Cria uma thread separada para cada cliente
            Thread clientThread = new(() => HandleClient(client));
            clientThread.Start();
        }
    }

    // Método para lidar com cada cliente conectado
    static void HandleClient(TcpClient client)
    {
        try
        {
            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            // Lê os dados enviados pelo cliente
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            Log("Mensagem recebida: " + message);

            // Verifica se é um comando AGG_DATA (formato esperado: "AGG_DATA;sensor;valor")
            if (message.StartsWith("AGG_DATA;"))
            {
                lock (lockObj) // Bloqueia o acesso à lista para evitar condições de corrida
                {
                    dadosRecebidos.Add(message);
                }

                // Guarda os dados num ficheiro
                SaveToFile(message);
                // Envia confirmação de receção ao cliente
                SendResponse(stream, "RECEIVED");
            }
            else
            {
                // Responde com erro para comandos desconhecidos
                SendResponse(stream, "UNKNOWN_COMMAND");
            }
        }
        catch (Exception ex)
        {
            Log("Erro na receção: " + ex.Message);
        }
        finally
        {
            // Garante que o cliente é fechado mesmo que ocorra um erro
            client.Close();
        }
    }

    // Método para enviar resposta ao cliente
    static void SendResponse(NetworkStream stream, string response)
    {
        byte[] data = Encoding.UTF8.GetBytes(response);
        stream.Write(data, 0, data.Length);
    }

    // Método para guardar dados num ficheiro de log
    static void SaveToFile(string data)
    {
        string folder = "logs";
        Directory.CreateDirectory(folder); // Cria a pasta se não existir
        string path = Path.Combine(folder, "dados_registados.txt");

        // Adiciona timestamp à linha de log
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string line = $"[{timestamp}] {data}";

        // Adiciona a linha ao ficheiro
        File.AppendAllText(path, line + Environment.NewLine);
    }

    // Loop de comandos para interação com o utilizador
    static void CommandLoop()
    {
        while (true)
        {
            Console.Write("> ");
            string command = Console.ReadLine()?.Trim().ToUpper();

            switch (command)
            {
                case "LIST":
                    ListData(); // Lista todos os dados recebidos
                    break;
                case "STATS":
                    ShowStats(); // Mostra estatísticas por tipo de sensor
                    break;
                case "RESET":
                    ResetData(); // Apaga todos os dados
                    break;
                default:
                    Console.WriteLine("Comando inválido. Use LIST, STATS ou RESET.");
                    break;
            }
        }
    }

    // Método para listar todos os dados recebidos
    static void ListData()
    {
        lock (lockObj) // Bloqueia o acesso à lista durante a leitura
        {
            if (dadosRecebidos.Count == 0)
            {
                Console.WriteLine("Nenhum dado recebido.");
                return;
            }

            foreach (var linha in dadosRecebidos)
            {
                Console.WriteLine(linha);
            }
        }
    }

    // Método para mostrar estatísticas por tipo de sensor
    static void ShowStats()
    {
        Dictionary<string, int> contagens = new();

        lock (lockObj)
        {
            foreach (var linha in dadosRecebidos)
            {
                // Formato esperado: "AGG_DATA;sensor;valor"
                string[] partes = linha.Split(';');
                if (partes.Length >= 3)
                {
                    string sensor = partes[1];
                    if (!contagens.ContainsKey(sensor))
                        contagens[sensor] = 0;
                    contagens[sensor]++;
                }
            }
        }

        Console.WriteLine("Estatísticas por tipo de sensor:");
        foreach (var kvp in contagens)
        {
            Console.WriteLine($"- {kvp.Key}: {kvp.Value} valores");
        }
    }

    // Método para apagar todos os dados
    static void ResetData()
    {
        lock (lockObj)
        {
            dadosRecebidos.Clear();
        }

        // Também limpa o ficheiro de log
        string filePath = Path.Combine("logs", "dados_registados.txt");
        if (File.Exists(filePath))
        {
            File.WriteAllText(filePath, "");
        }

        Console.WriteLine("Dados apagados com sucesso.");
    }

    // Método auxiliar para logging com timestamp
    static void Log(string msg)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}