
using System.Globalization;
using System.Text;
using PowerSupplyLib;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace PsuService;

internal static class Program {
    static string connectionString = "localhost";
    private static IPowerSupply powerSupply;
    private static MqttClient mqttClient;
    
    private static double currentVoltage = -1;
    private static readonly object psuLock = new();
    
    
    static void Main(string[] args) {
        Console.WriteLine("Hello, World!");

        powerSupply = PowerSupplyFactory.CreatePowerSupply(1);
        mqttClient = new MqttClient(connectionString);
        mqttClient.Connect("PowerSupply");

        {
            double maxVoltage = powerSupply.GetMaxVoltage();
            string topic = "/psu/get/max_voltage";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(maxVoltage.ToString(CultureInfo.InvariantCulture)), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
        } {
            string serialNumber = powerSupply.GetSerialNumber();
            string topic = "/psu/get/serial_number";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(serialNumber), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
        } {
            string articleNumber = powerSupply.GetArticleNumber();
            string topic = "/psu/get/article_number";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(articleNumber), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
        } {
            string deviceType = powerSupply.GetDeviceType();
            string topic = "/psu/get/device_type";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(deviceType), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
        }
        
        mqttClient.MqttMsgPublishReceived += (sender, e) => {
            string topic = e.Topic;
            string message = Encoding.UTF8.GetString(e.Message);
            try {
                switch (topic) {
                    case "/psu/set/voltage":
                        if (!double.TryParse(message, NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage)) {
                            Console.WriteLine($"Invalid voltage: '{message}'");
                            break;
                        }
                        lock (psuLock) { // thread safe
                            powerSupply.SetVoltage(voltage);
                        }
                        break;
                    case "/psu/set/locked":
                        if (!bool.TryParse(message, out bool isLocked)) {
                            Console.WriteLine($"Invalid locked value: '{message}'");
                            break;
                        }
                        lock (psuLock) { // thread safe
                            powerSupply.SetLocked(isLocked);
                        }
                        break;
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed to handle '{topic}': {ex.Message}");
            }
        };
        mqttClient.Subscribe(
            ["/psu/set/voltage", "/psu/set/locked"],
            [MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE]);

        while (true) {
            try {
                double newCurrentVoltage;
                lock (psuLock) { // thread safe
                    newCurrentVoltage = powerSupply.GetVoltage();
                }
                if (newCurrentVoltage != currentVoltage) {
                    string topic = "/psu/get/voltage";
                    mqttClient.Publish(topic, Encoding.UTF8.GetBytes(newCurrentVoltage.ToString(CultureInfo.InvariantCulture)), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                    currentVoltage = newCurrentVoltage;
                }
            } catch (Exception ex) {
                Console.WriteLine($"Failed to poll voltage: {ex.Message}");
            }
            Thread.Sleep(200); // give thread to others, we dont need to spam the psu
        }
    }
}

