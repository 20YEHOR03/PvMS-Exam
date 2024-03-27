using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program {
        private const int PORT = 8888;
        private const int QUEUE_SIZE = 50;
        private const int BUFFER_SIZE = 1024;
        private const int GRID_SIZE = 10;
        private const int WINNING_LENGTH = 5;

        private static Queue<TcpClient> requestQueue = new Queue<TcpClient>();
        private static Dictionary<TcpClient, byte[]> playerBuffers = new Dictionary<TcpClient, byte[]>();
        private static char[,] gridValues = new char[GRID_SIZE, GRID_SIZE];
        private static char currentPlayer = 'X';

        static void Main(string[] args) {
            TcpListener server = null;
            try {
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                server = new TcpListener(localAddr, PORT);
                server.Start();
                Console.WriteLine("Server started...");

                // Start the request handler thread
                Thread requestHandlerThread = new Thread(RequestHandler);
                requestHandlerThread.Start();

                while (true) {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Client connected...");
                    lock (requestQueue) {
                        if (requestQueue.Count < QUEUE_SIZE) {
                            requestQueue.Enqueue(client);
                        } else {
                            // Rejecting connection due to full queue
                            client.Close();
                            Console.WriteLine("Connection rejected: Queue full");
                        }
                    }
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
            finally {
                server?.Stop();
            }
        }

        static void RequestHandler() {
            while (true) {
                TcpClient client;
                lock (requestQueue) {
                    if (requestQueue.Count > 0) {
                        client = requestQueue.Dequeue();
                    }
                    else {
                        Thread.Sleep(100); // Wait before checking the queue again
                        continue;
                    }
                }

                try {
                    // Initialize player buffer
                    byte[] buffer = new byte[BUFFER_SIZE];
                    playerBuffers.Add(client, buffer);

                    NetworkStream stream = client.GetStream();
                    StringBuilder sb = new StringBuilder();
                    while (true) {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // Client disconnected
                        string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        sb.Append(data);

                        if (data.Contains("\n")) {
                            string message = sb.ToString().Trim();
                            Console.WriteLine($"Received: {message}");
                            ProcessMessage(message, client);
                            sb.Clear();
                        }
                    }

                    // Broadcast updated grid state to all clients
                    byte[] gridData = Encoding.ASCII.GetBytes(GridStateToString());
                    foreach (var pair in playerBuffers) {
                        TcpClient targetClient = pair.Key;
                        NetworkStream targetStream = targetClient.GetStream();
                        targetStream.Write(gridData, 0, gridData.Length);
                    }
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
                finally {
                    lock (requestQueue) {
                        playerBuffers.Remove(client);
                    }
                    client.Close();
                }
            }
        }


        private static void ProcessMessage(string message, TcpClient client) {
            string[] parts = message.Split(',');
            int x = int.Parse(parts[0]);
            int y = int.Parse(parts[1]);
            char player = char.Parse(parts[2]);

            if (gridValues[x, y] != '-') {
                // Клітинка вже зайнята, скидаємо хід
                return;
            }

            gridValues[x, y] = player;
            currentPlayer = (player == 'X') ? 'O' : 'X';

            if (CheckForWin(x, y, player)) {
                // Send win message to client
                byte[] winMessage = Encoding.ASCII.GetBytes("WIN");
                NetworkStream stream = client.GetStream();
                stream.Write(winMessage, 0, winMessage.Length);
            }
        }


        static bool CheckForWin(int x, int y, char currentPlayer) {
            int count;

            // Check horizontally
            count = 1;
            for (int i = y - 1; i >= 0 && gridValues[x, i] == currentPlayer; i--) count++;
            for (int i = y + 1; i < GRID_SIZE && gridValues[x, i] == currentPlayer; i++) count++;
            if (count >= WINNING_LENGTH) return true;

            // Check vertically
            count = 1;
            for (int i = x - 1; i >= 0 && gridValues[i, y] == currentPlayer; i--) count++;
            for (int i = x + 1; i < GRID_SIZE && gridValues[i, y] == currentPlayer; i++) count++;
            if (count >= WINNING_LENGTH) return true;

            // Check diagonally (top-left to bottom-right)
            count = 1;
            for (int i = x - 1, j = y - 1; i >= 0 && j >= 0 && gridValues[i, j] == currentPlayer; i--, j--) count++;
            for (int i = x + 1, j = y + 1; i < GRID_SIZE && j < GRID_SIZE && gridValues[i, j] == currentPlayer; i++, j++) count++;
            if (count >= WINNING_LENGTH) return true;

            // Check diagonally (top-right to bottom-left)
            count = 1;
            for (int i = x - 1, j = y + 1; i >= 0 && j < GRID_SIZE && gridValues[i, j] == currentPlayer; i--, j++) count++;
            for (int i = x + 1, j = y - 1; i < GRID_SIZE && j >= 0 && gridValues[i, j] == currentPlayer; i++, j--) count++;
            if (count >= WINNING_LENGTH) return true;

            return false;
        }

        static string GridStateToString() {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < GRID_SIZE; i++) {
                for (int j = 0; j < GRID_SIZE; j++) {
                    sb.Append(gridValues[i, j]);
                    sb.Append(",");
                }
            }
            return sb.ToString();
        }
}

