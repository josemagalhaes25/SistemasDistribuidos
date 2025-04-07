using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        int port = 5000;
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("Agregador iniciado...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread thread = new Thread(() => HandleClient(client));
            thread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[256];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Recebido do WAVY: " + message);

                if (message.StartsWith("DATA"))
                    ForwardToServer(message);
                else if (message == "BYE")
                {
                    byte[] response = Encoding.UTF8.GetBytes("BYE_ACK");
                    stream.Write(response, 0, response.Length);
                    break;
                }
                else
                {
                    byte[] response = Encoding.UTF8.GetBytes("ACK");
                    stream.Write(response, 0, response.Length);
                }
            }
        }
        client.Close();
    }

    static void ForwardToServer(string data)
    {
        using (TcpClient serverClient = new TcpClient("127.0.0.1", 6000))
        using (NetworkStream stream = serverClient.GetStream())
        {
            byte[] message = Encoding.UTF8.GetBytes("FORWARD;" + data);
            stream.Write(message, 0, message.Length);
        }
    }
}