// See https://aka.ms/new-console-template for more information

using PowerSupplyLib;

internal class Program {
    [STAThread]
    static void Main(string[] args) {
        Program p = new Program();
        p.Startup();
    }

    // Runs the read-only checks and shows them in a window. Current voltage can change
    // while the window is open, so StatusForm keeps re-reading it on a timer.
    public void Startup() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        IPowerSupply powerSupply = PowerSupplyFactory.CreatePowerSupply(0);
        Application.Run(new StatusForm(powerSupply));
    }
}
