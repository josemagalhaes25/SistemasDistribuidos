using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace WavyApp
{
    internal class Program
    {
        // Identificador único deste dispositivo Wavy
        static string wavyId = "WAVY_005";

        // Endereço IP do agregador (servidor) e porta de comunicação
        static string aggregatorIp = "127.0.0.1";
        static int port = 5000;

        // Cliente TCP e stream de comunicação
        static TcpClient client;
        static NetworkStream stream;

        // Flag para controlar o estado da aplicação
        static bool running = true;

        // Gerador de números aleatórios para simular dados dos sensores
        static Random rand = new();

        // Lista de sensores disponíveis neste dispositivo
        static List<string> sensores = new() { "TemperaturaSuperficial","NivelNitritos","CargaOrganica","PresencaMetaisPesados" };

        static void Main(string[] args)
        {
            // Configura o handler para o evento Ctrl+C (terminar a aplicação)
            Console.CancelKeyPress += (sender, e) => { running = false; };

            try
            {
                // Estabelece ligação com o agregador
                client = new TcpClient(aggregatorIp, port);
                stream = client.GetStream();

                Log("Ligado ao agregador.");

                // Envia mensagem de apresentação e lê a resposta
                SendMessage($"HELLO;{wavyId}");
                Log("HELLO enviado: " + ReceiveMessage());

                // Regista os sensores disponíveis no agregador
                SendMessage($"REGISTER;{string.Join(",", sensores)}");
                Log("REGISTER enviado: " + ReceiveMessage());

                // Inicia uma thread secundária para gerar dados dos sensores
                Thread dataThread = new Thread(GenerateData);
                dataThread.Start();

                // Loop principal (mantém a aplicação ativa)
                while (running) { Thread.Sleep(100); }

                // Processo de encerramento
                SendMessage("BYE");
                Log("BYE enviado: " + ReceiveMessage());

                // Fecha a ligação
                stream.Close();
                client.Close();
                Log("Ligação encerrada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
            }
        }

        // Gera dados aleatórios para cada sensor periodicamente
        static void GenerateData()
        {
            while (running)
            {
                foreach (var sensor in sensores)
                {
                    // Gera um valor aleatório consoante o tipo de sensor
                    string value = sensor switch
                    {
                        "TemperaturaSuperficial" => (10 + rand.NextDouble() * 15).ToString("F1"),      // ºC
                        "NivelNitritos" => (0 + rand.NextDouble() * 5).ToString("F2"),                // mg/L
                        "CargaOrganica" => (0 + rand.NextDouble() * 100).ToString("F0"),              // mg O2/L
                        "PresencaMetaisPesados" => (0 + rand.NextDouble() * 0.5).ToString("F3"),      // mg/L
                        _ => "0"
                    };

                    // Envia os dados para o agregador
                    string dataMsg = $"DATA;{sensor};{value}";
                    SendMessage(dataMsg);
                    Log($"Enviado: {dataMsg} → {ReceiveMessage()}");
                    Thread.Sleep(1000); // Intervalo de 1 segundo entre sensores
                }
            }
        }

        // Envia uma mensagem para o agregador através da stream de rede
        static void SendMessage(string msg)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            stream.Write(buffer, 0, buffer.Length);
        }

        // Recebe uma mensagem do agregador
        static string ReceiveMessage()
        {
            byte[] buffer = new byte[256];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        // Escreve uma mensagem no log com timestamp
        static void Log(string msg)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
    }
}
