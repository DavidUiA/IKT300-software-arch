using PowerSupplyLib;

// Window used to interact with the powerSupply
internal class StatusForm : Form
{
    private readonly IPowerSupply powerSupply;

    private readonly Label deviceTypeLabel = new() { AutoSize = true };
    private readonly Label serialNumberLabel = new() { AutoSize = true };
    private readonly Label maxVoltageLabel = new() { AutoSize = true };
    private readonly Label currentVoltageLabel = new() { AutoSize = true };
    private readonly Label articleNumberLabel = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer currentVoltageTimer = new() { Interval = 2000 };

    private readonly TextBox voltageInput = new() { Width = 100 };
    private readonly Button getVoltageButton = new() { Text = "Get voltage", AutoSize = true };
    private readonly Button setVoltageButton = new() { Text = "Set voltage", AutoSize = true, Enabled = false };
    private readonly CheckBox autoUpdateCheckBox = new() { Text = "Auto update", AutoSize = true };
    private readonly CheckBox remoteControlCheckBox = new() { Text = "Remote control", AutoSize = true };

    public StatusForm(IPowerSupply powerSupply) {
        this.powerSupply = powerSupply;

        Text = "PS 2000 B status";
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

        getVoltageButton.Location = new Point(120, y);
        getVoltageButton.Click += (_, _) => RefreshCurrentVoltage();
        Controls.Add(getVoltageButton);

        setVoltageButton.Location = new Point(220, y);
        setVoltageButton.Click += (_, _) => SetVoltageFromInput();
        Controls.Add(setVoltageButton);
        y += 30;

        autoUpdateCheckBox.Location = new Point(10, y);
        autoUpdateCheckBox.CheckedChanged += (_, _) => {
            if (autoUpdateCheckBox.Checked) {
                currentVoltageTimer.Start();
            } else {
                currentVoltageTimer.Stop();
            }
        };
        Controls.Add(autoUpdateCheckBox);
        y += 30;

        // Lock/unlock
        remoteControlCheckBox.Location = new Point(10, y);
        remoteControlCheckBox.CheckedChanged += (_, _) => {
            if (remoteControlCheckBox.Checked) {
                this.powerSupply.SetLocked(false);
            } else {
                this.powerSupply.SetLocked(true);
            }
            setVoltageButton.Enabled = remoteControlCheckBox.Checked;
        };
        Controls.Add(remoteControlCheckBox);

        currentVoltageTimer.Tick += (_, _) => RefreshCurrentVoltage();
        Load += (_, _) => LoadInfo();
    }

    private void LoadInfo() {
        deviceTypeLabel.Text = $"Device type: {powerSupply.GetDeviceType()}";
        serialNumberLabel.Text = $"Serial number: {powerSupply.GetSerialNumber()}";
        maxVoltageLabel.Text = $"Max voltage: {powerSupply.GetMaxVoltage()}";
        articleNumberLabel.Text = $"Article number: {powerSupply.GetArticleNumber()}";
        RefreshCurrentVoltage();
    }

    private void RefreshCurrentVoltage() {
        double currentVoltage = powerSupply.GetVoltage();
        currentVoltageLabel.Text = $"Current voltage: {currentVoltage}";
        voltageInput.Text = currentVoltage.ToString();
    }

    private void SetVoltageFromInput() {
        if (float.TryParse(voltageInput.Text, out float setVolt)) {
            powerSupply.SetVoltage(setVolt);
            RefreshCurrentVoltage();
        } else {
            MessageBox.Show("Illegal voltage", "PS 2000 B status");
        }
    }
}
