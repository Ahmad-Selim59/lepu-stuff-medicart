using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace LepuCli
{
    class Program
    {
        // CRC Table from the original software
        private static readonly byte[] CrcTable =
        {
            0x00,0x5e,0xbc,0xe2,0x61,0x3f,0xdd,0x83,0xc2,0x9c,0x7e,0x20,0xa3,0xfd,0x1f,0x41,
            0x9d,0xc3,0x21,0x7f,0xfc,0xa2,0x40,0x1e,0x5f,0x01,0xe3,0xbd,0x3e,0x60,0x82,0xdc,
            0x23,0x7d,0x9f,0xc1,0x42,0x1c,0xfe,0xa0,0xe1,0xbf,0x5d,0x03,0x80,0xde,0x3c,0x62,
            0xbe,0xe0,0x02,0x5c,0xdf,0x81,0x63,0x3d,0x7c,0x22,0xc0,0x9e,0x1d,0x43,0xa1,0xff,
            0x46,0x18,0xfa,0xa4,0x27,0x79,0x9b,0xc5,0x84,0xda,0x38,0x66,0xe5,0xbb,0x59,0x07,
            0xdb,0x85,0x67,0x39,0xba,0xe4,0x06,0x58,0x19,0x47,0xa5,0xfb,0x78,0x26,0xc4,0x9a,
            0x65,0x3b,0xd9,0x87,0x04,0x5a,0xb8,0xe6,0xa7,0xf9,0x1b,0x45,0xc6,0x98,0x7a,0x24,
            0xf8,0xa6,0x44,0x1a,0x99,0xc7,0x25,0x7b,0x3a,0x64,0x86,0xd8,0x5b,0x05,0xe7,0xb9,
            0x8c,0xd2,0x30,0x6e,0xed,0xb3,0x51,0x0f,0x4e,0x10,0xf2,0xac,0x2f,0x71,0x93,0xcd,
            0x11,0x4f,0xad,0xf3,0x70,0x2e,0xcc,0x92,0xd3,0x8d,0x6f,0x31,0xb2,0xec,0x0e,0x50,
            0xaf,0xf1,0x13,0x4d,0xce,0x90,0x72,0x2c,0x6d,0x33,0xd1,0x8f,0x0c,0x52,0xb0,0xee,
            0x32,0x6c,0x8e,0xd0,0x53,0x0d,0xef,0xb1,0xf0,0xae,0x4c,0x12,0x91,0xcf,0x2d,0x73,
            0xca,0x94,0x76,0x28,0xab,0xf5,0x17,0x49,0x08,0x56,0xb4,0xea,0x69,0x37,0xd5,0x8b,
            0x57,0x09,0xeb,0xb5,0x36,0x68,0x8a,0xd4,0x95,0xcb,0x29,0x77,0xf4,0xaa,0x48,0x16,
            0xe9,0xb7,0x55,0x0b,0x88,0xd6,0x34,0x6a,0x2b,0x75,0x97,0xc9,0x4a,0x14,0xf6,0xa8,
            0x74,0x2a,0xc8,0x96,0x15,0x4b,0xa9,0xf7,0xb6,0xe8,0x0a,0x54,0xd7,0x89,0x6b,0x35
        };

        static void Main(string[] args)
        {
            if (args.Contains("-help") || args.Contains("--help"))
            {
                Console.WriteLine("Lepu Device CLI Tool");
                Console.WriteLine("Usage:");
                Console.WriteLine("  lepu_cli.exe [options]");
                Console.WriteLine("");
                Console.WriteLine("Options:");
                Console.WriteLine("  -heartrate    Start in continuous Heart Rate / SpO2 monitoring mode (default).");
                Console.WriteLine("  -nibp         Trigger a Blood Pressure (NIBP) measurement immediately.");
                Console.WriteLine("  -glu          Monitor for Blood Glucose readings.");
                Console.WriteLine("  -temperature  Monitor/Query for Temperature readings.");
                Console.WriteLine("  -help         Show this help message.");
                Console.WriteLine("");
                Console.WriteLine("Examples:");
                Console.WriteLine("  lepu_cli.exe -heartrate   # Stream PR/SpO2");
                Console.WriteLine("  lepu_cli.exe -nibp        # Start BP cuff inflation & result");
                Console.WriteLine("  lepu_cli.exe -glu         # Listen for Glucose meter data");
                Console.WriteLine("  lepu_cli.exe -temperature # Query & listen for Temperature data");
                Console.WriteLine("");
                Console.WriteLine("Data Format (Stdout):");
                Console.WriteLine("  Heart Rate:  DATA:PR={int},SPO2={int} | STATUS:PROBE_OFF");
                Console.WriteLine("  NIBP (Live): DATA:CUFF_PRESSURE={int}");
                Console.WriteLine("  NIBP (End):  DATA:NIBP_RESULT:SYS={int},DIA={int},MAP={int},PR={int},IRR={bool}");
                Console.WriteLine("  NIBP (Err):  STATUS:NIBP_ERROR={int}");
                Console.WriteLine("  Glucose:     DATA:GLU={int} (mg/dL)");
                Console.WriteLine("  Temp:        DATA:TEMP={float} (Celsius)");
                return;
            }

            string mode = "auto";
            if (args.Contains("-heartrate")) mode = "heartrate";
            if (args.Contains("-nibp")) mode = "nibp";
            if (args.Contains("-glu")) mode = "glu";
            if (args.Contains("-temperature")) mode = "temperature";

            string? selectedPort = AutoDetectPort();

            if (selectedPort == null)
            {
                Console.WriteLine("Error: No Lepu device detected on any serial port.");
                Console.WriteLine("Make sure the dongle is plugged in and not in use by another app.");
                return;
            }

            Console.WriteLine($"Detected Lepu device on {selectedPort}. Connecting...");

            using (SerialPort port = new SerialPort(selectedPort, 115200, Parity.None, 8, StopBits.One))
            {
                try
                {
                    port.Open();
                    // Re-send init in case it was just a quick probe
                    InitializeConnection(port);

                    if (mode == "nibp")
                    {
                         Console.WriteLine("Sending Start NIBP Command...");
                         StartNibp(port);
                    }
                    else if (mode == "temperature")
                    {
                         Console.WriteLine("Sending Query Temp Command...");
                         // AA 55 72 02 03 04 (Query Temp Mode)
                         // This often prompts the device to send current status/mode
                         byte[] cmd = { 0xAA, 0x55, 0x72, 0x02, 0x03, 0x04 };
                         port.Write(cmd, 0, cmd.Length);
                    }
                    else if (mode == "glu")
                    {
                         Console.WriteLine("Checking Glucose Device...");
                         // AA 55 E4 02 01 41 (Check GLU Device Type)
                         byte[] cmd = { 0xAA, 0x55, 0xE4, 0x02, 0x01, 0x41 };
                         port.Write(cmd, 0, cmd.Length);
                    }

                    Console.WriteLine($"Streaming data... Mode: {mode}");
                    
                    // Read Loop
                    List<byte> buffer = new List<byte>();
                    byte[] readBuf = new byte[1024];

                    while (true)
                    {
                        if (port.BytesToRead > 0)
                        {
                            int count = port.Read(readBuf, 0, readBuf.Length);
                            for (int i = 0; i < count; i++)
                            {
                                buffer.Add(readBuf[i]);
                            }
                            ProcessBuffer(buffer, mode);
                        }
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection lost: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Scans all available serial ports to find one that looks like a Lepu device.
        /// </summary>
        private static string? AutoDetectPort()
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0) return null;

            Console.WriteLine($"Scanning {ports.Length} ports: {string.Join(", ", ports)}...");

            foreach (var portName in ports)
            {
                try 
                {
                    using (SerialPort p = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One))
                    {
                        p.ReadTimeout = 500; 
                        p.WriteTimeout = 500;
                        p.Open();
                        
                        p.WriteLine("SPP:setScan on \r\n\0");
                        
                        byte[] buf = new byte[256];
                        int totalRead = 0;
                        
                        Thread.Sleep(200); 
                        
                        if (p.BytesToRead > 0)
                        {
                            totalRead = p.Read(buf, 0, Math.Min(p.BytesToRead, buf.Length));
                        }
                        
                        for (int i = 0; i < totalRead - 1; i++)
                        {
                            if (buf[i] == 0xAA && buf[i+1] == 0x55)
                            {
                                return portName;
                            }
                        }
                        
                        Thread.Sleep(200);
                        if (p.BytesToRead > 0)
                        {
                            int more = p.Read(buf, totalRead, Math.Min(p.BytesToRead, buf.Length - totalRead));
                            totalRead += more;
                        }

                        for (int i = 0; i < totalRead - 1; i++)
                        {
                            if (buf[i] == 0xAA && buf[i+1] == 0x55)
                            {
                                return portName;
                            }
                        }
                    }
                }
                catch 
                {
                }
            }
            return null;
        }

        private static void InitializeConnection(SerialPort port)
        {
            port.WriteLine("SPP:disconnect \r\n\0");
            Thread.Sleep(50);
            port.WriteLine("SPP:setScan off \r\n\0");
            Thread.Sleep(50);
            port.WriteLine("SPP:setScan on \r\n\0");
            Thread.Sleep(50);
            port.WriteLine("SPP:setConnInt 10 20 0 200 \r\n\0");
            Thread.Sleep(50);
        }

        private static void StartNibp(SerialPort port)
        {
            byte[] cmd = { 0xAA, 0x55, 0x40, 0x02, 0x01, 0x29 };
            port.Write(cmd, 0, cmd.Length);
        }

        private static void StopNibp(SerialPort port)
        {
            byte[] cmd = { 0xAA, 0x55, 0x40, 0x02, 0x02, 0xCB };
            port.Write(cmd, 0, cmd.Length);
        }

        private static void ProcessBuffer(List<byte> buffer, string mode)
        {
            while (buffer.Count >= 6)
            {
                if (buffer[0] != 0xAA || buffer[1] != 0x55)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                byte len = buffer[3];
                int packetSize = len + 4;

                if (buffer.Count < packetSize) break;

                byte crcCalc = 0;
                for (int k = 0; k <= len + 2; k++)
                {
                    crcCalc = CrcTable[crcCalc ^ buffer[k]];
                }

                if (crcCalc == buffer[packetSize - 1])
                {
                    ParsePacket(buffer.GetRange(0, packetSize).ToArray(), mode);
                    buffer.RemoveRange(0, packetSize);
                }
                else
                {
                    buffer.RemoveAt(0);
                }
            }
        }

        private static void ParsePacket(byte[] packet, string mode)
        {
            byte type = packet[2];
            byte subType = (packet.Length > 4) ? packet[4] : (byte)0;

            // SpO2 & Pulse Rate (Type 0x53)
            if (type == 0x53 && subType == 0x01)
            {
                if (mode == "heartrate" || mode == "auto")
                {
                    int spo2 = packet[5];
                    int pr = (packet[7] << 8) + packet[6];
                    bool probeOff = (packet[9] & 0x02) == 0x02;

                    if (probeOff)
                    {
                        Console.WriteLine($"STATUS:PROBE_OFF");
                    }
                    else
                    {
                        Console.WriteLine($"DATA:PR={pr},SPO2={spo2}");
                    }
                }
            }
            // NIBP
            else if (type == 0x40 || type == 0x42 || type == 0x43)
            {
                if (mode == "nibp" || mode == "auto")
                {
                     if (type == 0x40 && subType == 0x01) Console.WriteLine("STATUS:NIBP_START");
                     else if (type == 0x40 && subType == 0x02) Console.WriteLine("STATUS:NIBP_END");
                     else if (type == 0x42 && subType == 0x01)
                     {
                         int cuffPressure = ((packet[5] & 0x0F) << 8) + packet[6];
                         Console.WriteLine($"DATA:CUFF_PRESSURE={cuffPressure}");
                     }
                     else if (type == 0x43)
                     {
                        if (subType == 0x01)
                        {
                            int sys = ((packet[5] & 0x0F) << 8) + packet[6];
                            int map = packet[7];
                            int dia = packet[8];
                            int pr = packet[9];
                            bool irregularRhythm = (packet[5] & 0x80) == 0x80;
                            Console.WriteLine($"DATA:NIBP_RESULT:SYS={sys},DIA={dia},MAP={map},PR={pr},IRR={irregularRhythm}");
                        }
                        else if (subType == 0x02)
                        {
                            int err = packet[5];
                            Console.WriteLine($"STATUS:NIBP_ERROR={err}");
                        }
                     }
                }
            }
            // Temperature (Type 0x72)
            else if (type == 0x72)
            {
                // Subtype 0x02 often is the mode/result
                // Looking at code:
                // tmpTempVal = ((Arr_answer[i+5] & 0x0F) << 8) + Arr_answer[i+6];
                // tmpTempInt = (Arr_answer[i+5] & 0xF0) >> 4; (Mode?)
                // Let's assume standard decimal format (integer part + decimal part logic varies).
                // Based on typical usage:
                // [5] is high byte, [6] is low byte for value * 10
                // Or [5] is integer, [6] is decimal?
                // Let's output generic TEMP bytes if unsure, but usually it's a short.
                
                // Let's assume simple 2-byte value for now:
                int rawTemp = ((packet[5] & 0xFF) << 8) + packet[6];
                double temp = rawTemp / 10.0; // Usually x10
                
                Console.WriteLine($"DATA:TEMP={temp:F1}");
            }
            // Glucose (Type 0xE3, 0xE4)
            else if (type == 0xE3 || type == 0xE4)
            {
                // Often: [5] = Value High, [6] = Value Low (or vice versa)
                // Let's dump it. Usually GLU is in mg/dL or mmol/L * 10 or 18.
                int gluVal = ((packet[5] & 0xFF) << 8) + packet[6];
                // Unit often in another byte or implied.
                Console.WriteLine($"DATA:GLU={gluVal}");
            }
        }
    }
}
