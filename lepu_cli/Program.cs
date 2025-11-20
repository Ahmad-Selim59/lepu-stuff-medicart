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
            Console.WriteLine("Lepu Device CLI");
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("No serial ports found.");
                return;
            }

            Console.WriteLine("Available Ports:");
            for (int i = 0; i < ports.Length; i++)
            {
                Console.WriteLine($"[{i}] {ports[i]}");
            }

            Console.Write("Select port index (default 0): ");
            string? input = Console.ReadLine();
            int portIndex = 0;
            if (!string.IsNullOrEmpty(input))
            {
                int.TryParse(input, out portIndex);
            }

            if (portIndex < 0 || portIndex >= ports.Length) portIndex = 0;
            string selectedPort = ports[portIndex];
            Console.WriteLine($"Connecting to {selectedPort}...");

            using (SerialPort port = new SerialPort(selectedPort, 115200, Parity.None, 8, StopBits.One))
            {
                try
                {
                    port.Open();
                    
                    // Initialize Connection (Sequence from original app)
                    InitializeConnection(port);

                    Console.WriteLine("Listening for data... (Press Ctrl+C to exit)");

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

                            ProcessBuffer(buffer);
                        }
                        Thread.Sleep(10); // Prevent high CPU usage
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private static void InitializeConnection(SerialPort port)
        {
            // Sequence to wake up/connect to the dongle/device
            Console.WriteLine("Sending init sequence...");
            
            // Disconnect first
            port.WriteLine("SPP:disconnect \r\n\0");
            Thread.Sleep(70);

            // Stop scan
            port.WriteLine("SPP:setScan off \r\n\0");
            Thread.Sleep(70);

            // Start scan (this often triggers the auto-reconnect on these dongles)
            port.WriteLine("SPP:setScan on \r\n\0");
            Thread.Sleep(70);
            
            // Set interval
            port.WriteLine("SPP:setConnInt 10 20 0 200 \r\n\0");
            Thread.Sleep(70);

            Console.WriteLine("Init sequence sent.");
        }

        private static void ProcessBuffer(List<byte> buffer)
        {
            // We need at least header (2) + type (1) + len (1) + min_payload (1) + crc (1) = 6 bytes
            while (buffer.Count >= 6)
            {
                // Look for Header 0xAA 0x55
                if (buffer[0] != 0xAA || buffer[1] != 0x55)
                {
                    buffer.RemoveAt(0); // Shift window
                    continue;
                }

                byte type = buffer[2];
                byte len = buffer[3];

                // Check if we have the full packet
                // Packet structure: AA 55 TYPE LEN [PAYLOAD...] CRC
                // Total length = 4 (Header+Type+Len) + LEN + 1 (CRC) = 5 + LEN
                // Wait... Original code says: crc check up to (Arr_answer[i + 3] + 2)
                // And checks against Arr_answer[i + Arr_answer[i + 3] + 3]
                // If i=0, len=buffer[3]. 
                // Checksum index = 0 + len + 3.
                // Total bytes needed = (len + 3) + 1 = len + 4 bytes FROM ZERO?
                // No. AA(0) 55(1) TYPE(2) LEN(3).
                // If LEN=1. Payload is at 4. CRC is at 5.
                // Total size = 6.
                // Formula: Total Size = LEN + 4.
                
                int packetSize = len + 4;

                if (buffer.Count < packetSize)
                {
                    // Wait for more data
                    break; 
                }

                // Verify CRC
                byte crcCalc = 0;
                // Original: for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                // i=0. loop k=0 to len+2.
                // Includes buffer[0]...buffer[len+2].
                for (int k = 0; k <= len + 2; k++)
                {
                    crcCalc = CrcTable[crcCalc ^ buffer[k]];
                }

                byte crcReceived = buffer[packetSize - 1]; // The last byte

                if (crcCalc == crcReceived)
                {
                    // Valid Packet
                    ParsePacket(buffer.GetRange(0, packetSize).ToArray());
                    buffer.RemoveRange(0, packetSize); // Consume packet
                }
                else
                {
                    // Bad CRC, skip header to try to resync
                    // Console.WriteLine($"CRC Fail: Calc {crcCalc:X2} vs Recv {crcReceived:X2}");
                    buffer.RemoveAt(0);
                }
            }
        }

        private static void ParsePacket(byte[] packet)
        {
            // packet[0]=AA, packet[1]=55, packet[2]=Type
            byte type = packet[2];

            // Type 0x53: SpO2 & Pulse Rate
            // Matches: (Arr_answer[i + 2] == 0x53)
            if (type == 0x53)
            {
                // Payload starts at index 4
                // Sub-type check: (Arr_answer[i + 4] == 0x01)
                // i=0. packet[4] should be 0x01.
                if (packet.Length > 4 && packet[4] == 0x01)
                {
                    int spo2 = packet[5];
                    // PR is 2 bytes: High byte at 7, Low byte at 6?
                    // Original: (Arr_answer[i + 7] << 8) + Arr_answer[i + 6]
                    int pr = (packet[7] << 8) + packet[6];
                    
                    // Status at packet[9]. 0x02 bit = Probe Off
                    bool probeOff = (packet[9] & 0x02) == 0x02;

                    if (probeOff)
                    {
                        Console.WriteLine($"[Status] Probe Off / Finger Out");
                    }
                    else
                    {
                        // Valid Reading
                        Console.WriteLine($"[Measurement] Heart Rate: {pr} bpm | SpO2: {spo2}%");
                    }
                }
            }
            // Type 0x40: NIBP (Blood Pressure)
            else if (type == 0x40)
            {
                // Just notifying we saw a BP packet
                // Subtype 0x04 = NIBP Test? 
                // Subtype 0x05 or similar = Result? 
                // (You can expand this later for BP)
            }
        }
    }
}

