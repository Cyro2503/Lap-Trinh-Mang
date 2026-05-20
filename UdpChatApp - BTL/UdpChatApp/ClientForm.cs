using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace UDPChatApp
{
    public class ClientForm : Form
    {
        // ===================== CONTROLS =====================
        private Panel panelHeader;
        private Label lblTitle;
        private Label lblStatus;
        private Panel panelMain;
        private Panel panelConfig;
        private Label lblClientName;
        private TextBox txtClientName;
        private Label lblServerIP;
        private TextBox txtServerIP;
        private Label lblServerPort;
        private TextBox txtServerPort;
        private Button btnStartStop;
        private Panel panelBody;
        private RichTextBox rtbChat;
        private Panel panelInput;
        private TextBox txtMessage;
        private Button btnSend;
        private Button btnClear;

        // ===================== UDP =====================
        private UdpClient? udpReceiver;
        private Thread? receiveThread;
        private bool isRunning = false;
        private int listenPort;
        private string clientName;

        // ===================== COLORS =====================
        private readonly Color ColorPrimary = Color.FromArgb(67, 56, 202);   // indigo-700
        private readonly Color ColorAccent = Color.FromArgb(129, 140, 248); // indigo-400
        private readonly Color ColorBg = Color.FromArgb(238, 242, 255);     // indigo-50
        private readonly Color ColorSend = Color.FromArgb(49, 46, 129);
        private readonly Color ColorReceive = Color.FromArgb(30, 64, 175);

        public ClientForm()
        {
            // Tự động tạo một số định danh ngẫu nhiên để tránh trùng cổng nếu mở nhiều exe
            Random rnd = new Random();
            int suffix = rnd.Next(1, 999);
            clientName = $"Client_{suffix}";
            listenPort = 9000 + suffix;

            InitializeComponents();

            this.Text = $"UDP Chat — {clientName}";
            this.Size = new Size(580, 650);
            this.MinimumSize = new Size(500, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorBg;
            this.Font = new Font("Segoe UI", 9.5f);
        }

        private void InitializeComponents()
        {
            // ===================== HEADER =====================
            panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorPrimary,
                Padding = new Padding(16, 0, 16, 0)
            };
            lblTitle = new Label
            {
                Text = "💬 UDP CHAT CLIENT",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblStatus = new Label
            {
                Text = "● Ngoại tuyến",
                ForeColor = Color.FromArgb(200, 200, 255),
                Font = new Font("Segoe UI", 9f),
                AutoSize = false,
                Width = 160,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight
            };
            panelHeader.Controls.Add(lblTitle);
            panelHeader.Controls.Add(lblStatus);

            // ===================== MAIN PANEL =====================
            panelMain = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg };

            // ---- Config Panel ----
            panelConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.White,
                Padding = new Padding(15)
            };
            panelConfig.Paint += PanelBottomBorder;

            lblClientName = MakeLabel("Tên bạn:", 15, 15);
            txtClientName = MakeTextBox(clientName, 85, 12, 140);
            txtClientName.TextChanged += (s, e) => {
                clientName = string.IsNullOrWhiteSpace(txtClientName.Text) ? "Client" : txtClientName.Text.Trim();
                this.Text = $"UDP Chat — {clientName}";
            };

            lblServerIP = MakeLabel("IP Server:", 15, 48);
            txtServerIP = MakeTextBox("127.0.0.1", 85, 45, 110);

            lblServerPort = MakeLabel("Cổng:", 205, 48);
            txtServerPort = MakeTextBox("9000", 255, 45, 60);

            btnStartStop = new Button
            {
                Text = "▶ Kết nối",
                Left = 330,
                Top = 12,
                Width = 130,
                Height = 58,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;

            panelConfig.Controls.AddRange(new Control[] {
                lblClientName, txtClientName, lblServerIP,
                txtServerIP, lblServerPort, txtServerPort, btnStartStop
            });

            // ---- Chat Area ----
            panelBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 15, 0),
                BackColor = ColorBg
            };
            rtbChat = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            panelBody.Controls.Add(rtbChat);

            // ---- Input Panel ----
            panelInput = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(15, 12, 15, 12)
            };
            panelInput.Paint += PanelTopBorder;

            txtMessage = new TextBox
            {
                PlaceholderText = "Nhập tin nhắn...",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtMessage.KeyDown += TxtMessage_KeyDown;

            btnClear = new Button
            {
                Text = "Xóa",
                Dock = DockStyle.Right,
                Width = 60,
                BackColor = Color.FromArgb(245, 245, 245),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderColor = Color.LightGray;
            btnClear.Click += (s, e) => rtbChat.Clear();

            btnSend = new Button
            {
                Text = "Gửi ➤",
                Dock = DockStyle.Right,
                Width = 90,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += BtnSend_Click;

            panelInput.Controls.Add(txtMessage);
            panelInput.Controls.Add(btnSend);
            panelInput.Controls.Add(btnClear);

            // ---- Assemble ----
            panelMain.Controls.Add(panelBody);
            panelMain.Controls.Add(panelInput);
            panelMain.Controls.Add(panelConfig);

            this.Controls.Add(panelMain);
            this.Controls.Add(panelHeader);
            this.FormClosing += ClientForm_FormClosing;
        }

        // ===================== LOGIC KẾT NỐI =====================
        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (!isRunning) StartClient(); else StopClient();
        }

        private void StartClient()
        {
            if (!int.TryParse(txtServerPort.Text.Trim(), out int serverPort)) return;

            try
            {
                // Thử bind vào cổng lắng nghe (nếu trùng cổng do mở nhiều exe, nó sẽ tự tăng)
                bool bound = false;
                while (!bound)
                {
                    try
                    {
                        udpReceiver = new UdpClient(listenPort);
                        bound = true;
                    }
                    catch
                    {
                        listenPort++;
                    }
                }

                isRunning = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();

                UpdateStatus(true, "● Đang hoạt động");
                btnStartStop.Text = "■ Ngắt kết nối";
                btnStartStop.BackColor = Color.FromArgb(185, 28, 28);
                txtClientName.Enabled = false;

                AppendChat($"[{Now}] ✅ Sẵn sàng tại cổng {listenPort}", ColorPrimary, true);

                // 🔥 ĐOẠN ĐƯỢC THÊM: Gửi gói tin báo hiệu kết nối lập tức tới Server
                SendSystemSignal("Đã kết nôi", serverPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khởi động client: {ex.Message}");
            }
        }

        private void StopClient()
        {
            if (isRunning)
            {
                // 🔥 ĐOẠN ĐƯỢC THÊM: Gửi tín hiệu báo hủy kết nối tới Server trước khi tắt socket hoàn toàn
                if (int.TryParse(txtServerPort.Text.Trim(), out int serverPort))
                {
                    SendSystemSignal("__DISCONNECT__", serverPort);
                }
            }

            isRunning = false;
            udpReceiver?.Close();
            udpReceiver = null;

            UpdateStatus(false, "● Ngoại tuyến");
            btnStartStop.Text = "▶ Kết nối";
            btnStartStop.BackColor = ColorPrimary;
            txtClientName.Enabled = true;
            AppendChat($"[{Now}] 🔴 Đã ngắt kết nối", Color.Gray, false);
        }

        /// <summary>
        /// Hàm phụ trợ dùng để gửi gói tin hệ thống (Kết nối / Ngắt kết nối) lên Server
        /// </summary>
        private void SendSystemSignal(string signalType, int serverPort)
        {
            try
            {
                // Tuân thủ đúng cấu trúc định dạng cấu trúc dữ liệu của bạn: Cổng|Tên|Nội dung
                string payload = $"{listenPort}|{clientName}|{signalType}";
                byte[] data = Encoding.UTF8.GetBytes(payload);

                using var sender = new UdpClient();
                sender.Send(data, data.Length, txtServerIP.Text.Trim(), serverPort);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi tín hiệu hệ thống: {ex.Message}");
            }
        }

        private void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpReceiver!.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);

                    this.Invoke((Action)(() => {
                        AppendChat($"[{Now}] 📩 Server: {message}", ColorReceive, false, "SERVER");
                    }));
                }
                catch { break; }
            }
        }

        private void SendMessage()
        {
            if (!isRunning || string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            try
            {
                string payload = $"{listenPort}|{clientName}|{txtMessage.Text.Trim()}";
                byte[] data = Encoding.UTF8.GetBytes(payload);

                using var sender = new UdpClient();
                sender.Send(data, data.Length, txtServerIP.Text.Trim(), int.Parse(txtServerPort.Text.Trim()));

                AppendChat($"[{Now}] 📤 Bạn: {txtMessage.Text.Trim()}", ColorSend, false, clientName);
                txtMessage.Clear();
                txtMessage.Focus();
            }
            catch (Exception ex) { AppendChat($"[!] Lỗi gửi: {ex.Message}", Color.Red, false); }
        }

        // ===================== UI HELPERS =====================
        private void BtnSend_Click(object? sender, EventArgs e) => SendMessage();
        private void TxtMessage_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendMessage(); }
        }

        private void AppendChat(string text, Color color, bool bold, string prefix = "")
        {
            if (rtbChat.InvokeRequired) { rtbChat.Invoke((Action)(() => AppendChat(text, color, bold, prefix))); return; }

            if (!string.IsNullOrEmpty(prefix))
            {
                rtbChat.SelectionStart = rtbChat.TextLength;
                rtbChat.SelectionColor = prefix == "SERVER" ? Color.DarkOrange : ColorAccent;
                rtbChat.SelectionFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                rtbChat.AppendText($"<{prefix}> ");
            }

            rtbChat.SelectionStart = rtbChat.TextLength;
            rtbChat.SelectionColor = color;
            rtbChat.SelectionFont = new Font("Segoe UI", 10f, bold ? FontStyle.Bold : FontStyle.Regular);
            rtbChat.AppendText(text + "\n");
            rtbChat.ScrollToCaret();
        }

        private void UpdateStatus(bool active, string text)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = active ? Color.SpringGreen : Color.FromArgb(200, 200, 255);
        }

        private static string Now => DateTime.Now.ToString("HH:mm:ss");

        private Label MakeLabel(string text, int left, int top) => new Label
        {
            Text = text,
            Left = left,
            Top = top,
            AutoSize = true,
            ForeColor = Color.FromArgb(75, 85, 99),
            Font = new Font("Segoe UI", 9f)
        };

        private TextBox MakeTextBox(string text, int left, int top, int width) => new TextBox
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Font = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle
        };

        private void PanelBottomBorder(object? sender, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(230, 230, 230), 1);
            e.Graphics.DrawLine(pen, 0, (sender as Panel)!.Height - 1, (sender as Panel)!.Width, (sender as Panel)!.Height - 1);
        }

        private void PanelTopBorder(object? sender, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(230, 230, 230), 1);
            e.Graphics.DrawLine(pen, 0, 0, (sender as Panel)!.Width, 0);
        }

        private void ClientForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            isRunning = false;
            udpReceiver?.Close();
        }
    }
}