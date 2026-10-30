using uPLibrary.Networking.M2Mqtt;

internal class Program {
    [STAThread]
    static void Main(string[] args) {
        Program p = new Program();
        p.Startup();
    }

    public void Startup() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string connectionString = "localhost";
        MqttClient mqttClient  = new MqttClient(connectionString);
        Application.Run(new StatusForm(mqttClient));
    }
}
