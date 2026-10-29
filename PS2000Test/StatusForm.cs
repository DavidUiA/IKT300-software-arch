using System.Globalization;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

// Window used to interact with the powerSupply
internal class StatusForm : Form {
    private readonly MqttClient mqttClient;

    private readonly Label deviceTypeLabel = new() { AutoSize = true };
    private readonly Label serialNumberLabel = new() { AutoSize = true };
    private readonly Label maxVoltageLabel = new() { AutoSize = true };
    private readonly Label currentVoltageLabel = new() { AutoSize = true };
    private readonly Label articleNumberLabel = new() { AutoSize = true };

    private readonly TextBox voltageInput = new() { Width = 100 };
    private readonly Button setVoltageButton = new() { Text = "Set voltage", AutoSize = true, Enabled = false };
    private readonly CheckBox remoteControlCheckBox = new() { Text = "Remote control", AutoSize = true };

    public StatusForm(MqttClient mqttClient) {
        this.mqttClient = mqttClient;
        mqttClient.Connect("PsuUi_" + Guid.NewGuid());

        Text = "Powersupply status";
        Width = 320;
        Height = 320;

        Label[] labels = { deviceTypeLabel, serialNumberLabel, maxVoltageLabel, currentVoltageLabel, articleNumberLabel };
        int y = 10;
        foreach (Label label in labels) {
            label.Location = new Point(10, y);
            Controls.Add(label);
            y += 30;
        }

        voltageInput.Location = new Point(10, y);
        Controls.Add(voltageInput);

        setVoltageButton.Location = new Point(120, y);
        setVoltageButton.Click += (_, _) => SetVoltageFromInput();
        Controls.Add(setVoltageButton);
        y += 30;

        // Lock/unlock
        remoteControlCheckBox.Location = new Point(10, y);
        remoteControlCheckBox.CheckedChanged += (_, _) => {
            string topic = "/psu/set/locked";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes((!remoteControlCheckBox.Checked).ToString()), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            setVoltageButton.Enabled = remoteControlCheckBox.Checked;
        };
        Controls.Add(remoteControlCheckBox);

        // Subscribe in Load, so the window handle exists before BeginInvoke is called
        Load += (_, _) => {
            mqttClient.MqttMsgPublishReceived += (sender, e) => {
                string topic = e.Topic;
                string message = Encoding.UTF8.GetString(e.Message);
                BeginInvoke(() => onMqttReceive(topic, message));
            };

            mqttClient.Subscribe(
                ["/psu/get/max_voltage", "/psu/get/serial_number", "/psu/get/article_number", "/psu/get/device_type", "/psu/get/voltage"],
                [MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE]);
        };

        FormClosing += (_, _) => {
            if (mqttClient.IsConnected) {
                mqttClient.Disconnect();
            }
        };
    }

    private void onMqttReceive(string topic, string message) {
        try {
            switch (topic) {
                case "/psu/get/voltage":
                    if (!double.TryParse(message, NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage)) {
                        Console.WriteLine($"Invalid voltage: '{message}'");
                        break;
                    }
                    currentVoltageLabel.Text = $"Current voltage: {voltage}";
                    // Don't overwrite what the user is typing
                    if (!voltageInput.Focused) {
                        voltageInput.Text = message;
                    }
                    break;
                case "/psu/get/max_voltage":
                    maxVoltageLabel.Text = $"Max voltage: {message}";
                    break;
                case "/psu/get/serial_number":
                    serialNumberLabel.Text = $"Serial number: {message}";
                    break;
                case "/psu/get/article_number":
                    articleNumberLabel.Text = $"Article number: {message}";
                    break;
                case "/psu/get/device_type":
                    deviceTypeLabel.Text = $"Device type: {message}";
                    break;
            }
        } catch (Exception ex) {
            Console.WriteLine($"Failed to handle '{topic}': {ex.Message}");
        }
    }

    private void SetVoltageFromInput() {
        if (float.TryParse(voltageInput.Text, out float setVolt)) {
            string topic = "/psu/set/voltage";
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(setVolt.ToString(CultureInfo.InvariantCulture)), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
        } else {
            MessageBox.Show("Illegal voltage", Text);
        }
    }
}
