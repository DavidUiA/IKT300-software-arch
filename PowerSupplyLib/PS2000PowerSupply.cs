namespace PowerSupplyLib;
using System.IO.Ports;

internal class Ps2000PowerSupply : IPowerSupply {

    // writes data into the serial usb port, and then reads response
    private List<byte> SendAndReceive(byte[] bytesToSend, int readDelayMs) {
        List<byte> response;
        using (SerialPort port = new SerialPort("Com5", 115200, 0, 8, StopBits.One)) {
            Thread.Sleep(500);
            port.Open();
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

    // calculates checksum of the bytes that will get sent
    private void AddChecksum(byte[] bytes) {
        int sum = 0;
        int arrayLength = bytes.Length;
        
        for (int i = 0; i < arrayLength; i++) {
            sum += bytes[i];
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
            bytes[arrayLength - 2] = Convert.ToByte(cs1, 16);
            bytes[arrayLength - 1] = Convert.ToByte(cs2, 16);
        }
    }

    private byte[] BuildQueryTelegram(byte objectId, int dataLength) { // dataLength: how many bytes the data is
        int direction = 0x10; // 0: send data to device, 1: get data from device
        int sender = 0x20; // 1: the telegram is sent by this program, 0: its sent by the powersupply itself
        int transmissionType = 0x40; // 01: query, 10: query response, 11: send data
        int header = transmissionType + sender + direction + (dataLength - 1); // SD, start delimiter
        byte headerByte = Convert.ToByte(header.ToString(), 10);

        //SD, DN, OBJ, CS
        byte deviceNode = 0x00; // 0: first input, 1: second input
        byte[] bytesToSend = { headerByte, deviceNode, objectId, 0x0, 0x0 }; // last 2 are placeholder, will get replaced by checksum
        AddChecksum(bytesToSend);

        return bytesToSend;
    }
    
    private int GetVoltagePercent() {
        // get voltage
        byte[] queryTelegram = BuildQueryTelegram((byte) Objects.CurrentVoltage, 6);

        // now the byte array is ready to be sent
        List<byte> responseTelegram = SendAndReceive(queryTelegram, 500);

        int percentVolt = 0;
        if (responseTelegram.Count == 0) {
            Console.WriteLine("No telegram was read");
        } else {
            string percentVoltString = responseTelegram[5].ToString("X") + responseTelegram[6].ToString("X");
            percentVolt = Convert.ToInt32(percentVoltString, 16);
        }
        return percentVolt;
    }

    // combines percentvoltage and nominalvoltage into the actual output voltage.
    public double GetVoltage() {
        int percentVolt = GetVoltagePercent();
        float nominalVoltage = GetMaxVoltage();
        return (double)percentVolt * nominalVoltage / 25600;
    }

    public float GetMaxVoltage() {
        byte[] queryTelegram = BuildQueryTelegram((byte) Objects.NominalVoltage, 5);

        List<byte> responseTelegram = SendAndReceive(queryTelegram, 50);

        float nominalVoltage = 0;
        if (responseTelegram.Count == 0) {
            Console.WriteLine("No telegram was read");
        } else {
            byte[] byteArray = { responseTelegram[6], responseTelegram[5], responseTelegram[4], responseTelegram[3] };
            nominalVoltage = BitConverter.ToSingle(byteArray, 0);
        }
        return nominalVoltage;
    }

    private void TurnOnRemoteControl() {
        // SD, DN,   OBJ, DATA [hex1, hex2] checksum1, checksum2
        byte[] commandTelegram = new byte[] { 0xF1, 0x00, (byte) Objects.RemoteControl, 0x10, 0x10, 0x01, 0x47 };
        List<byte> responseTelegram = SendAndReceive(commandTelegram, 50);

        if (responseTelegram[3] == 0) {
            Console.WriteLine("Remote Control is turned on");
        } else {
            Console.WriteLine($"Remote control is not turned on due to error: {responseTelegram[3]}");
        }
    }

    private void TurnOffRemoteControl() {
        // control set to 0 instead of 1.
        byte[] commandTelegram = new byte[] { 0xF1, 0x00, (byte) Objects.RemoteControl, 0x10, 0x00, 0x01, 0x37 };
        List<byte> responseTelegram = SendAndReceive(commandTelegram, 50);

        if (responseTelegram[3] == 0) {
            Console.WriteLine("Remote Control is turned off");
        } else {
            Console.WriteLine($"Tried to turn off remote control but failed: {responseTelegram[3]}");
        }
    }

    public void SetLocked(bool isLocked) {
        if (isLocked) {
            TurnOffRemoteControl();
        } else {
            TurnOnRemoteControl();
        }
    }

    public void SetVoltage(double setVolt) {
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

        byte[] commandTelegram = { 0xF2, 0x00, (byte) Objects.SetVoltage, Convert.ToByte(hexValue1, 16), Convert.ToByte(hexValue2, 16), 0x0, 0x0 };
        AddChecksum(commandTelegram);

        List<byte> responseTelegram = SendAndReceive(commandTelegram, 500);
        if (responseTelegram[3] == 0) {
            Console.WriteLine("New voltage was set");
        } else {
            Console.WriteLine(responseTelegram[3].ToString());
        }
    }

    public string GetSerialNumber() {
        return ReadStringObject((byte) Objects.SerialNumber);
    }

    public string GetDeviceType() {
        return ReadStringObject((byte) Objects.DeviceType);
    }

    public string GetArticleNumber() {
        return ReadStringObject((byte) Objects.ArticleNumber);
    }

    private string ReadStringObject(byte obj) {
        // SD, DN, OBJ, DATA, checksum1, checksum2
        byte[] queryTelegram = { 0x7F, 0x00, obj, 0x0, 0x0 };
        AddChecksum(queryTelegram);
        List<byte> responseTelegram = SendAndReceive(queryTelegram, 500);

        string binary = Convert.ToString(responseTelegram[0], 2);
        string payloadLengtBinaryString = binary.Substring(4);
        int payloadLength = Convert.ToInt32(payloadLengtBinaryString, 2);

        string result = "";

        if (responseTelegram[2] == obj) { // means that I got a response on obj, which refers to the object list.
            for (var i = 0; i < payloadLength; i++) {
                result += Convert.ToChar(responseTelegram[3 + i]);
            }
        }
        return result;
    }

    private enum Objects {
        DeviceType = 0x00,
        SerialNumber = 0x01,
        NominalVoltage = 0x02,
        ArticleNumber = 0x06,
        CurrentVoltage = 0x47,
        RemoteControl = 0x36,
        SetVoltage = 0x32
    }
}