using UDPChatApp;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Kiểm tra nếu có tham số truyền vào là "client"
        if (args.Length > 0 && args[0].ToLower() == "client")
        {
            Application.Run(new ClientForm());
        }
        else
        {
            // Mặc định nếu không có tham số (hoặc chạy từ VS) sẽ mở Server
            Application.Run(new ServerForm());
        }
    }
}