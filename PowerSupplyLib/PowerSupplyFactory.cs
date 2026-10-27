namespace PowerSupplyLib;

public static class PowerSupplyFactory {
    public static IPowerSupply CreatePowerSupply(int PSUType) {
        IPowerSupply powerSupply;

        switch (PSUType) {
            case 0:
                powerSupply = new Ps2000PowerSupply();
                break;
            default:
                return null;
        }
        return powerSupply;
    }
}