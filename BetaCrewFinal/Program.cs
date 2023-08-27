using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace BetaCrewClient
{
    class BCClient
    {
        static void Main(string[] args)
        {
            string serverIp = "127.0.0.1";
            int serverPort = 3000;

            try
            {
                // Connect to the server
                using (TcpClient client = new TcpClient(serverIp, serverPort))
                using (NetworkStream stream = client.GetStream())
                {
                    // Prepare a call to stream all packets
                    byte[] requestPayload = { 1, 0 }; // callType = 1, resendSeq = 0
                    stream.Write(requestPayload, 0, requestPayload.Length);

                    Console.WriteLine("Request sent!");

                    // Receive and process response
                    List<Packet> packets = ReceivePackets(stream);

                    Console.WriteLine("Initial Packets received: " + packets.Count);
                    Console.WriteLine("Requesting Missing packets!");

                    // Make a list of received packet sequence numbers
                    List<int> receivedSequences = new List<int>();
                    foreach (Packet packet in packets)
                        receivedSequences.Add(packet.Sequence);

                    Console.WriteLine();

                    List<int> missingSequences = FindMissingSequences(receivedSequences);

                    foreach (int sequence in missingSequences)
                    {
                        using (TcpClient resendClient = new TcpClient(serverIp, serverPort))
                        using (NetworkStream resendStream = resendClient.GetStream())
                        {
                            Console.WriteLine("Requesting packet: " + sequence);
                            byte[] resendRequest = { 2, (byte)sequence };
                            resendStream.Write(resendRequest, 0, resendRequest.Length);

                            // Receive and process the missing packet
                            Packet missingPacket = ReceivePacket(resendStream);
                            if (!receivedSequences.Contains(missingPacket.Sequence))
                            {
                                packets.Add(missingPacket);
                                Console.WriteLine("Packet added: " + missingPacket.Sequence);
                            }
                            else
                            {
                                Console.WriteLine("Packet already exists!");
                            }
                            Console.WriteLine();
                        }
                    }

                    Console.WriteLine("Missing packets received!");

                    // Generate JSON output
                    GenerateJsonOutput(packets);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static List<int> FindMissingSequences(List<int> receivedSequences)
        {
            List<int> missingSequences = new List<int>();
            for (int i = 1; i < receivedSequences.Last(); i++)
            {
                if (!receivedSequences.Contains(i))
                    missingSequences.Add(i);
            }

            return missingSequences;
        }

        static List<Packet> ReceivePackets(NetworkStream stream)
        {
            List<Packet> packets = new List<Packet>();
            bool receivedAllPackets = false;

            while (!receivedAllPackets)
            {
                Packet packet = ReceivePacket(stream);
                packets.Add(packet);

                // Check for end of stream (server closed connection)
                if (stream.DataAvailable)
                    continue;
                else
                    receivedAllPackets = true;
            }

            return packets;
        }

        static Packet ReceivePacket(NetworkStream stream)
        {
            // Parsing bytes to objects
            byte[] symbolBytes = new byte[4];
            byte[] buySellIndicatorBytes = new byte[1];
            byte[] quantityBytes = new byte[4];
            byte[] priceBytes = new byte[4];
            byte[] sequenceBytes = new byte[4];

            stream.Read(symbolBytes, 0, 4);
            stream.Read(buySellIndicatorBytes, 0, 1);
            stream.Read(quantityBytes, 0, 4);
            stream.Read(priceBytes, 0, 4);
            stream.Read(sequenceBytes, 0, 4);

            // Console.WriteLine("Quantity: ");
            // PrintByteArray(sequenceBytes);

            Array.Reverse(quantityBytes);
            Array.Reverse(priceBytes);
            Array.Reverse(sequenceBytes);

            string symbol = Encoding.ASCII.GetString(symbolBytes);
            char buySellIndicator = Encoding.ASCII.GetChars(buySellIndicatorBytes)[0];
            int quantity = BitConverter.ToInt32(quantityBytes, 0);
            int price = BitConverter.ToInt32(priceBytes, 0);
            int sequence = BitConverter.ToInt32(sequenceBytes, 0);

            Console.WriteLine("Packet received: " + sequence);

            return new Packet
            {
                Symbol = symbol,
                BuySellIndicator = buySellIndicator,
                Quantity = quantity,
                Price = price,
                Sequence = sequence
            };
        }

        /*static void PrintByteArray(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                Console.WriteLine(b);
            }
        }

        static byte[] ReverseByteArray(byte[] arr)
        {
            byte[] b = new byte[arr.Length];
            Array.Reverse(arr, 0, arr.Length);
            return b;
        }*/

        static void GenerateJsonOutput(List<Packet> packets)
        {
            // packets = packets.OrderBy(packet => packet.Symbol).ToList();
            packets.Sort((p1, p2) => p1.Sequence.CompareTo(p2.Sequence));
            List<object> packetObjects = new List<object>();
            foreach (Packet packet in packets)
            {
                packetObjects.Add(new
                {
                    Symbol = packet.Symbol,
                    BuySellIndicator = packet.BuySellIndicator,
                    Quantity = packet.Quantity,
                    Price = packet.Price,
                    Sequence = packet.Sequence
                });
            }

            string jsonOutput = JsonSerializer.Serialize(packetObjects, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText("output.json", jsonOutput);
        }
    }

    class Packet
    {
        public string Symbol { get; set; }
        public char BuySellIndicator { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int Sequence { get; set; }
    }
}
