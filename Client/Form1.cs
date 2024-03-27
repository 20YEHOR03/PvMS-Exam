using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client {
    public partial class Form1 : Form {
        private const int GRID_SIZE = 10;
        private const int CELL_SIZE = 50;
        private const string SERVER_IP = "127.0.0.1";
        private const int SERVER_PORT = 8888;
        private TcpClient client;
        private NetworkStream stream;
        private byte[] buffer;

        private Button[,] gridButtons;
        private char[,] gridValues;
        private char currentPlayer = 'X';
        private bool waitingForResponse = false; // Flag to track whether waiting for response from server

        public Form1() {
            InitializeComponent();
            InitializeGrid();

            // Connect to the server
            client = new TcpClient(SERVER_IP, SERVER_PORT);
            stream = client.GetStream();
            buffer = new byte[1024];
            Task.Run(() => ReceiveData());
        }

        private void ReceiveData() {
            int totalBytesRead = 0;
            while (true) {
                int bytesRead = stream.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                totalBytesRead += bytesRead;

                if (bytesRead == 0) {
                    // Кінець потока, обробка повідомлення
                    string receivedMessage = Encoding.ASCII.GetString(buffer, 0, totalBytesRead);
                    ProcessMessage(receivedMessage);
            
                    // Очищення буфера та скидання лічильника зчитаних байтів
                    Array.Clear(buffer, 0, buffer.Length);
                    totalBytesRead = 0;
                }
            }
        }

        private void ProcessMessage(string message) {
            if (message.StartsWith("WIN")) {
                MessageBox.Show($"{message[3]} wins!");
                ResetGame();
            }
            else {
                string[] parts = message.Split(',');
                for (int i = 0; i < GRID_SIZE; i++) {
                    for (int j = 0; j < GRID_SIZE; j++) {
                        gridValues[i, j] = parts[i * GRID_SIZE + j][0];
                        gridButtons[i, j].Text = gridValues[i, j].ToString();
                    }
                }
                currentPlayer = (currentPlayer == 'X') ? 'O' : 'X';
            }
        }
        
        private string ReadMessageFromStream(MemoryStream stream) {
            const int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];

            StringBuilder messageBuilder = new StringBuilder();
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                messageBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                if (stream.Position >= stream.Length) {
                    break;
                }
            }

            string message = messageBuilder.ToString();
            if (string.IsNullOrEmpty(message))
                return null;

            return message;
        }

        private void InitializeGrid() {
            gridButtons = new Button[GRID_SIZE, GRID_SIZE];
            gridValues = new char[GRID_SIZE, GRID_SIZE];

            for (int i = 0; i < GRID_SIZE; i++) {
                for (int j = 0; j < GRID_SIZE; j++) {
                    var button = new Button {
                        Size = new System.Drawing.Size(CELL_SIZE, CELL_SIZE),
                        Location = new System.Drawing.Point(j * CELL_SIZE, i * CELL_SIZE),
                        Font = new System.Drawing.Font("Arial", 20),
                        Tag = new System.Drawing.Point(i, j)
                    };
                    button.Click += GridButtonClick;
                    Controls.Add(button);
                    gridButtons[i, j] = button;
                    gridValues[i, j] = '-';
                }
            }
        }



        private void GridButtonClick(object sender, EventArgs e) {
            if (waitingForResponse) 
                return; // Не дозволяємо нові введення, поки очікуємо відповіді від сервера

            var button = (Button)sender;
            var point = (System.Drawing.Point)button.Tag;
            int x = point.X;
            int y = point.Y;

            if (gridValues[x, y] == '-') {
                gridValues[x, y] = currentPlayer;
                button.Text = currentPlayer.ToString();

                string message = $"{x},{y},{currentPlayer}";
                SendMessage(message);
                waitingForResponse = true; // Встановлюємо прапорець, що очікуємо відповіді
            }
        }

        
        private void SendMessage(string message) {
            byte[] data = Encoding.ASCII.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }

        private void ResetGame() {
            foreach (Button button in gridButtons) {
                button.Text = "";
            }
            currentPlayer = 'X';
            gridValues = new char[GRID_SIZE, GRID_SIZE];
            for (int i = 0; i < GRID_SIZE; i++) {
                for (int j = 0; j < GRID_SIZE; j++) {
                    gridValues[i, j] = '-';
                }
            }
        }
    }
}
