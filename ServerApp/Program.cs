using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        // Define a porta onde o servidor vai escutar conexões
        int port = 6000;

        // Cria um servidor TCP que escuta em todas as interfaces de rede, na porta definida
        TcpListener server = new TcpListener(IPAddress.Any, port);

        // Inicia o servidor
        server.Start();
        Console.WriteLine("Servidor iniciado...");

        // Loop infinito para aceitar múltiplas conexões de clientes
        while (true)
        {
            // Aguarda (bloqueia) até que um cliente se conecte
            TcpClient client = server.AcceptTcpClient();

            // Cria uma nova thread para tratar o cliente, permitindo que o servidor continue a escutar outros
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    // Função que trata a comunicação com um cliente individual
    static void HandleClient(TcpClient client)
    {
        // Usa um bloco 'using' para garantir que o stream seja fechado corretamente
        using (NetworkStream stream = client.GetStream())
        {
            // Cria um buffer para armazenar os dados recebidos
            byte[] buffer = new byte[256];

            // Lê os dados enviados pelo cliente
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            // Converte os bytes recebidos numa string (texto) em UTF-8
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Escreve no ecrã a mensagem recebida do cliente
            Console.WriteLine("Dado recebido do Agregador: " + message);

            // Cria uma resposta em formato de bytes
            byte[] response = Encoding.UTF8.GetBytes("RECEIVED");

            // Envia a resposta de volta ao cliente
            stream.Write(response, 0, response.Length);
        }

        // Fecha a ligação com o cliente
        client.Close();
    }
}