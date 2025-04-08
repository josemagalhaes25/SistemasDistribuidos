using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        // Configuração do servidor na porta 6000
        int port = 6000;

        // Cria e inicia o listener TCP na porta especificada
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("Servidor iniciado na porta " + port);

        // Loop principal do servidor para aceitar ligações de clientes
        while (true)
        {
            // Aceita uma nova ligação de cliente (agregador)
            TcpClient client = server.AcceptTcpClient();

            // Cria uma thread separada para lidar com cada cliente
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    // Método para processar as mensagens de cada cliente (agregador)
    static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        {
            // Buffer para receber os dados
            byte[] buffer = new byte[256];

            // Lê os dados recebidos do agregador
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Regista a mensagem recebida no log
            Log("Dados recebidos do Agregador: " + message);

            // Verifica se é uma mensagem de dados do agregador
            if (message.StartsWith("AGG_DATA;"))
            {
                // Guarda os dados recebidos num ficheiro
                SaveToFile(message);
            }

            // Envia confirmação de receção ao agregador
            byte[] response = Encoding.UTF8.GetBytes("RECEIVED");
            stream.Write(response, 0, response.Length);
        }

        // Fecha a ligação com o cliente
        client.Close();
    }

    // Método para guardar os dados num ficheiro de log
    static void SaveToFile(string data)
    {
        try
        {
            // Cria a pasta de logs se não existir
            string folderPath = "logs";
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Caminho completo para o ficheiro de log
            string filePath = Path.Combine(folderPath, "dados_registados.txt");

            // Formata a linha com timestamp e dados
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string line = $"[{timestamp}] {data}";

            // Adiciona a linha ao ficheiro
            File.AppendAllText(filePath, line + Environment.NewLine);

            // Regista no log a operação bem sucedida
            Log("Dados guardados em ficheiro: " + filePath);
        }
        catch (Exception ex)
        {
            // Regista eventuais erros
            Log("Erro ao guardar ficheiro: " + ex.Message);
        }
    }

    // Método auxiliar para registar mensagens no console com timestamp
    static void Log(string msg)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}