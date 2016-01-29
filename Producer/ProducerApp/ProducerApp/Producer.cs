using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerApp
{

    // Objeto de estado para receber dados do servidor.
    public class StateObject
    {
        // Socket cliente.
        public Socket workSocket = null;
        // Tamanho do buffer de recebimento de dados.
        public const int BufferSize = 256;
        // Buffer de dados.
        public byte[] buffer = new byte[BufferSize];
        // String de retorno do servidor.
        public StringBuilder sb = new StringBuilder();
    }

    public class ProducerAsync
    {
        // Numero da porta de conexão com o servidor.
        private static int port = 11000;

        // Ip do servidor
        private static string ip = "192.168.88.51";

        private static CountdownEvent countdown;

        // ManualResetEvent instancias que servem de flags.
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        // A resposta do servidor.
        private static String response = String.Empty;

        private static bool StartClient(string taskName)
        {
            // Conecta com o servidor
            IPHostEntry ipHostInfo = Dns.Resolve(ip);
            Stopwatch clock = new Stopwatch();
            IPAddress ipAddress = ipHostInfo.AddressList[0];

            while (true)
            {
                try
                {
                    // Configura o endpoint com as informações do servidor
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                    clock.Reset();

                    // Create a TCP/IP socket.
                    Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    // Connect to the remote endpoint.
                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    // Send test data to the remote device.
                    int random = new Random().Next(999);
                    clock.Start();
                    Send(client, "Add by produtor {" + taskName + "} [" + random + "] <EOF>");
                    sendDone.WaitOne();

                    // Receive the response from the remote device.
                    Receive(client);
                    receiveDone.WaitOne();
                    clock.Stop();

                    if (response.ToLower().Equals("ok add"))
                    {
                        Console.WriteLine("Colocado o valor {0} no Buffer pelo Produtor {1} em {2} segundos", random, taskName, clock.Elapsed.TotalSeconds);
                        Thread.Sleep(1000);
                    }
                    else if (response.ToLower().Equals("full"))
                    {
                        Console.WriteLine("Produtor {0} tentou colocar item no Buffer cheio", taskName);
                        Thread.Sleep(5000);
                    }

                    // Release the socket.
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    Thread.Sleep(2000);
                }
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        public static int Main(String[] args)
        {
            Console.WriteLine("Olá este é o produtor de dados,");
            Console.WriteLine("Informe o ip do servidor buffer:");
            string ipServer = Console.ReadLine();
            ProducerAsync.ip = ipServer;
            Console.WriteLine("Informe a porta do servidor:");
            int portNumber = Convert.ToInt32(Console.ReadLine());
            ProducerAsync.port = portNumber;
            Console.WriteLine("Informe a quantidade de threads que devem estar rodando no produtor:");
            int threadCount = Convert.ToInt32(Console.ReadLine());
            List<Task> taskList = new List<Task>();
            for (int i = 0; i < threadCount; i++)
            {
                string taskName = "Produtor " + i;
                var task = Task.Factory.StartNew(() => StartClient(taskName));
                taskList.Add(task);
            }
            Task.WaitAll(taskList.ToArray());

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
            return 0;
        }
    }
}
