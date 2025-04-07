using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        // Define a porta onde o Agregador vai escutar conexões
        int port = 5000;

        // Cria um servidor TCP que escuta em todas as interfaces de rede na porta 5000
        TcpListener server = new TcpListener(IPAddress.Any, port);

        // Inicia o servidor
        server.Start();
        Console.WriteLine("Agregador iniciado...");

        // Loop infinito para aceitar várias conexões dos clientes (como o WAVY)
        while (true)
        {
            // Aguarda por uma conexão de um cliente (bloqueante)
            TcpClient client = server.AcceptTcpClient();

            // Cria uma nova thread para tratar o cliente conectado
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    // Função para tratar a comunicação com um cliente específico (neste caso, o WAVY)
    static void HandleClient(TcpClient client)
    {
        // Usa o stream de rede do cliente para ler e escrever dados
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[256]; // Buffer para armazenar dados recebidos
            int bytesRead;

            // Enquanto o cliente estiver a enviar dados
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Converte os bytes recebidos em string (texto)
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Recebido do WAVY: " + message);

                // Se a mensagem começa com "DATA", reencaminha-a para o Server
                if (message.StartsWith("DATA"))
                    ForwardToServer(message);

                // Se a mensagem for "BYE", envia confirmação e termina a ligação
                else if (message == "BYE")
                {
                    byte[] response = Encoding.UTF8.GetBytes("BYE_ACK");
                    stream.Write(response, 0, response.Length);
                    break; // Sai do ciclo e fecha a ligação
                }

                // Caso contrário, envia apenas um "ACK"
                else
                {
                    byte[] response = Encoding.UTF8.GetBytes("ACK");
                    stream.Write(response, 0, response.Length);
                }
            }
        }

        // Fecha a ligação com o cliente
        client.Close();
    }

    // Função que reencaminha dados para o servidor final (na porta 6000)
    static void ForwardToServer(string data)
    {
        // Conecta ao servidor na porta 6000 (localhost)
        using (TcpClient serverClient = new TcpClient("127.0.0.1", 6000))
        using (NetworkStream stream = serverClient.GetStream())
        {
            // Prepara os dados com o prefixo "FORWARD;" e envia
            byte[] message = Encoding.UTF8.GetBytes("FORWARD;" + data);
            stream.Write(message, 0, message.Length);
        }
    }
}