// Aplicação Agregadora - Recebe dados de sensores de dispositivos WAVY, grava em ficheiros CSV e encaminha para um servidor central

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    // Configuração da porta onde o agregador escuta conexões
    static int porta = 5000;
    static TcpListener listener;

    // Dicionário de locks para proteger escrita concorrente nos ficheiros por sensor
    static readonly Dictionary<string, object> locksPorSensor = new();

    // Dicionário para registar quais WAVYs já escreveram em cada sensor (para inserir separadores)
    static readonly Dictionary<string, HashSet<string>> wavysPorSensor = new();

    static void Main(string[] args)
    {
        // Iniciar o servidor TCP na porta configurada
        listener = new TcpListener(IPAddress.Any, porta);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {porta}...");

        // Ciclo principal - aceita novas conexões e cria threads para as tratar
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread thread = new(() => TratarWavy(client));
            thread.Start();
        }
    }

    // Método que trata a comunicação com cada dispositivo WAVY conectado
    static void TratarWavy(TcpClient client)
    {
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[256];
        string wavyId = ""; // Armazena o identificador da WAVY

        try
        {
            while (true)
            {
                // Ler dados recebidos da WAVY
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;

                string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine("[WAVY] " + msg);

                // Dividir a mensagem pelos separadores ';'
                string[] partes = msg.Split(';');

                // Processar o comando recebido
                switch (partes[0])
                {
                    case "HELLO":
                        // Mensagem de identificação inicial da WAVY
                        wavyId = partes[1];
                        Send(stream, "HELLO_ACK"); // Enviar confirmação
                        break;

                    case "REGISTER":
                        // Mensagem de registo (não usado atualmente)
                        Send(stream, "REGISTER_ACK");
                        break;

                    case "DATA":
                        // Mensagem contendo dados de sensor
                        string sensor = partes[1];
                        string valor = partes[2];

                        // Gravar os dados no ficheiro CSV do sensor
                        GravarEmCsv(wavyId, sensor, valor);

                        // Encaminhar dados para o servidor central
                        string mensagemServer = $"AGG_DATA;{wavyId};{sensor};{valor}";
                        EnviarParaServidor(mensagemServer);

                        Send(stream, "RECEIVED"); // Confirmar receção
                        break;

                    case "BYE":
                        // Mensagem de despedida da WAVY
                        Send(stream, "BYE_ACK");
                        return; // Terminar esta thread

                    default:
                        // Comando não reconhecido - enviar confirmação genérica
                        Send(stream, "ACK");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro na WAVY: " + ex.Message);
        }
    }

    // Método para gravar dados num ficheiro CSV específico do sensor
    static void GravarEmCsv(string wavyId, string sensor, string valor)
    {
        string pasta = "sensores"; // Pasta onde os ficheiros são guardados
        string ficheiro = Path.Combine(pasta, $"{sensor}.csv");
        // Linha a gravar: data/hora, ID WAVY, valor
        string linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{wavyId};{valor}";

        // Criar a pasta se não existir
        Directory.CreateDirectory(pasta);

        // Obter ou criar um lock específico para este sensor
        lock (locksPorSensor)
        {
            if (!locksPorSensor.ContainsKey(sensor))
                locksPorSensor[sensor] = new object();
        }

        // Bloquear o acesso ao ficheiro deste sensor
        lock (locksPorSensor[sensor])
        {
            // Verificar se esta WAVY já escreveu neste sensor antes
            if (!wavysPorSensor.ContainsKey(sensor))
                wavysPorSensor[sensor] = new HashSet<string>();

            // Se for a primeira vez desta WAVY, adicionar um separador
            bool primeiraVez = wavysPorSensor[sensor].Add(wavyId);

            if (primeiraVez)
            {
                File.AppendAllText(ficheiro, $"# --- {wavyId} ---{Environment.NewLine}");
            }

            // Gravar a linha de dados
            File.AppendAllText(ficheiro, linha + Environment.NewLine);
        }
    }

    // Método para enviar dados para o servidor central
    static void EnviarParaServidor(string msg)
    {
        try
        {
            // Conectar ao servidor (assumido como estando em localhost:6000)
            using TcpClient serverClient = new TcpClient("127.0.0.1", 6000);
            using NetworkStream stream = serverClient.GetStream();

            // Enviar a mensagem
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);

            // Esperar pela resposta do servidor
            byte[] buffer = new byte[256];
            int bytes = stream.Read(buffer, 0, buffer.Length);
            string resposta = Encoding.UTF8.GetString(buffer, 0, bytes);
            Console.WriteLine($"[SERVIDOR] {resposta}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao contactar servidor: " + ex.Message);
        }
    }

    // Método auxiliar para enviar respostas para a WAVY
    static void Send(NetworkStream stream, string resposta)
    {
        byte[] respostaBytes = Encoding.UTF8.GetBytes(resposta);
        stream.Write(respostaBytes, 0, respostaBytes.Length);
    }
}