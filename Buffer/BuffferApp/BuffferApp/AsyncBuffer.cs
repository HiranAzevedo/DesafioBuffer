using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BuffferApp
{
    public class StateObject
    {
        // Client  socket.
        public Socket WorkSocket = null;
        // Tamanho do buffer de dados.
        public const int BufferSize = 1024;
        // Buffer de dados.
        public byte[] Buffer = new byte[BufferSize];
        // String recebida.
        public StringBuilder Sb = new StringBuilder();
    }

    public class BufferAsync
    {
        // Sinal da Thread
        public static ManualResetEvent AllDone = new ManualResetEvent(false);

        //Lista armazenando os inteiros(BUFFER)
        public static ConcurrentQueue<int> BuffList = new ConcurrentQueue<int>();

        public static int PortNumber = 11000;
        private static int MaxSizeBuff = 5;

        public static void StartListening()
        {
            // Buffer para recebimento dos dados.
            byte[] bytes = new Byte[1024];

            // Setando o endpoint local para o socket.            
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PortNumber);

            // Criando o socket TCP/IP.
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Faz bind do socket para o endpoit local e aguarda conexões.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                Console.WriteLine("Esperando uma conexão...");
                while (true)
                {
                    // Reseta o sinal do evento.
                    AllDone.Reset();

                    // Starta um socket async para esperar por conexões.
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    // Aguarda uma conexão antes de continuar
                    AllDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Sinaliza a thread principal para continuar.
            AllDone.Set();

            // Cria um socket para lidar com o request desse cliente.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Cria o objeto de estado dessa conexão
            StateObject state = new StateObject();
            state.WorkSocket = handler;
            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Recupera o objeto temporario e o socket para essa conexão.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.WorkSocket;

            // Le os dados do cliente no socket
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // Talvez haja mais dados, então coloca na string e continua recebendo.
                state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));

                // Verifica se tem o EOF tag, se não tiver aqui continua lendo os dados.
                content = state.Sb.ToString();
                if (content.Contains("Add") && content.IndexOf("<EOF>") > -1)
                {
                    // Todos os dados foram lidos do cliente
                    // Adiciona os valores enviados para o buffer                 
                    int sLengthNumber = content.IndexOf('[') + 1;
                    int fLengthNumber = (content.IndexOf(']') - sLengthNumber);
                    int number = Convert.ToInt32(content.Substring(sLengthNumber, fLengthNumber));
                    int sNameLength = content.IndexOf('{') + 1;
                    int fNameLength = content.IndexOf('}') - sNameLength;
                    string producerName = content.Substring(sNameLength, fNameLength);

                    // Envia resposta para o cliente
                    if (BuffList.Count() >= MaxSizeBuff)
                    {
                        Console.WriteLine("O buffer está cheio");
                        Send(handler, "FULL");
                    }
                    else
                    {
                        Console.WriteLine("Valor {0} adicionado em Buffer pelo Produtor {1}", number, producerName);
                        BuffList.Enqueue(number);
                        Send(handler, "OK ADD");
                    }
                }
                else if (content.Contains("DROP") && content.IndexOf("<EOF>") > -1)
                {
                    int number = -1;
                    if (BuffList.Count() > 0)
                    {
                        bool result = false;
                        while (!result)
                        {
                            result = BuffList.TryDequeue(out number);
                        }
                    }

                    int sNameLength = content.IndexOf('{') + 1;
                    int fNameLength = content.IndexOf('}') - sNameLength;
                    string producerName = content.Substring(sNameLength, fNameLength);

                    // Envia resposta para o cliente
                    if (number == -1)
                    {
                        Console.WriteLine("O buffer está vazio");
                        Send(handler, "EMPTY");
                    }
                    else
                    {
                        Console.WriteLine("Valor {0} removido em Buffer pelo Produtor {1}", number, producerName);
                        Send(handler, "OK DROP [" + number + "]");
                    }
                }
                else
                {
                    // Nem todos os dados foram recebidos, pegar o resto.
                    handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
                Console.WriteLine("Buffer -> [" + string.Join(",", BuffList.ToArray()) + "]");
            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert os dados da string em bytes para serem enviados usando ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            

            // Começa a transação para enviar os dados para o cliente.
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Recupera o socket do objeto de estado.
                Socket handler = (Socket)ar.AsyncState;

                // Envia os dados para o dispositivo cliente.
                int bytesSent = handler.EndSend(ar);
                

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }
        static void Main(string[] args)
        {
            var ip = Dns.Resolve(Dns.GetHostName()).AddressList.FirstOrDefault();
            Console.WriteLine("Olá, este é o buffer server, está rodando no ip: " + ip);
            Console.WriteLine(" Por favor escolha a porta:");
            BufferAsync.PortNumber = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("Por favor entre com a capacidade max do buffer:");
            string buffSizeStr = Console.ReadLine();
            BufferAsync.MaxSizeBuff = Convert.ToInt32(buffSizeStr);
            StartListening();
        }
    }
}
