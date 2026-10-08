namespace PowerSupplyLib;

public interface IPowerSupply {
    void SetVoltage(double voltage);
    double GetVoltage();
    float GetMaxVoltage();
    void SetLocked(bool isLocked);
    string GetSerialNumber();
    string GetDeviceType();
    string GetArticleNumber();
}