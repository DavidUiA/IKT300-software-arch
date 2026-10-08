namespace PowerSupplyLib;

public static class PowerSupplyFactory {
    public static IPowerSupply CreatePowerSupply() {
        return new Ps2000PowerSupply();
    }
}