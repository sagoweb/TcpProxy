using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace TcpProxy
{
    class Program
    {
        enum Alignment
        {
            Left,
            Right
        }

        static object _lock = new object();

        static int inboundPort;
        static string outboundAddress;
        static int outboundPort;

        static string inboundAddress;
        private static int LineLength = 10;

        public static int WindowWidth { get; private set; } = 120;
        public static int MessageInfoLeftPadding { get; private set; } = 30;

        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: TcpProxy <inboundPort> <outboundAddress:outboundPort>");
                return;
            }

            if (!int.TryParse(args[0], out inboundPort))
            {
                Console.WriteLine("Invalid inboundPort value. Please provide a valid integer value.");
                return;
            }

            //check that the second argument is in the format address:port using a regular expression
            if (!Regex.IsMatch(args[1], @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:\d{1,5}$"))
            {
                Console.WriteLine("Invalid outboundAddress:outboundPort value. Please provide a valid address:port value.");
                return;
            }

            outboundAddress = args[1].Split(':')[0];

            if (!IPAddress.TryParse(outboundAddress, out IPAddress _))
            {
                Console.WriteLine("Invalid outboundAddress value. Please provide a valid IP address.");
                return;
            }

            string port = args[1].Split(':')[1];
            if (!int.TryParse(port, out outboundPort))
            {
                Console.WriteLine("Invalid outboundPort value. Please provide a valid integer value.");
                return;
            }

            Console.Title = $"{inboundPort} -> {outboundAddress}:{outboundPort}";

            await StartListener(inboundPort, outboundAddress, outboundPort);
        }

        static void PrintMessage(byte[] messageContent, Alignment alignment)
        {
            lock (_lock)
            {
                Console.ForegroundColor = alignment == Alignment.Left ? ConsoleColor.Green : ConsoleColor.Blue;

                string messageInfo = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                messageInfo = messageInfo.PadLeft(messageInfo.Length + MessageInfoLeftPadding);
                Console.WriteLine(messageInfo);

                messageInfo = (alignment == Alignment.Left ? $"{inboundAddress} -> {outboundAddress}" : $"{outboundAddress} -> {inboundAddress}");

                messageInfo = messageInfo.PadLeft(messageInfo.Length + MessageInfoLeftPadding);
                Console.WriteLine(messageInfo);

                messageInfo = "Message length: " + messageContent.Length;
                messageInfo = messageInfo.PadLeft(messageInfo.Length + MessageInfoLeftPadding);
                Console.WriteLine(messageInfo);
                Console.WriteLine();

                int padding = 0;
                for (int i = 0; i < messageContent.Length; i += LineLength)
                {
                    string hexLine = string.Join(" ", messageContent.Skip(i).Take(LineLength).Select(b => b.ToString("X2"))).PadRight(31);
                    // for each line concatenate the ascii representation of the bytes
                    hexLine += "  | " + string.Join("", messageContent.Skip(i).Take(LineLength).Select(b => b < 32 ? '.' : (char)b)).PadRight(LineLength);

                    // then concatenate the decimal representation of the bytes
                    hexLine += "  | " + string.Join(" ", messageContent.Skip(i).Take(LineLength).Select(b => b.ToString().PadLeft(3))).PadRight(40) + "|";

                    if (alignment == Alignment.Right)
                    {
                        padding = WindowWidth - LineLength;
                        if (padding > 0)
                            hexLine = hexLine.PadLeft(LineLength + padding);
                    }

                    Console.WriteLine(hexLine);
                }

                // Write a line of dashes to separate messages
                Console.WriteLine(new string('-', WindowWidth));
                Console.ResetColor();
            }
        }


        static async Task StartListener(int inboundPort, string outboundAddress, int outboundPort)
        {
            TcpListener server = new TcpListener(IPAddress.Any, inboundPort);
            server.Start();
            Console.WriteLine($"Server started on port {inboundPort}");

            while (true)
            {
                TcpClient inboundClient = await server.AcceptTcpClientAsync();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Accepted client from {0}", inboundClient.Client.RemoteEndPoint);
                Console.Title = $"{inboundClient.Client.RemoteEndPoint.ToString().Split(':')[0]}:{inboundPort} -> {outboundAddress}:{outboundPort}";
                inboundAddress = ((IPEndPoint)inboundClient.Client.RemoteEndPoint).Address.ToString();
                Console.ResetColor();

                TcpClient outboundClient = null;
                bool connected = false;

                while (!connected)
                {
                    try
                    {
                        outboundClient = new TcpClient(outboundAddress, outboundPort);
                        connected = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Failed to connect to outbound address {outboundAddress}:{outboundPort}. Retrying in 1 second...");
                        await Task.Delay(1000);
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Connected to address {outboundAddress}:{outboundPort}");
                Console.ResetColor();

                NetworkStream inboundStream = inboundClient.GetStream();
                NetworkStream outboundStream = outboundClient.GetStream();

                byte[] writeBuffer = new byte[1024];
                byte[] readBuffer = new byte[1024];

                int bytesRead;

                // Read every message from the outbound stream and forward it to the inbound stream
                Task readTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        bytesRead = await outboundStream.ReadAsync(readBuffer, 0, readBuffer.Length);

                        if (bytesRead == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Server closed the connection");
                            Console.ResetColor();
                            break;
                        }

                        // Forward the message to the inbound stream
                        await inboundStream.WriteAsync(readBuffer, 0, bytesRead);

                        PrintMessage(readBuffer.Take(bytesRead).ToArray(), Alignment.Right);
                    }

                    inboundClient.Close();
                });

                // Read every message from the inbound stream and forward it to the outbound stream
                Task writeTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        bytesRead = await inboundStream.ReadAsync(writeBuffer, 0, writeBuffer.Length);

                        // Forward the message to the outbound stream
                        await outboundStream.WriteAsync(writeBuffer, 0, bytesRead);

                        PrintMessage(writeBuffer.Take(bytesRead).ToArray(), Alignment.Left);

                        if (bytesRead == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Client closed the connection");
                            Console.ResetColor();
                            break;
                        }
                    }

                    outboundClient.Close();
                });

                await Task.WhenAny(readTask, writeTask);
            }
        }
    }
}
