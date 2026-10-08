// See https://aka.ms/new-console-template for more information

using System;
using System.Collections;
using System.IO.Ports;
using System.Windows.Forms;

internal class Program {
    [STAThread]
    static void Main(string[] args) {
        Program p = new Program();
        p.Startup();
        p.Test2();
    }

    // Runs the read-only checks and shows them in a window. Current voltage can change
    // while the window is open, so StatusForm keeps re-reading it on a timer.
    public void Startup() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new StatusForm(this));
    }


    public void Test2() {
        double volt = GetCurrentVoltage();
        Console.WriteLine($"Voltage:{volt}");

        // remember to turn on remote control first
        TurnOnRemoteControl();

        // setting voltage, 30V
        float setVolt = 70;
        SetVoltage(setVolt);

        GetSerialNumber();
    }

    // ---- USB / seriport ----

    // Åpner porten, skriver bytesToSend, venter readDelayMs, leser alt som er tilgjengelig og lukker porten igjen.
    private List<byte> SendAndReceive(byte[] bytesToSend, int readDelayMs) {
        List<byte> response;
        using (SerialPort port = new SerialPort("Com5", 115200, 0, 8, StopBits.One)) {
            Thread.Sleep(500);
            port.Open();
            // write to the USB port
            port.Write(bytesToSend, 0, bytesToSend.Length);
            Thread.Sleep(readDelayMs);

            response = new List<byte>();
            int length = port.BytesToRead;
            if (length > 0) {
                byte[] message = new byte[length];
                port.Read(message, 0, length);
                foreach (var t in message) {
                    response.Add(t);
                }
            }
            port.Close();
            Thread.Sleep(500);
        }
        return response;
    }

    // ---- Protokoll-hjelpere ----

    // Regner ut checksum og fyller inn de to siste bytene i bytesWithoutChecksum.
    private void CalculateChecksum(byte[] bytesWithoutChecksum) {
        int sum = 0;
        int arrayLength = bytesWithoutChecksum.Length;
        
        for (int i = 0; i < arrayLength; i++) {
            sum += bytesWithoutChecksum[i];
        }

        string hexSum = sum.ToString("X");
        string cs1 = "";
        string cs2 = "";
        if (hexSum.Length == 4) {
            cs1 = hexSum.Substring(0, hexSum.Length / 2);
            cs2 = hexSum.Substring(hexSum.Length / 2);
        } else if (hexSum.Length == 3) {
            cs1 = hexSum.Substring(0, 1);
            cs2 = hexSum.Substring(1);
        } else if (hexSum.Length == 2 || hexSum.Length == 1) {
            cs1 = "0";
            cs2 = hexSum;
        }

        if (cs1 != "") {
            bytesWithoutChecksum[arrayLength - 2] = Convert.ToByte(cs1, 16);
            bytesWithoutChecksum[arrayLength - 1] = Convert.ToByte(cs2, 16);
        }
    }

    // Bygger en query-telegram: SD, DN, OBJ, CS - en query har ikke noe DATA-felt.
    // dataLength er antall databyte vi forventer i svaret
    private byte[] BuildQueryTelegram(byte objectId, int dataLength) {
        //SD = MessageType + CastType + Direction + Length
        int SDHex = 0x40 + 0x20 + 0x10 + (dataLength - 1);
        byte SD = Convert.ToByte(SDHex.ToString(), 10);

        //SD, DN, OBJ, CS
        byte[] bytesToSend = { SD, 0x00, objectId, 0x0, 0x0 };
        CalculateChecksum(bytesToSend);

        return bytesToSend;
    }
    

    private int GetVoltage() {
        // get voltage
        byte[] byteWithOutCheckSum = BuildQueryTelegram((byte) Objects.CurrentVoltage, 6); // quert status

        // now the byte array is ready to be sent
        List<byte> responseTelegram = SendAndReceive(byteWithOutCheckSum, 500);

        int percentVolt = 0;
        if (responseTelegram == null) {
            Console.WriteLine("No telegram was read");
        } else {
            string percentVoltString = responseTelegram[5].ToString("X") + responseTelegram[6].ToString("X");
            percentVolt = Convert.ToInt32(percentVoltString, 16);
        }
        return percentVolt;
    }

    // Combines percent voltage (object 71) and nominal voltage (object 2) into the actual output voltage.
    internal double GetCurrentVoltage() {
        int percentVolt = GetVoltage();
        float nominalVoltage = GetNominalVoltage();
        return (double)percentVolt * nominalVoltage / 25600;
    }

    internal float GetNominalVoltage() {
        byte[] bytesToSend = BuildQueryTelegram((byte) Objects.NominalVoltage, 5);

        List<byte> response = SendAndReceive(bytesToSend, 50);

        float nominalVoltage = 0;
        if (response == null) {
            Console.WriteLine("No telegram was read");
        } else {
            byte[] byteArray = { response[6], response[5], response[4], response[3] };
            nominalVoltage = BitConverter.ToSingle(byteArray, 0);
        }
        return nominalVoltage;
    }

    internal void TurnOnRemoteControl() {
        // Remember the dataframe setup, SD, DN,   OBJ, DATA [hex1, hex2] checksum1, checksum2
        byte[] bytesToSendToTurnOnRC = new byte[] { 0xF1, 0x00, (byte) Objects.RemoteControl, 0x10, 0x10, 0x01, 0x47 };
        List<byte> RCresponse = SendAndReceive(bytesToSendToTurnOnRC, 50);

        if (RCresponse[3] == 0) {
            Console.WriteLine("Remote Control is turned on");
        } else {
            Console.WriteLine($"Remote control is not turned on due to error: {RCresponse[3]}");
        }
    }

    internal void TurnOffRemoteControl() {
        // DATA field is [mask, control] (object list: Mask column comes before Data column) - not
        // [control, mask] like the earlier attempt assumed. Same bit (0x10) as TurnOnRemoteControl,
        // just with control set to 0 instead of 1.
        byte[] bytesToSendToTurnOffRC = new byte[] { 0xF1, 0x00, (byte) Objects.RemoteControl, 0x10, 0x00, 0x01, 0x37 };
        List<byte> RCresponse = SendAndReceive(bytesToSendToTurnOffRC, 50);

        if (RCresponse[3] == 0) {
            Console.WriteLine("Remote Control is turned off");
        } else {
            Console.WriteLine($"Remote control is not turned off due to error: {RCresponse[3]}");
        }
    }

    internal void SetVoltage(float setVolt) {
        int percentSetValue = (int)Math.Round(25600 * setVolt / 84);

        string hexValue = percentSetValue.ToString("X");
        string hexValue1 = "";
        string hexValue2 = "";

        if (hexValue.Length == 4) {
            hexValue1 = hexValue.Substring(0, hexValue.Length / 2);
            hexValue2 = hexValue.Substring(hexValue.Length / 2);
        } else if (hexValue.Length == 3) {
            hexValue1 = hexValue.Substring(0, 1);
            hexValue2 = hexValue.Substring(1);
        } else if (hexValue.Length == 2 || hexValue.Length == 1) {
            hexValue1 = "0";
            hexValue2 = hexValue;
        }

        byte[] newbytesWithoutChecksum = { 0xF2, 0x00, (byte) Objects.SetVoltage, Convert.ToByte(hexValue1, 16), Convert.ToByte(hexValue2, 16), 0x0, 0x0 };
        CalculateChecksum(newbytesWithoutChecksum);

        List<byte> newResponseTelegram = SendAndReceive(newbytesWithoutChecksum, 500);
        if (newResponseTelegram[3] == 0) {
            Console.WriteLine("New voltage was set");
        } else {
            Console.WriteLine(newResponseTelegram[3].ToString());
        }
    }

    internal string GetSerialNumber() {
        return ReadStringObject((byte) Objects.SerialNumber);
    }

    internal string GetDeviceType() {
        return ReadStringObject((byte) Objects.DeviceType);
    }

    internal string GetArticleNumber() {
        return ReadStringObject((byte) Objects.ArticleNumber);
    }

    // Reads a "ro string" object (max 16 bytes), per the object list. Remember the dataframe
    // setup, SD, DN, OBJ, DATA, checksum1, checksum2 - a query has no DATA field.
    private string ReadStringObject(byte obj) {
        byte[] bytesToSend = { 0x7F, 0x00, obj, 0x0, 0x0 };
        CalculateChecksum(bytesToSend);
        List<byte> response = SendAndReceive(bytesToSend, 500);

        string binary = Convert.ToString(response[0], 2);
        string payloadLengtBinaryString = binary.Substring(4);
        int payloadLength = Convert.ToInt32(payloadLengtBinaryString, 2);

        string result = "";

        if (response[2] == obj) { // means that I got a response on obj, which refers to the object list.
            for (var i = 0; i < payloadLength; i++) {
                result += Convert.ToChar(response[3 + i]);
            }
        }

        return result;
    }

    enum Objects {
        DeviceType = 0x00,
        SerialNumber = 0x01,
        NominalVoltage = 0x02,
        ArticleNumber = 0x06,
        CurrentVoltage = 0x47,
        RemoteControl = 0x36,
        SetVoltage = 0x32
    }
}
