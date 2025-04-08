using System;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;

class Program
{
    // Porta onde este agregador escuta as mensagens do WAVY
    static int portaWavy = 5000;
    // Porta e IP do servidor principal onde serão enviados os dados agregados
    static int portaServer = 6000;
    static string ipServer = "127.0.0.1";  // localhost

    static void Main(string[] args)
    {
        // Cria um listener TCP para receber conexões do WAVY
        TcpListener listener = new TcpListener(IPAddress.Any, portaWavy);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {portaWavy}...");

        // Loop infinito para aceitar múltiplos clientes WAVY
        while (true)
        {
            // Aceita uma conexão do WAVY (bloqueante)
            TcpClient wavyClient = listener.AcceptTcpClient();
            // Cria uma thread separada para cada cliente WAVY
            Thread thread = new(() => HandleWavy(wavyClient));
            thread.Start();
        }
    }

    // Método para processar cada cliente WAVY conectado
    static void HandleWavy(TcpClient client)
    {
        using NetworkStream wavyStream = client.GetStream();
        byte[] buffer = new byte[256];  // Buffer de 256 bytes
        int bytesRead;

        try
        {
            // Fica a ler continuamente os dados do WAVY
            while ((bytesRead = wavyStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string mensagem = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Mensagem recebida do WAVY: " + mensagem);

                // Verifica se é uma mensagem de dados (formato: "DATA;...")
                if (mensagem.StartsWith("DATA;"))
                {
                    // Transforma a mensagem para o formato do servidor principal
                    string mensagemServer = $"AGG_DATA;{mensagem}";

                    // Envia para o servidor principal e obtém resposta
                    string resposta = EnviarParaServidor(mensagemServer);
                    Console.WriteLine("Resposta do Servidor: " + resposta);

                    // Envia confirmação de receção ao WAVY
                    SendResponse(wavyStream, "RECEIVED");
                }
                else
                {
                    // Responde com ACK (acknowledgment) para outras mensagens
                    SendResponse(wavyStream, "ACK");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
        finally
        {
            // Garante que a conexão é fechada
            client.Close();
        }
    }

    // Método para enviar mensagens ao servidor principal
    static string EnviarParaServidor(string msg)
    {
        try
        {
            // Cria conexão com o servidor principal
            using TcpClient serverClient = new TcpClient(ipServer, portaServer);
            using NetworkStream stream = serverClient.GetStream();

            // Envia a mensagem
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);

            // Aguarda e lê a resposta do servidor
            byte[] buffer = new byte[256];
            int bytes = stream.Read(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer, 0, bytes);
        }
        catch (Exception ex)
        {
            return "Erro ao contactar servidor: " + ex.Message;
        }
    }

    // Método auxiliar para enviar respostas ao WAVY
    static void SendResponse(NetworkStream stream, string response)
    {
        byte[] data = Encoding.UTF8.GetBytes(response);
        stream.Write(data, 0, data.Length);
    }
}