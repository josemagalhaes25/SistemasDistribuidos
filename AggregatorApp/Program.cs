using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

class Program
{
    // Dicionário para armazenar os sensores registados por cada dispositivo Wavy
    static Dictionary<string, List<string>> sensoresPorWavy = new();

    // Caminho para a pasta de configuração onde serão guardados os ficheiros
    static string configPath = "config";

    static void Main(string[] args)
    {
        // Cria a pasta de configuração se não existir
        Directory.CreateDirectory(configPath);

        // Configura o servidor TCP na porta 5000
        int port = 5000;
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("[Agregador] A escutar na porta 5000...");

        // Loop principal do servidor para aceitar ligações de clientes
        while (true)
        {
            // Aceita uma nova ligação de cliente
            TcpClient client = server.AcceptTcpClient();

            // Cria uma thread separada para lidar com cada cliente
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    // Método para lidar com a comunicação de cada cliente Wavy
    static void HandleClient(TcpClient client)
    {
        string wavyId = "";

        // Obtém o endereço IP do cliente Wavy
        string wavyIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[256];
            int bytesRead;

            // Fica à espera de mensagens do cliente
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("[Recebido] " + message);

                // Divide a mensagem recebida em partes
                string[] parts = message.Split(';');
                string response = "ACK"; // Resposta padrão

                // Processa diferentes tipos de mensagens
                if (parts[0] == "HELLO" && parts.Length == 2)
                {
                    // Mensagem de apresentação do Wavy
                    wavyId = parts[1];
                    response = "ACK";
                }
                else if (parts[0] == "REGISTER" && parts.Length == 2)
                {
                    // Mensagem de registo de sensores
                    List<string> sensores = new(parts[1].Split(','));
                    sensoresPorWavy[wavyId] = sensores;

                    // Guarda a configuração em ficheiro
                    GravarConfig(wavyId, sensores, wavyIp);
                    response = "ACK";
                }
                else if (parts[0] == "DATA" && parts.Length == 3)
                {
                    // Mensagem com dados de sensores
                    string tipo = parts[1];
                    string valor = parts[2];

                    // Verifica se o sensor está registado
                    if (sensoresPorWavy.ContainsKey(wavyId) && sensoresPorWavy[wavyId].Contains(tipo))
                    {
                        // Guarda os dados em ficheiro CSV
                        GuardarEmCSV(tipo, wavyId, valor, wavyIp);

                        // Reencaminha os dados para o servidor principal
                        ForwardToServer(tipo, valor);
                        response = "ACK";
                    }
                    else
                    {
                        response = "ERROR;Sensor não registado";
                    }
                }
                else if (message == "BYE")
                {
                    // Mensagem de despedida
                    response = "BYE_ACK";
                    byte[] byeResponse = Encoding.UTF8.GetBytes(response);
                    stream.Write(byeResponse, 0, byeResponse.Length);
                    break; // Termina a ligação
                }
                else
                {
                    // Mensagem não reconhecida
                    response = "ERROR;Mensagem inválida";
                }

                // Envia a resposta ao cliente
                byte[] reply = Encoding.UTF8.GetBytes(response);
                stream.Write(reply, 0, reply.Length);
            }
        }
        client.Close(); // Fecha a ligação com o cliente
    }

    // Grava as informações de registo no ficheiro config_wavys.csv
    static void GravarConfig(string wavyId, List<string> sensores, string ip)
    {
        string linha = $"{wavyId}:ativa:[{string.Join(",", sensores)}]:{DateTime.Now:yyyy-MM-ddTHH:mm:ss}";
        File.AppendAllText(Path.Combine(configPath, "config_wavys.csv"), linha + Environment.NewLine);
    }

    // Guarda os dados em ficheiros CSV separados por tipo de sensor
    static void GuardarEmCSV(string tipo, string wavyId, string valor, string ip)
    {
        string linha = $"{wavyId}:media:{valor}:{ip}";
        string ficheiro = Path.Combine(configPath, $"{tipo}.csv");
        File.AppendAllText(ficheiro, linha + Environment.NewLine);
    }

    // Reencaminha os dados para o servidor principal (na porta 6000)
    static void ForwardToServer(string tipo, string valor)
    {
        try
        {
            using (TcpClient serverClient = new TcpClient("127.0.0.1", 6000))
            using (NetworkStream stream = serverClient.GetStream())
            {
                string msg = $"FORWARD;AGG_DATA;{tipo};{valor}";
                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[Erro ao contactar servidor] " + e.Message);
        }
    }
}