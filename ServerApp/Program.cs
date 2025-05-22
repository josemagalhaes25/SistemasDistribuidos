using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;

class Program
{
    // Porta onde o servidor vai escutar
    static int port = 6000;
    // Objeto para escutar conexões TCP
    static TcpListener server;
    // Lista para armazenar os dados recebidos dos clientes
    static List<string> dadosRecebidos = new();
    // Objeto para sincronização de threads (evitar concorrência)
    static object lockObj = new();
    // Objeto para sincronização de acesso ao ficheiro
    static object fileLock = new();

    static void Main(string[] args)
    {
        // Inicia uma thread separada para o servidor
        Thread listenerThread = new(() => StartServer());
        listenerThread.Start();

        // Inicia o loop de comandos na thread principal
        CommandLoop();
    }

    // Método para iniciar o servidor TCP
    static void StartServer()
    {
        server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor a escutar na porta {port}...");

        // Loop infinito para aceitar clientes
        while (true)
        {
            // Aceita uma nova conexão de cliente
            TcpClient client = server.AcceptTcpClient();
            // Cria uma nova thread para lidar com o cliente
            Thread clientThread = new(() => HandleClient(client));
            clientThread.Start();
        }
    }

    // Método para lidar com a comunicação de um cliente
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

            // Tenta interpretar como JSON primeiro
            try
            {
                var dado = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                if (dado != null && dado.ContainsKey("sensor"))
                {
                    // Adiciona à lista de dados recebidos (com thread safety)
                    lock (lockObj)
                    {
                        dadosRecebidos.Add(message);
                    }
                    SaveToFile(message);
                    SendResponse(stream, "RECEIVED");
                    return;
                }
            }
            catch
            {
                // Se falhar o JSON, continua para verificar AGG_DATA
            }

            // Verifica se é um comando AGG_DATA
            if (message.StartsWith("AGG_DATA;"))
            {
                lock (lockObj)
                {
                    dadosRecebidos.Add(message);
                }
                SaveToFile(message);
                SendResponse(stream, "RECEIVED");
            }
            else
            {
                SendResponse(stream, "UNKNOWN_COMMAND");
            }
        }
        catch (Exception ex)
        {
            Log("Erro na receção: " + ex.Message);
        }
        finally
        {
            // Garante que o cliente é fechado
            client.Close();
        }
    }

    // Envia uma resposta ao cliente
    static void SendResponse(NetworkStream stream, string response)
    {
        byte[] data = Encoding.UTF8.GetBytes(response);
        stream.Write(data, 0, data.Length);
    }

    // Guarda os dados recebidos num ficheiro
    static void SaveToFile(string data)
    {
        string folder = "logs";
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "dados_registados.txt");

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string line = $"[{timestamp}] {data}";

        // Garante que apenas uma thread escreve no ficheiro de cada vez
        lock (fileLock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    // Loop para receber comandos do utilizador
    static void CommandLoop()
    {
        while (true)
        {
            Console.Write("> ");
            string command = Console.ReadLine()?.Trim().ToUpper();

            switch (command)
            {
                case "LIST":
                    ListData();
                    break;
                case "STATS":
                    ShowStats();
                    break;
                case "RESET":
                    ResetData();
                    break;
                default:
                    Console.WriteLine("Comando inválido. Use LIST, STATS ou RESET.");
                    break;
            }
        }
    }

    // Lista todos os dados recebidos
    static void ListData()
    {
        lock (lockObj)
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

    // Mostra estatísticas dos dados recebidos
    static void ShowStats()
    {
        Dictionary<string, int> contagens = new();

        lock (lockObj)
        {
            foreach (var linha in dadosRecebidos)
            {
                try
                {
                    // Tenta processar como JSON
                    var dado = JsonSerializer.Deserialize<Dictionary<string, string>>(linha);
                    if (dado != null && dado.ContainsKey("sensor"))
                    {
                        string sensor = dado["sensor"];
                        if (!contagens.ContainsKey(sensor)) contagens[sensor] = 0;
                        contagens[sensor]++;
                    }
                    else if (linha.StartsWith("AGG_DATA;"))
                    {
                        // Processa no formato AGG_DATA
                        string[] partes = linha.Split(';');
                        if (partes.Length >= 3)
                        {
                            string sensor = partes[1];
                            if (!contagens.ContainsKey(sensor)) contagens[sensor] = 0;
                            contagens[sensor]++;
                        }
                    }
                }
                catch
                {
                    // Ignora linhas mal formatadas
                }
            }
        }

        Console.WriteLine("Estatísticas por tipo de sensor:");
        foreach (var kvp in contagens)
        {
            Console.WriteLine($"- {kvp.Key}: {kvp.Value} valores");
        }
    }

    // Limpa todos os dados recebidos
    static void ResetData()
    {
        lock (lockObj)
        {
            dadosRecebidos.Clear();
        }

        string filePath = Path.Combine("logs", "dados_registados.txt");
        if (File.Exists(filePath))
        {
            lock (fileLock)
            {
                File.WriteAllText(filePath, "");
            }
        }

        Console.WriteLine("Dados apagados com sucesso.");
    }

    // Método auxiliar para registar mensagens com timestamp
    static void Log(string msg)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}