using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WavyApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string aggregatorIp = "127.0.0.1"; // ou IP real se for outro PC
            int port = 5000;
            string wavyId = "WAVY_001";

            try
            {
                using (TcpClient client = new TcpClient(aggregatorIp, port))
                using (NetworkStream stream = client.GetStream())
                {
                    // Enviar mensagem de HELLO com o ID
                    SendMessage(stream, $"HELLO;{wavyId}");
                    Console.WriteLine("HELLO enviado.");

                    // Esperar ACK
                    string response = ReceiveMessage(stream);
                    Console.WriteLine("Resposta do Agregador: " + response);

                    // Enviar dados simulados
                    string[] dataSamples =
                    {
                        "DATA;TemperaturaAgua;20.5",
                        "DATA;SalinidadeAgua;35",
                        "DATA;AlturaOndas;2.3",
                        "DATA;VelocidadeVento;15.2"
                    };

                    foreach (var data in dataSamples)
                    {
                        SendMessage(stream, data);
                        Console.WriteLine("Enviado: " + data);
                        Thread.Sleep(1000); // Espera 1 segundo entre mensagens
                    }

                    // Enviar BYE e fechar
                    SendMessage(stream, "BYE");
                    Console.WriteLine("BYE enviado.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
            }

            Console.WriteLine("Pressiona qualquer tecla para sair...");
            Console.ReadKey();
        }

        static void SendMessage(NetworkStream stream, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);
        }

        static string ReceiveMessage(NetworkStream stream)
        {
            byte[] buffer = new byte[256];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
    }
}