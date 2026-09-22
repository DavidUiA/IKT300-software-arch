using System.Drawing;
using System.Windows.Forms;

// Viser tallene fra Startup i et vindu. Current voltage kan endre seg mens vinduet er
// åpent, så den leses på nytt med en timer i stedet for bare én gang ved oppstart.
internal class StatusForm : Form
{
    private readonly Program program;

    private readonly Label deviceTypeLabel = new Label { AutoSize = true };
    private readonly Label serialNumberLabel = new Label { AutoSize = true };
    private readonly Label maxVoltageLabel = new Label { AutoSize = true };
    private readonly Label currentVoltageLabel = new Label { AutoSize = true };
    private readonly Label articleNumberLabel = new Label { AutoSize = true };
    private readonly System.Windows.Forms.Timer currentVoltageTimer = new System.Windows.Forms.Timer { Interval = 2000 };

    private readonly TextBox voltageInput = new TextBox { Width = 100 };
    private readonly Button getVoltageButton = new Button { Text = "Get voltage", AutoSize = true };
    private readonly Button setVoltageButton = new Button { Text = "Set voltage", AutoSize = true, Enabled = false };
    private readonly CheckBox autoUpdateCheckBox = new CheckBox { Text = "Auto update", AutoSize = true };
    private readonly CheckBox remoteControlCheckBox = new CheckBox { Text = "Remote control", AutoSize = true };

    public StatusForm(Program program)
    {
        this.program = program;

        Text = "PS 2000 B status";
        Width = 320;
        Height = 320;

        Label[] labels = { deviceTypeLabel, serialNumberLabel, maxVoltageLabel, currentVoltageLabel, articleNumberLabel };
        int y = 10;
        foreach (Label label in labels)
        {
            label.Location = new Point(10, y);
            Controls.Add(label);
            y += 30;
        }

        voltageInput.Location = new Point(10, y);
        Controls.Add(voltageInput);

        getVoltageButton.Location = new Point(120, y);
        getVoltageButton.Click += (s, e) => RefreshCurrentVoltage();
        Controls.Add(getVoltageButton);

        setVoltageButton.Location = new Point(220, y);
        setVoltageButton.Click += (s, e) => SetVoltageFromInput();
        Controls.Add(setVoltageButton);
        y += 30;

        autoUpdateCheckBox.Location = new Point(10, y);
        autoUpdateCheckBox.CheckedChanged += (s, e) =>
        {
            if (autoUpdateCheckBox.Checked)
            {
                currentVoltageTimer.Start();
            }
            else
            {
                currentVoltageTimer.Stop();
            }
        };
        Controls.Add(autoUpdateCheckBox);
        y += 30;

        // Lock/unlock (skru remote control av/på) tar ca. 1 sekund per kall, så det gjøres
        // her når du huker av/på i stedet for rundt hvert enkelt "Set voltage"-trykk.
        remoteControlCheckBox.Location = new Point(10, y);
        remoteControlCheckBox.CheckedChanged += (s, e) =>
        {
            if (remoteControlCheckBox.Checked)
            {
                program.TurnOnRemoteControl();
            }
            else
            {
                program.TurnOffRemoteControl();
            }
            setVoltageButton.Enabled = remoteControlCheckBox.Checked;
        };
        Controls.Add(remoteControlCheckBox);

        currentVoltageTimer.Tick += (s, e) => RefreshCurrentVoltage();
        Load += (s, e) => LoadValues();
    }

    private void LoadValues()
    {
        deviceTypeLabel.Text = $"Device type: {program.GetDeviceType()}";
        serialNumberLabel.Text = $"Serial number: {program.GetSerialNumber()}";
        maxVoltageLabel.Text = $"Max voltage: {program.GetNominalVoltage()}";
        articleNumberLabel.Text = $"Article number: {program.GetArticleNumber()}";
        RefreshCurrentVoltage();
        // auto update is off by default - checking the box starts the timer
    }

    // Get-knappen leser current voltage på nytt og oppdaterer både labelen og innskrivningsfeltet.
    private void RefreshCurrentVoltage()
    {
        double currentVoltage = program.GetCurrentVoltage();
        currentVoltageLabel.Text = $"Current voltage: {currentVoltage}";
        voltageInput.Text = currentVoltage.ToString();
    }

    // Krever at remoteControlCheckBox er huket av - knappen er grået ut ellers.
    private void SetVoltageFromInput()
    {
        if (float.TryParse(voltageInput.Text, out float setVolt))
        {
            program.SetVoltage(setVolt);
            RefreshCurrentVoltage();
        }
        else
        {
            MessageBox.Show("Ugyldig spenningsverdi", "PS 2000 B status");
        }
    }
}
