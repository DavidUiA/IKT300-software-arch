namespace PowerSupplyLib;

public class DummyPowerSupply : IPowerSupply {
    private const float MaxVoltage = 84;

    private double voltage;
    private bool isLocked = true;

    public void SetVoltage(double voltage) {
        this.voltage = voltage;
    }

    public double GetVoltage() {
        return voltage;
    }

    public float GetMaxVoltage() {
        return MaxVoltage;
    }

    public void SetLocked(bool isLocked) {
        this.isLocked = isLocked;
    }

    public string GetSerialNumber() {
        return "DUMMY-1234";
    }

    public string GetDeviceType() {
        return "Dummy PSU";
    }

    public string GetArticleNumber() {
        return "DUMMY-ART-12481632";
    }
}
