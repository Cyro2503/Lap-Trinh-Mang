using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace UDPChatApp
{
    public class ServerForm : Form
    {
        // ===================== CONTROLS =====================
        private Panel panelHeader;
        private Label lblTitle;
        private Label lblStatus;

        // Panel trái: danh sách client
        private Panel panelClients;
        private Label lblClientsTitle;
        private ListBox lbClients;          // hiển thị tên từng client
        private Label lblSelectHint;

        // Panel phải: chat
        private Panel panelMain;
        private Panel panelConfig;
        private Label lblListenPort;
        private TextBox txtListenPort;
        private Button btnStartStop;
        private Label lblConnectionInfo;
        private Panel panelBody;
        private RichTextBox rtbChat;
        private Panel panelInput;
        private TextBox txtMessage;
        private Button btnSendPrivate;      // Gửi riêng cho client đang chọn
        private Button btnBroadcast;        // Gửi tất cả
        private Button btnClear;

        // ===================== UDP =====================
        private UdpClient? udpServer;
        private Thread? receiveThread;
        private bool isRunning = false;

        // key = "tên client" (unique), value = IPEndPoint để reply
        // Dùng tên làm key vì client gửi kèm tên trong payload
        private readonly Dictionary<string, IPEndPoint> clients = new();
        private readonly object clientsLock = new object();

        // ===================== COLORS =====================
        private readonly Color ColorPrimary = Color.FromArgb(15, 118, 110);   // teal-600
        private readonly Color ColorAccent = Color.FromArgb(20, 184, 166);   // teal-400
        private readonly Color ColorBg = Color.FromArgb(240, 253, 250);  // teal-50
        private readonly Color ColorSide = Color.FromArgb(19, 78, 74);     // teal-800
        private readonly Color ColorSend = Color.FromArgb(8, 80, 65);
        private readonly Color ColorReceive = Color.FromArgb(2, 52, 44);
        private readonly Color ColorPrivate = Color.FromArgb(120, 53, 15);    // amber-900
        private readonly Color ColorBroadcast = Color.FromArgb(5, 150, 105);    // emerald-600

        public ServerForm()
        {
            InitializeComponents();
            this.Text = "UDP Chat — SERVER";
            this.Size = new Size(780, 700);
            this.MinimumSize = new Size(640, 560);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(60, 80);
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
                Text = "🖥  UDP CHAT SERVER",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblStatus = new Label
            {
                Text = "● Chưa khởi động",
                ForeColor = Color.FromArgb(255, 200, 200),
                Font = new Font("Segoe UI", 9f),
                AutoSize = false,
                Width = 180,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight
            };
            panelHeader.Controls.Add(lblTitle);
            panelHeader.Controls.Add(lblStatus);

            // ===================== PANEL TRÁI — DANH SÁCH CLIENT =====================
            panelClients = new Panel
            {
                Dock = DockStyle.Left,
                Width = 160,
                BackColor = ColorSide,
                Padding = new Padding(8, 10, 8, 8)
            };

            lblClientsTitle = new Label
            {
                Text = "CLIENTS",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = ColorAccent,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Hint bên dưới ListBox
            lblSelectHint = new Label
            {
                Text = "Chọn Client để chat riêng\nBỏ chọn = chat tổng",
                Dock = DockStyle.Bottom,
                Height = 40,
                ForeColor = Color.FromArgb(153, 246, 228),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.TopLeft
            };

            lbClients = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(17, 94, 89),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                SelectionMode = SelectionMode.One,
                ItemHeight = 32,
                DrawMode = DrawMode.OwnerDrawFixed
            };

            // Tự vẽ từng item — item đang chọn nền xanh sáng, còn lại nền tối
            lbClients.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                string itemText = lbClients.Items[e.Index].ToString() ?? "";
                bool isSelected = e.Index == lbClients.SelectedIndex;

                // Nền
                Color bgColor = isSelected
                    ? Color.FromArgb(20, 184, 166)   // teal-400 — item đang chọn
                    : Color.FromArgb(17, 94, 89);     // teal-700 — bình thường
                e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

                // Thanh dọc bên trái khi đang chọn (giống tab active)
                if (isSelected)
                    e.Graphics.FillRectangle(
                        new SolidBrush(Color.White),
                        new Rectangle(e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height));

                // Icon + tên
                string display = isSelected ? $"  ● {itemText}" : $"  ○ {itemText}";
                Color textColor = isSelected ? Color.White : Color.FromArgb(153, 246, 228);
                var font = new Font("Segoe UI", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular);
                e.Graphics.DrawString(display, font, new SolidBrush(textColor),
                    e.Bounds.X + 6, e.Bounds.Y + (e.Bounds.Height - font.Height) / 2);
            };

            lbClients.SelectedIndexChanged += (s, e) =>
            {
                UpdateSendButtons();
                lbClients.Invalidate();
            };

            // Nút Bỏ chọn — cách duy nhất để bỏ chọn, rõ ràng không nhầm lẫn
            var btnDeselect = new Button
            {
                Text = "✕ Bỏ chọn",
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(13, 74, 72),
                ForeColor = Color.FromArgb(153, 246, 228),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btnDeselect.FlatAppearance.BorderSize = 0;
            btnDeselect.Click += (s, e) =>
            {
                lbClients.SelectedIndex = -1;
                lbClients.Invalidate();
                UpdateSendButtons();
            };

            panelClients.Controls.Add(lbClients);
            panelClients.Controls.Add(btnDeselect);
            panelClients.Controls.Add(lblSelectHint);
            panelClients.Controls.Add(lblClientsTitle);

            // ===================== PANEL PHẢI — CHAT =====================
            panelMain = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg };

            // ---- Config ----
            panelConfig = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
            };
            panelConfig.Paint += PanelBottomBorder;

            lblListenPort = MakeLabel("Cổng lắng nghe:", 0, 14);
            txtListenPort = MakeTextBox("9000", 115, 11, 60);

            lblConnectionInfo = new Label
            {
                Text = "Chưa có client nào kết nối",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Left = 0,
                Top = 46,
                Width = 380,
                Height = 20
            };

            btnStartStop = new Button
            {
                Text = "▶  Khởi động",
                Left = 420,
                Top = 8,
                Width = 120,
                Height = 30,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Click += BtnStartStop_Click;

            panelConfig.Controls.AddRange(new Control[]
            {
                lblListenPort, txtListenPort,
                lblConnectionInfo, btnStartStop
            });

            // ---- Chat area ----
            panelBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 0),
                BackColor = ColorBg
            };
            rtbChat = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            panelBody.Controls.Add(rtbChat);

            // ---- Input ----
            panelInput = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
            };
            panelInput.Paint += PanelTopBorder;

            txtMessage = new TextBox
            {
                PlaceholderText = "Nhập tin nhắn...",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
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

            // Nút Chat riêng — chỉ gửi cho client đang chọn trong ListBox
            btnSendPrivate = new Button
            {
                Text = "✉ Gửi riêng",
                Dock = DockStyle.Right,
                Width = 105,
                BackColor = Color.FromArgb(180, 83, 9), // amber-700
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false   // chỉ bật khi có client được chọn
            };
            btnSendPrivate.FlatAppearance.BorderSize = 0;
            btnSendPrivate.Click += (s, e) => SendMessage(broadcast: false);

            // Nút Chat tổng — gửi tất cả client
            btnBroadcast = new Button
            {
                Text = "📢 Chat tổng",
                Dock = DockStyle.Right,
                Width = 115,
                BackColor = ColorBroadcast,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnBroadcast.FlatAppearance.BorderSize = 0;
            btnBroadcast.Click += (s, e) =>
            {
                lbClients.SelectedIndex = -1;
                lbClients.Invalidate();
                UpdateSendButtons();
                SendMessage(broadcast: true);
            };

            panelInput.Controls.Add(txtMessage);
            panelInput.Controls.Add(btnSendPrivate);
            panelInput.Controls.Add(btnBroadcast);
            panelInput.Controls.Add(btnClear);

            // ---- Gắn vào panelMain ----
            panelMain.Controls.Add(panelBody);
            panelMain.Controls.Add(panelInput);
            panelMain.Controls.Add(panelConfig);

            // ===================== ASSEMBLE FORM =====================
            this.Controls.Add(panelMain);
            this.Controls.Add(panelClients);
            this.Controls.Add(panelHeader);
            this.FormClosing += ServerForm_FormClosing;
        }

        // Cập nhật trạng thái nút Gửi riêng theo lựa chọn ListBox
        private void UpdateSendButtons()
        {
            bool selected = lbClients.SelectedIndex >= 0;
            btnSendPrivate.Enabled = selected;
            btnSendPrivate.BackColor = selected
                ? Color.FromArgb(180, 83, 9)
                : Color.FromArgb(120, 120, 120);
            txtMessage.PlaceholderText = selected
                ? $"Nhập tin nhắn riêng cho [{lbClients.SelectedItem}]..."
                : "Nhập tin nhắn... (chọn client để gửi riêng, hoặc Chat tổng)";
        }

        // ===================== START / STOP =====================
        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (!isRunning) StartServer(); else StopServer();
        }

        private void StartServer()
        {
            if (!int.TryParse(txtListenPort.Text.Trim(), out int listenPort)
                || listenPort < 1 || listenPort > 65535)
            {
                MessageBox.Show("Cổng lắng nghe không hợp lệ!", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                udpServer = new UdpClient(listenPort);
                isRunning = true;

                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "ServerReceive"
                };
                receiveThread.Start();

                UpdateStatus(true, $"● Lắng nghe :{listenPort}");
                btnStartStop.Text = "■  Dừng";
                btnStartStop.BackColor = Color.FromArgb(185, 28, 28);
                txtListenPort.Enabled = false;
                AppendChat($"[{Now}] ✅ Server khởi động, lắng nghe cổng {listenPort}",
                           ColorPrimary, bold: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể khởi động server:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            isRunning = false;
            try { udpServer?.Close(); } catch { }
            udpServer = null;

            lock (clientsLock) { clients.Clear(); }

            this.Invoke((Action)(() =>
            {
                lbClients.Items.Clear();
                UpdateSendButtons();
                lblConnectionInfo.Text = "Chưa có client nào kết nối";
                lblConnectionInfo.ForeColor = Color.Gray;
            }));

            UpdateStatus(false, "● Đã dừng");
            btnStartStop.Text = "▶  Khởi động";
            btnStartStop.BackColor = ColorPrimary;
            txtListenPort.Enabled = true;
            AppendChat($"[{Now}] 🔴 Server đã dừng", Color.Gray, bold: false);
        }

        // ===================== RECEIVE LOOP =====================
        private void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpServer!.Receive(ref remoteEP);
                    string raw = Encoding.UTF8.GetString(data);

                    // Giao thức: "listenPort|tên|nội dung"
                    string senderName, message;
                    IPEndPoint replyEP;

                    var parts = raw.Split('|', 3);
                    if (parts.Length == 3 && int.TryParse(parts[0], out int clientListenPort))
                    {
                        senderName = parts[1].Trim();
                        message = parts[2];
                        // Reply về IP nhận được, nhưng cổng là listenPort client khai báo
                        replyEP = new IPEndPoint(remoteEP.Address, clientListenPort);
                    }
                    else
                    {
                        // Fallback client cũ
                        senderName = "CLIENT";
                        message = raw;
                        replyEP = remoteEP;
                    }

                    // Lưu/cập nhật endpoint, đồng thời cập nhật ListBox
                    bool isNew;
                    lock (clientsLock)
                    {
                        isNew = !clients.ContainsKey(senderName);
                        clients[senderName] = replyEP;
                    }

                    int clientCount = clients.Count;

                    this.Invoke((Action)(() =>
                    {
                        // Thêm vào ListBox nếu client mới
                        if (isNew && !lbClients.Items.Contains(senderName))
                            lbClients.Items.Add(senderName);

                        lblConnectionInfo.Text =
                            $"{clientCount} client đang kết nối";
                        lblConnectionInfo.ForeColor = ColorPrimary;

                        AppendChat(
                            $"[{Now}] 📩 {senderName} ({replyEP.Address}:{replyEP.Port}): {message}",
                            ColorReceive, bold: false, prefix: senderName);
                    }));
                }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (isRunning)
                        this.Invoke((Action)(() =>
                            AppendChat($"[{Now}] ⚠ Lỗi nhận: {ex.Message}",
                                       Color.OrangeRed, bold: false)));
                }
            }
        }

        // ===================== SEND =====================
        private void TxtMessage_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                // Enter = gửi riêng nếu đang chọn, ngược lại broadcast
                SendMessage(broadcast: lbClients.SelectedIndex < 0);
            }
        }

        private void SendMessage(bool broadcast)
        {
            if (!isRunning)
            {
                MessageBox.Show("Server chưa được khởi động!\nNhấn 'Khởi động' trước.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string msg = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            if (broadcast)
            {
                // ---- CHAT TỔNG ----
                List<KeyValuePair<string, IPEndPoint>> targets;
                lock (clientsLock)
                    targets = new List<KeyValuePair<string, IPEndPoint>>(clients);

                if (targets.Count == 0)
                {
                    MessageBox.Show("Chưa có client nào kết nối!\nClient phải gửi tin trước.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                byte[] data = Encoding.UTF8.GetBytes(msg);
                int sent = 0, failed = 0;
                foreach (var kv in targets)
                {
                    try { using var s = new UdpClient(); s.Send(data, data.Length, kv.Value); sent++; }
                    catch { failed++; }
                }

                string summary = failed > 0
                    ? $"({sent} thành công, {failed} thất bại)"
                    : $"→ {sent} client";
                AppendChat($"[{Now}] 📢 Broadcast {summary}: {msg}",
                           ColorBroadcast, bold: false, prefix: "SERVER");
            }
            else
            {
                // ---- CHAT RIÊNG ----
                string? selectedName = lbClients.SelectedItem?.ToString();
                if (selectedName == null) return;

                IPEndPoint? ep;
                lock (clientsLock)
                    clients.TryGetValue(selectedName, out ep);

                if (ep == null)
                {
                    MessageBox.Show($"Không tìm thấy endpoint của [{selectedName}]!", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    using var s = new UdpClient();
                    s.Send(data, data.Length, ep);

                    AppendChat($"[{Now}] ✉ Riêng → [{selectedName}] ({ep.Address}:{ep.Port}): {msg}",
                               ColorPrivate, bold: false, prefix: "SERVER→" + selectedName);
                }
                catch (Exception ex)
                {
                    AppendChat($"[{Now}] ❌ Gửi thất bại: {ex.Message}", Color.Red, bold: false);
                }
            }

            txtMessage.Clear();
            txtMessage.Focus();
        }

        // ===================== HELPERS =====================
        private void AppendChat(string text, Color color, bool bold, string prefix = "")
        {
            if (rtbChat.InvokeRequired)
            {
                rtbChat.Invoke((Action)(() => AppendChat(text, color, bold, prefix)));
                return;
            }
            if (!string.IsNullOrEmpty(prefix))
            {
                rtbChat.SelectionStart = rtbChat.TextLength;
                rtbChat.SelectionLength = 0;
                // SERVER→TênClient = chat riêng (màu cam)
                // SERVER = broadcast (màu teal)
                // tên client = tin nhận vào (màu vàng nâu)
                rtbChat.SelectionColor =
                    prefix.StartsWith("SERVER→") ? Color.FromArgb(217, 119, 6) :
                    prefix == "SERVER" ? ColorAccent :
                                                   Color.FromArgb(180, 100, 0);
                rtbChat.SelectionFont = new Font("Segoe UI", 8f, FontStyle.Bold);
                rtbChat.AppendText($"[{prefix}] ");
            }
            rtbChat.SelectionStart = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;
            rtbChat.SelectionColor = color;
            rtbChat.SelectionFont = new Font("Segoe UI", 10f,
                bold ? FontStyle.Bold : FontStyle.Regular);
            rtbChat.AppendText(text + "\n");
            rtbChat.ScrollToCaret();
        }

        private void UpdateStatus(bool active, string text)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke((Action)(() => UpdateStatus(active, text)));
                return;
            }
            lblStatus.Text = text;
            lblStatus.ForeColor = active
                ? Color.FromArgb(167, 243, 208) : Color.FromArgb(255, 200, 200);
        }

        private static string Now => DateTime.Now.ToString("HH:mm:ss");

        private Label MakeLabel(string text, int left, int top) => new Label
        {
            Text = text,
            Left = left,
            Top = top,
            AutoSize = true,
            ForeColor = Color.FromArgb(55, 65, 81),
            Font = new Font("Segoe UI", 8.8f)
        };

        private TextBox MakeTextBox(string text, int left, int top, int width) => new TextBox
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 24,
            Font = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle
        };

        private void PanelBottomBorder(object? sender, PaintEventArgs e)
        {
            var p = sender as Panel;
            using var pen = new Pen(Color.FromArgb(220, 220, 220), 1);
            e.Graphics.DrawLine(pen, 0, p!.Height - 1, p.Width, p.Height - 1);
        }

        private void PanelTopBorder(object? sender, PaintEventArgs e)
        {
            var p = sender as Panel;
            using var pen = new Pen(Color.FromArgb(220, 220, 220), 1);
            e.Graphics.DrawLine(pen, 0, 0, p!.Width, 0);
        }

        private void ServerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            isRunning = false;
            try { udpServer?.Close(); } catch { }
        }
    }
}