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
            // Define o IP do Agregador (pode ser 127.0.0.1 se estiver localmente)
            string aggregatorIp = "127.0.0.1";
            int port = 5000;

            // ID único deste dispositivo WAVY
            string wavyId = "WAVY_001";

            try
            {
                // Cria uma ligação TCP com o Agregador
                using (TcpClient client = new TcpClient(aggregatorIp, port))
                using (NetworkStream stream = client.GetStream())
                {
                    // Envia uma mensagem inicial de "HELLO" com o ID do dispositivo
                    SendMessage(stream, $"HELLO;{wavyId}");
                    Console.WriteLine("HELLO enviado.");

                    // Aguarda resposta do Agregador (espera um ACK ou algo semelhante)
                    string response = ReceiveMessage(stream);
                    Console.WriteLine("Resposta do Agregador: " + response);

                    // Simula o envio de dados de sensores
                    string[] dataSamples =
                    {
                        "DATA;TemperaturaAgua;20.5",
                        "DATA;SalinidadeAgua;35",
                        "DATA;AlturaOndas;2.3",
                        "DATA;VelocidadeVento;15.2"
                    };

                    // Envia cada amostra de dados com 1 segundo de intervalo
                    foreach (var data in dataSamples)
                    {
                        SendMessage(stream, data);
                        Console.WriteLine("Enviado: " + data);
                        Thread.Sleep(1000); // Pausa de 1 segundo entre mensagens
                    }

                    // Após envio dos dados, envia mensagem de despedida "BYE"
                    SendMessage(stream, "BYE");
                    Console.WriteLine("BYE enviado.");
                }
            }
            catch (Exception ex)
            {
                // Em caso de erro na ligação ou comunicação
                Console.WriteLine("Erro: " + ex.Message);
            }

            // Espera o utilizador pressionar uma tecla antes de encerrar o programa
            Console.WriteLine("Pressiona qualquer tecla para sair...");
            Console.ReadKey();
        }

        // Função que envia uma mensagem para o Agregador via stream
        static void SendMessage(NetworkStream stream, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);
        }

        // Função que recebe uma mensagem da stream do Agregador
        static string ReceiveMessage(NetworkStream stream)
        {
            byte[] buffer = new byte[256];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
    }
}