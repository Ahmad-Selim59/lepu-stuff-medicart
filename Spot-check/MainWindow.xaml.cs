using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Data;
using System.Diagnostics;

namespace Spot_check
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //声明读写INI文件的API函数
        [DllImport("kernel32")] //返回0表示失败，非0为成功
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")] //返回取得字符串缓冲区的长度
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        // 导出SendMessage函数
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        // 导出FindWindow函数，用于找到目标窗口所在进程
        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public UInt32 WM_CLOSE = 0x10;

        public SerialPort serialPort1 = new SerialPort();
        public bool _keepReading;
        public byte[] Arr_answer = new byte[4000];
        public int readCnt = 0;
        public class Spo2PrC
        {
            public int SpO2 { get; set; }
            public int Pr { get; set; }
        }
        public struct ECG_FILE_DATA
        {
            public int ID;
            public DateTime stTime;
            public int ResultNo; 
            public int HRavg;
            public int ECGMaxV;
            public double ECGAmpV;
            public double ECG1mvV;
            
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4500)]
            public int[] ECGRec;
        }

        public ECG_FILE_DATA ecgFileData2 = new ECG_FILE_DATA();
        public BinaryWriter bw_ecg;
        /*TECG = record     //心电记录数据结构
       ID         : integer;
       JH_time    : Tdatetime;
       ResultNo   : integer;
       HRavg      : integer;
       ECGMaxV    : integer;
       ECGAmpV    : double;
       ECG1mvV    : double;
       ECGRec     : array[0..4499] of integer;
       // HR         : array[0..180] of integer;
     end;*/
        public Spo2PrC[] spo2Pr = new Spo2PrC[122];

        private Thread comThread = null;

        public int startX ;   //获取鼠标起始点的X坐标
        public int startY;    //获取鼠标起始点的Y坐标
        //System.Drawing.Brush brush2;
        //System.Windows.Media.Pen graph0_pen = new System.Windows.Media.Pen(System.Windows.Media.Color.Red, 1);      //设置画笔颜色和宽度
        public bool comOpen = false, end_flag = false;
        public bool drawFlag = false;
        public bool ECG_startDr = false, ECG_startM = false, move_flag=false;
        public int pulseEnd, pulseSt, EcgEnd, EcgSt, ecg_idx, ecgSaveIdx, send_idx, ecg_testCnt, ecg_1mv, ecg_amp, amp_n, ecg_max;
        public double[] imgEcgY = new double[4500];
        public byte[] ArrayPulseN = new byte[2405];
        public int[] flowResp = new int[2405];
        public int[] flowSnore = new int[2405];
        public int[] ECGdata = new int[4500];
        public byte[] ecgDataB = new byte[9000];

        public int pulseIdx = 0;
        public bool rewPulse = false;
        public bool showPulse = false;
        public int drawCnt = 2399, ecgCnt = 4500; //2399;  //600;
        public int spo2Cnt = 119;
        public int spo2Idx = 0;
        public int stopCnt = 0;
        public int prCnt = 0;
        public int interval2 = 0;
        public int idCnt, thr_cnt, charge_cnt, charge_idx, startSend, NibpMode_cnt, nibpTest_cnt, tmrCnt_getSta;
        public int tmpSysVal, tmpMapVal, tmpDiaVal, tmpPrVal, tmpGradeVal, tmpNibpMode, tmpBSGradeVal, glu_unit, ua_unit, chol_unit, chkGluDev_cnt, askTempMode_cnt;
        public double tmpSysVal2, tmpMapVal2, tmpDiaVal2, tmpGlu0Val, tmpGlu1Val, tmpGlu2Val, tmpUa0Val, tmpUa1Val, tmpUa2Val, tmpChol0Val, tmpChol1Val, tmpChol2Val;
        public double tmpTempVal, tmpTempVal0, tmpTempVal20;
        public int tmpTempInt, tmpTempInt0;
        public DateTime date1, gluDate, uaDate, cholDate, nibpDate;
        public TimeSpan intv2 = new TimeSpan(0, 0, 0, 2, 0);
        public string verS2, verH2, productID, customerId2, devAddr, curDir, tmpDir;
        public int flowIdx = 0;
        public int flowSt, flowEnd;
        public bool rewFlow = false, rewEcg = false;
        public bool showFlow = false, showEcg = false, verSendFF = false;

        public DispatcherTimer timer_battery = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1000), IsEnabled = false };
        public DispatcherTimer timer_start = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(200), IsEnabled = false };
        //
        public DispatcherTimer timer_display = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100), IsEnabled = false };
        public DispatcherTimer timer_pulse = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(94), IsEnabled = false };
        public DispatcherTimer timer_fsnore = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(999), IsEnabled = false };
        public DispatcherTimer timer3 = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(40), IsEnabled = false };
        public DispatcherTimer timer_open = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(4000), IsEnabled = false };
        public DispatcherTimer timer_getSta = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(200), IsEnabled = false };
        
        public DispatcherTimer timer4 = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(3000), IsEnabled = false };
        public DispatcherTimer timer_id = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(200), IsEnabled = false};
        public DispatcherTimer timer_NibpMode = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500), IsEnabled = false };
        public DispatcherTimer timer_nibpTest = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1000), IsEnabled = false };
        public DispatcherTimer timerSys = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(40), IsEnabled = false };
        public DispatcherTimer timer_Datetime = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(300), IsEnabled = false };
        public DispatcherTimer timer_ecg = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(166), IsEnabled = false };
        public DispatcherTimer timer_chkGluDev = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500), IsEnabled = false };
        public DispatcherTimer timer_askTempMode = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100), IsEnabled = false };

        //chart_wave;
        public string[] nibpGrade = {"--", "Opti", "Norm", "G1-HTN", "G2-HTN", "G3-HTN"};
        public string[] Ecg_status = { "Preparing...", "Measuring...", "Analysing..." };
        public string[] Vpc_Type_30s  ={"0- No irregularity found.","1- Suspected a little fast beat.","2- Suspected fast beat.","3- Suspected short run of fast beat.","4- Suspected a little slow beat.",
"5- Suspected slow beat.","6- Suspected occasional short beat interval.","7- Suspected irregular beat interval.","8- Suspected fast beat with short beat interval.","9- Suspected slow beat with short beat interval.",
"10- Suspected slow beat with irregular beat interval.","11- Waveform baseline wander. Measure again.","12- Suspected fast beat with baseline wander.","13- Suspected slow beat with baseline wander.",
"14- Suspected occasional short beat interval with baseline wander.","15- Suspected irregular beat interval with baseline wander.","16- Poor signal. Measure again."};

        public string[] DevAllName = { "PC-100", "PC-200", "PC-300SNT" };
        public string[] dev_list = new string[51];
        public string bdScanOn = "SPP:setScan on \r\n\0"; //'SPP:connect ' + addr + ' public ';
        public string bdScanOff = "SPP:setScan off \r\n\0";
        public string bdSetInt = "SPP:setConnInt 10 20 0 200 \r\n\0";
        public string bdGetStatus = "SPP:getStatus \r\n\0";
        Byte[] TxData_ver0 = {0xAA, 0x55, 0x51, 0x02, 0x01, 0xC8}; //AA 55 51 02 01 C8
        Byte[] TxData_ver1 = {0xAA, 0x55, 0xFF, 0x02, 0x02, 0x28}; //AA 55 FF 02 02 28 
        Byte[] TxData_ver2 = {0xAA, 0x55, 0xFF, 0x02, 0x04, 0xF5}; //AA 55 FF 02 04 F5 
        Byte[] TxData_verH600 = { 0xAA, 0x55, 0x31, 0x02, 0x01, 0x6D }; //AA 55 31 02 01 6D
        Byte[] TxData_id = { 0xAA, 0x55, 0xFF, 0x02, 0x01, 0xCA }; //AA 55 FF 02 01 CA //Check ID
        Byte[] TxData_CustomerId = { 0xAA, 0x55, 0xFF, 0x02, 0x10, 0x09 };  //AA 55 FF 02 10 09 //Check CustomerID
        Byte[] TxData_send   = {0xAA, 0x55, 0x50, 0x03, 0x02, 0x01, 0x27};  //AA 55 50 03 02 01 27 //set sending status     -write_sta4
        Byte[] TxData_noSend = {0xAA, 0x55, 0x50, 0x03, 0x02, 0x01, 0x79 }; //AA 55 50 03 02 00 79 //set no sending status  -write_sta4
        Byte[] TxData_chkNibpMode = {0xAA, 0x55, 0x40, 0x03, 0x03, 0x10, 0x18}; //AA 55 40 03 03 10 18 Check NIBP Mode
        Byte[] TxData_NibpTest  = { 0xAA, 0x55, 0x40, 0x02, 0x04, 0x16};  //AA 55 40 02 04 16  , NIBP self test - calibrate
        Byte[] TxData_NibpStart = { 0xAA, 0x55, 0x40, 0x02, 0x01, 0x29 }; //AA 55 40 02 01 29  , NIBP start measuring
        Byte[] TxData_NibpEnd   = { 0xAA, 0x55, 0x40, 0x02, 0x02, 0xCB }; //AA 55 40 02 02 CB  , Nibp end measuring
        Byte[] TxData_chkGluDevice = {0xAA, 0x55, 0xE4, 0x02, 0x01, 0x41};//AA 55 E4 02 01 41  , Check Glucose device type
        Byte[] TxData_delGluDevData = { 0xAA, 0x55, 0xE5, 0x02, 0x01, 0xEA };//AA 55 E5 02 01 EA  , //delete all glucose data for device  'On Call Sure Sync'
        Byte[] TxData_askTempMode = { 0xAA, 0x55, 0x72, 0x02, 0x03, 0x04 }; //AA 55 72 02 03 04 //Query Temp Mode 查询奥极体温计模式、温度单位。

        public byte[] crcNew_table = 
        {0x00,0x5e,0xbc,0xe2,0x61,0x3f,0xdd,0x83,0xc2,0x9c,0x7e,0x20,0xa3,0xfd,0x1f,0x41,
        0x9d,0xc3,0x21,0x7f,0xfc,0xa2,0x40,0x1e,0x5f,0x01,0xe3,0xbd,0x3e,0x60,0x82,0xdc,  // v[27]=0xbd =189,
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

        // Constant
        public const int SpO2_Data_Top_Limit = 127;
        public const int Base_Line_Data_8_Bit = 128;
        public const int Base_Line_Data_12_Bit = 2048;
        public const int Canvas_Count_of_Voltage = 6; // the amplitude of ECG signal voltage (-3~+3 mV)
        public const double DPI_One_Inch_to_mm = 25.4; // 1 Inch = 25.4 mm
        public const int Sampling_Frequency = 150; // Hz
        public const double Wave_Speed = 25; // 25 mm/s

        public IntPtr hBluetooth;
        public IntPtr hSerial;

        public int m_nDiscSta = 0, m_nVirPortSta = 0, m_nSerialSta = 0, m_nNIBPSta = 0, m_nECGSta = 0, m_nNIBPMode = 0, m_nPower = 0, m_nCustomerID = 0;

        public int m_nADValue = 1, m_nGain12Bit = 1;

        public Polyline SpO2Line = new Polyline();
        public PointCollection SpO2Collection = new PointCollection();

        public Polyline RespLine = new Polyline();
        public PointCollection RespCollection = new PointCollection();

        public Polyline SnoreLine = new Polyline();
        public PointCollection SnoreCollection = new PointCollection();

        public Polyline ECGLine = new Polyline();
        public PointCollection ECGCollection = new PointCollection();
        //public DispatcherTimer StatusTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(20), IsEnabled = true };
        public static string byteToHexStr(byte[] bytes, int count)
        {
            string returnStr = "";
            if (bytes != null)
            {
                //for (int i = 0; i < bytes.Length; i++)
                for (int i = 0; i < count; i++)
                {
                    returnStr += bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }
        
        private int SysDiaGrade(int tmpSysVal, int tmpDiaVal)
        {
            int i=0;
            if (tmpSysVal < 120)
            {
                if (tmpDiaVal < 80)
                    i = 1;
                else if ( (tmpDiaVal >= 80) && (tmpDiaVal <= 84) )
                    i = 2;
                else if ( (tmpDiaVal >= 85) && (tmpDiaVal <= 89) )
                    i = 3;
                else if ( (tmpDiaVal >= 90) && (tmpDiaVal <= 99) )
                    i = 4;
                else if ( (tmpDiaVal >= 100) && (tmpDiaVal <= 109) )
                    i = 5;
            }
            else if ( (tmpSysVal >= 120) && (tmpSysVal <= 129) )
            {
                if (tmpDiaVal <= 84) 
                    i = 2;
                else if ( (tmpDiaVal >= 85) && (tmpDiaVal <= 89) )
                    i = 3;
                else if ( (tmpDiaVal >= 90) && (tmpDiaVal <= 99) )
                    i = 4;
                else if ( (tmpDiaVal >= 100) && (tmpDiaVal <= 109) )
                    i = 5;
                else if (tmpDiaVal >= 110)
                i = 6;
            }
            else if ( (tmpSysVal >= 130) && (tmpSysVal <= 139) )
            {
                if (tmpDiaVal <= 89)
                    i = 3;
                else if ( (tmpDiaVal >= 90) && (tmpDiaVal <= 99) )
                    i = 4;
                else if ( (tmpDiaVal >= 100) && (tmpDiaVal <= 109) )
                    i = 5;
                else if (tmpDiaVal >= 110)
                    i = 6;
            }
            else if ( (tmpSysVal >= 140) && (tmpSysVal <= 159) )
            {
                if (tmpDiaVal <= 99)
                    i = 4;
                else if ( (tmpDiaVal >= 100) && (tmpDiaVal <= 109) )
                    i = 5;
                else if (tmpDiaVal >= 110)
                    i = 6;
            }
            else if ( (tmpSysVal >= 160) && (tmpSysVal <= 179) )
            {
                if (tmpDiaVal <= 109)
                    i = 5;
                else if (tmpDiaVal >= 110)
                    i = 6;
            }
            else if (tmpSysVal >= 180)
            {
                i = 6;
            }
            return(i);
        }

        private void setNibpMode(int mode2)
        {
            int i;
            byte crcVal = 0;
            byte[] TxData = new byte[40];
            TxData[0] = 0xAA;
            TxData[1] = 0x55;
            TxData[2] = 0x40;
            TxData[3] = 0x03;
            TxData[4] = 0x03;
            TxData[5] = (byte)mode2;
            crcVal = 0;
            
            for (i = 0; i <= 5; i++)
                crcVal = crcNew_table[crcVal ^ TxData[i]];
            TxData[6] = crcVal;
            try
            {
                serialPort1.Write(TxData, 0, 7);
            }
            catch (Exception) { };
        }

        void setGluDevice(int type2) // set Glucose device type as bioland or BeneCheck.  1 : bioland ; 2 : BeneCheck ; 3 : On Call Sure Sync.
        {
            int i;
            byte crcVal = 0;
            byte[] TxData = new byte[40];
            if (!((type2 >= 1) && (type2 <= 3)) ) return;
            TxData[0] = 0xAA;
            TxData[1] = 0x55;
            TxData[2] = 0xE3;
            TxData[3] = 0x03;
            TxData[4] = (byte)type2;
            TxData[5] = 0x00;

            crcVal = 0;
            for (i = 0; i <= 5; i++)
                crcVal = crcNew_table[crcVal ^ TxData[i]];
            TxData[6] = crcVal;
            try
            {
                serialPort1.Write(TxData, 0, 7);
            }
            catch (Exception) { }; // set GLU device type 01:爱奥乐 bioland; type  02 : 百捷 BeneCheck; 3 : On Call Sure Sync.
            //AA 55 E3 03 01 00 35 // AA 55 E3 03 02 00 60  // AA 55 E3 03 03 00 A4
        }
        /*AA 55 72 03 02 11 CD  0x11表示耳温模式+摄氏度
          AA 55 72 03 02 21 73  0x21表示成人额温模式+摄氏度

          AA 55 72 03 02 12 2F  0x12表示耳温模式+华氏度；
          AA 55 72 03 02 22 91  0x22表示成人额温模式+华氏度；*/
        private void setTempMode(int mode2) // AA 55 72 03 02 mode crc  设置奥极体温计模式、温度单位。
        {
            int i;
            byte crcVal = 0;
            byte[] TxData = new byte[40];
            TxData[0] = 0xAA;
            TxData[1] = 0x55;
            TxData[2] = 0x72;
            TxData[3] = 0x03;
            TxData[4] = 0x02;
            TxData[5] = (byte)mode2;
            crcVal = 0;

            for (i = 0; i <= 5; i++)
                crcVal = crcNew_table[crcVal ^ TxData[i]];
            TxData[6] = crcVal;
            try
            {
                serialPort1.Write(TxData, 0, 7);
            }
            catch (Exception) { };
        }

        void set12BitEcg(bool ECG_bit12) // set Ecg Data 12bit or 8bit
        {
            int i;
            byte crcVal = 0;
            byte[] TxData = new byte[40];
            TxData[0] = 0xAA;
            TxData[1] = 0x55;
            TxData[2] = 0x30;
            TxData[3] = 0x03;
            TxData[4] = 0x05;
            if (ECG_bit12)
                TxData[5] = 0x02; //[6] = 0x3B
            else
                TxData[5] = 0x01; //[6] = 0xD9

            crcVal = 0;
            for (i = 0; i <= 5; i++)
                crcVal = crcNew_table[crcVal ^ TxData[i]];
            TxData[6] = crcVal;
            try
            {
                serialPort1.Write(TxData, 0, 7);
            }
            catch (Exception) { };

        }

        void ECGInit()
        {
            int i;
            ecg_idx = 0;
            EcgSt = 0;
            EcgEnd = 0;
            ecg_1mv = 0;
            ecg_amp = 0;
            lblEcg_1mv.Content = "";
            lblEcg_amp.Content = "";
            lblHrValue.Content = "--";
            lblEcgReValue.Content = "--";
            lblEcgStatus.Visibility = System.Windows.Visibility.Hidden;
            ECGCollection.Clear();
            for (i = 0; i < ECGdata.Length; i++)
                ECGdata[i] = 0;
            ecgFileData2.ID = 0;
            ecgFileData2.stTime = DateTime.Now;
            ecgFileData2.HRavg  = 0;
            ecgFileData2.ECGMaxV = 0;
            ecgFileData2.ECGAmpV = 0;
            ecgFileData2.ECG1mvV = 0;
            Array.Clear(ecgFileData2.ECGRec,0,4500);
        }

        private void openInit()
        {
            lblTempWarn.Visibility = System.Windows.Visibility.Hidden;
            timer_getSta.IsEnabled = false;
            ecgSaveIdx = 0;
            image_heart.Visibility = System.Windows.Visibility.Hidden;
            timer_battery.IsEnabled = true;
            timer_display.IsEnabled = true;
            timer3.IsEnabled = true;
            int i = 0;
            //readCnt = 0;
            //for (i = 0; i < Arr_answer.Length; i++)
            //    Arr_answer[i] = 0;
            SpO2Collection.Clear();
            RespCollection.Clear();
            SnoreCollection.Clear();
            for (i = 0; i < ArrayPulseN.Length; i++)
                ArrayPulseN[i] = 0;
            for (i = 0; i < flowResp.Length; i++)
            {
                flowResp[i] = 0;
                flowSnore[i] = 0;
            }
            drawFlag = true;
            comOpen = true;
            stopCnt = 0;
            //fillChanged = false;
            ECG_startDr = false;
            ECG_startM = false;
            ECGInit();

            pulseIdx = 0;
            pulseSt = 0;
            pulseEnd = 0;
            rewPulse = false;
            showPulse = false;

            flowIdx = 0;
            flowSt = 0;
            flowEnd = 0;
            rewFlow = false;
            showFlow = false;

            drawFlag = true;
            timer_open.IsEnabled = false;
            idCnt = 0;
            timer_id.IsEnabled = true;
            
            lblCheckProbe.Visibility = System.Windows.Visibility.Hidden;
            lblSProbeValue.Visibility = System.Windows.Visibility.Visible;
            timer4.IsEnabled = true;
            timer3.IsEnabled = true;
            timer_battery.IsEnabled = true;
            startSend = 0;
            timer_start.IsEnabled = true;
            cboxNibpMode.SelectedIndex = 0;
            NibpMode_cnt = 0;
            timer_NibpMode.IsEnabled = true;
            chkGluDev_cnt = 0;
            timer_chkGluDev.IsEnabled = true;
            this.Show();
        }
        //---
        public void BDaddrPrc(string rec)
        {
            string strRec = rec;//"public dat =\xCC\xDE\xED,\x8B\xB3\r\n\0SPP: dev addr CC:DE:ED:2C:8B:B3 public name =PC-68B:67295\r\n\0SPP: dev addr CC:DE:ED:2C:8B:B3 public dat =\xCC\xDE\xED,\x8B\xB3\r\n\0SPP: dev addr CC:DE:ED:2C:8B:B3 public name =PC-68B:67295\r\n\0t";
            string str_old = strRec;
            string str2, BTName, s0, s2, s3, dev2, addr2;
            int cnt = 0;
            int dev_cnt = 0;
            int i, j, j0, k, p2, pos2, len2;
            bool devExist;
            for (i = 0; i < 51; i++)
                dev_list[i] = "";

            for (k = 0; k < DevAllName.Length; k++)
            {
                strRec = str_old;
                p2 = strRec.IndexOf(" public name =" + DevAllName[k]);
                while (p2 > 0)
                {
                    cnt++;
                    BTName = DevAllName[k];
                    j = strRec.IndexOf(" public name =" + BTName);
                    s2 = " public name =";
                    s3 = strRec.Substring(j + s2.Length, strRec.Length - j - s2.Length);
                    //pos2 := pos(chr(13) + chr(10) + chr(0), s3);
                    pos2 = s3.IndexOf("\r\n\0");
                    dev2 = "";
                    if (pos2 >= 0)
                        dev2 = s3.Substring(0, pos2);// copy(s3,1, pos2-1);
                    s2 = " public name =" + BTName;
                    str2 = strRec.Substring(j + s2.Length, strRec.Length - j - s2.Length); //copy(strRec, j + length(' public name ='+BTName), length(strRec));
                    s2 = "SPP: dev addr 98:7B:F3:50:8D:10";
                    len2 = s2.Length;
                    s0 = "";
                    if (j > len2)
                        s0 = strRec.Substring(j - len2, strRec.Length - j + len2); //copy(strRec, j-len2,length(strRec) );
                    //  if (j > len2) and (pos('SPP: dev addr ', s0)=1) and (pos(BTName, dev2) >0) then
                    if ((j > len2) && (s0.IndexOf("SPP: dev addr ") == 0) && (dev2.IndexOf(BTName) >= 0))
                    {
                        BTName = dev2;
                        j = s0.IndexOf("SPP: dev addr ");
                        s2 = "SPP: dev addr ";
                        s0 = s0.Substring(j + s2.Length, s0.Length - j - s2.Length);
                        j0 = s0.IndexOf(" ");
                        addr2 = "";
                        if (j0 >= 0)
                            addr2 = s0.Substring(0, j0 + 1);
                        addr2 = addr2.Trim();
                        if (addr2.Length > 5)
                        {
                            s2 = addr2 + "  " + dev2;
                            devExist = false;
                            if (dev_cnt > 0)
                            {
                                for (i = 0; i < dev_cnt; i++)
                                {
                                    if (s2 == dev_list[i])
                                    {
                                        devExist = true;
                                        break;
                                    }
                                }
                            }
                            if (devExist == false)
                            {
                                dev_cnt++;
                                dev_list[dev_cnt - 1] = s2;
                            }
                        }//if length(addr2) > 5 then
                    }
                    strRec = str2;
                    p2 = strRec.IndexOf(" public name =" + DevAllName[k]);
                }//while p2 > 0 do
            }//for (k = 0; k < DevAllName.Length; k++ )
            s2 = "";
            for (i = 0; i < dev_cnt; i++)
            {
                //s2 = s2 + "\n" + dev_list[i];
                s2 = dev_list[i];
                devExist = false;
                if (listBox_dev.Items.Count > 0)
                    for (k = 0; k < listBox_dev.Items.Count; k++)
                    {
                        if (listBox_dev.Items[k].ToString() == s2)
                        {
                            devExist = true;
                            break;
                        }
                    }
                if (devExist == false)
                    listBox_dev.Items.Add(s2);
                //listBox_dev.Refresh();
            }
            if ((devAddr == "") && (comOpen == false) && (listBox_dev.Items.Count > 0))
            {
                listBox_dev.SelectedIndex = 0;
                s2 = listBox_dev.SelectedItem.ToString();
                devAddr = s2.Substring(0, s2.IndexOf(" "));
                lblDevAddr.Content = devAddr;
                //MessageBox.Show("devAddr = " + devAddr, "Notice", System.Windows.Forms.MessageBoxButtons.OK);
            }
        }

        public void dataPrc()
        {
            byte crcVal = 0;
            int i = 0;
            int k = 0;
            double v2;
            int j = -1;
            int j0 = -1;
            int tmp_t = 0;
            double PIval = 0.0, y_val;
            int spo2, pr, tmpErr;
            tmp_t = readCnt;
            //TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 100);
            string s2 = "SPP: dev addr 98:7B:F3:50:8D:10";
            bool restartFlag = false;
            if (comOpen == false)
            {
                string strRec = "";
                for (i = 0; i < tmp_t; i++)
                {
                    strRec = strRec + (char)Arr_answer[i];
                }
                if ((strRec.IndexOf("SPP: dev addr ") > 0) && (strRec.IndexOf(" public name =") > s2.Length))
                    BDaddrPrc(strRec);
            }
            if (tmp_t > 5)
            {
                i = -1;
                while (i < (tmp_t - 5))
                {
                    j = -1;
                    ++i;
                    date1 = DateTime.Now;
                    //+++
                    if ( (tmp_t > (Arr_answer[i+3]+3+i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i+1] == 0x55) && (Arr_answer[i+2] == 0xFF) && (Arr_answer[i+3]>2) && (Arr_answer[i+4]==0x01) )
                    {  //ProductID
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i+3]+2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i+k]];
                        if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            productID = "";
                            for (k = 1; k<= (Arr_answer[i+3]-2); k++)
                                productID = productID + (char)(Arr_answer[i+4+k]);
                            sbarProduct.Content = productID;
                            j = i + Arr_answer[i + 3] + 4;
                            //break;
                        }
                    }
                    //else if ( (tmp_t > (Arr_answer[i + 3] + 3 + i)) && ( ((Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xFF) && (Arr_answer[i + 3] > 2) && ((Arr_answer[i + 4] == 0x02) || (Arr_answer[i + 4] == 0x04)) )
                    //    || ((Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x51) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01)) )  )
                    else if ( ((i+Arr_answer[i+3]+3) < tmp_t) && (Arr_answer[i] == 0xAA) && (Arr_answer[i+1] == 0x55) && (
                    ((Arr_answer[i+2] == 0xFF) && (Arr_answer[i+3] == 0x05) && (Arr_answer[i+4]==0x02))          //version 1
                 || ((Arr_answer[i+2] == 0xFF) && (Arr_answer[i+3] == 0x06) && (Arr_answer[i+4]==0x04))          //version 2
                 || ((productID.IndexOf("H_600") > 0) && (Arr_answer[i + 2] == 0x31) && (Arr_answer[i + 3] >= 0x04) && (Arr_answer[i + 4] == 0x01))  //for H600 Arr_answer[i + 3] = 0x04, Arr_answer[i + 3] = 0x09 for new version 
                 || ((Arr_answer[i + 2] == 0x51) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01))  //for OxiSensor (Arr_answer[i+3] = 0x04), for PC-300SNT old version (Arr_answer[i+3] = 0x05) 
                 )  )
                    {  //version
                        crcVal = 0;
                        for (k=0; k<=(Arr_answer[i+3]+2); k++ )
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i+k]];
                        if (crcVal == Arr_answer[i+Arr_answer[i+3]+3] )
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            if (Arr_answer[i+2] == 0xFF)
                                verSendFF = true;
                            verS2 =  ( (Arr_answer[i+5] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+5] & 0x0f).ToString();
                            verH2 = ( (Arr_answer[i+6] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+6] & 0x0f).ToString();
                            //-
                            if ((Arr_answer[i+3] > 0x04) && (Arr_answer[i+2] != 0x31) )
                            {//

                                k = ((Arr_answer[i+7] & 0x30) >> 4);
                                if (Arr_answer[i+4]==0x04)
                                {
                                    verS2 = ( (Arr_answer[i+5] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+5] & 0x0f).ToString() 
                                        + "." + ( (Arr_answer[i+6] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+6] & 0x0f).ToString();
                                    verH2 = ( (Arr_answer[i+7] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+7] & 0x0f).ToString();
                                
                                    k = ((Arr_answer[i+8] & 0x30) >> 4);
                                }
                                sbarVersion.Content = "SW : " + verS2 + "  HW : " + verH2;
                                //--
                                if (k == 1)
                                {
                                    lblCharge.Content = "+";
                                    //+ if timer_charge.Enabled = false then
                                    //+    timer_charge.Enabled := true;
                                }
                                else if (k == 2)
                                {
                                    lblCharge.Content = "+";
                                    //+ timer_charge.Enabled := false;
                                }
                                else {
                                    lblCharge.Content = "";
                                    //+ timer_charge.Enabled := false;
                                }
                                if ((k == 1) || (k == 2))
                                    k = 3;
                                else {
                                    k = Arr_answer[i+7] & 0x03;
                                    if (Arr_answer[i+4]== 0x04)
                                        k = Arr_answer[i+8] & 0x03;
              
                                }
                                sbarBattery.Content = "Battery : " + k.ToString();
                                //+ if timer_charge.Enabled = false then
                                //+     Image_bat.Picture := imgBat[k].Picture;
                                charge_cnt = k;
                            } 
                            else
                            {      //for OxiSensor and H600 
                                k = -1;
                                btnSysEnd.IsEnabled = false;
                                if ((Arr_answer[i+3] == 0x09) && (Arr_answer[i+2] == 0x31) ) //H600 new version
                                {
                                    verS2 = Arr_answer[i+5].ToString() + "." + Arr_answer[i+6].ToString() + "." + Arr_answer[i+7].ToString() + "." + (char)(Arr_answer[i+8]) + Arr_answer[i+9].ToString() + Arr_answer[i+10].ToString();
                                    verH2 = ( (Arr_answer[i+11] & 0xf0) >> 4 ).ToString() + "." + (Arr_answer[i+11] & 0x0f).ToString();
                                    sbarVersion.Content = "SW : " + verS2 + "  HW : " + verH2;
                                }
                            }//-

                            if (k <= 0)
                            {
                                btnSysStart.IsEnabled = false;
                                btnCalibrate.IsEnabled = false;
                            }
                            else {
                                btnSysStart.IsEnabled = true;
                                btnCalibrate.IsEnabled = true;
                            }
                            //--
                            j = i + Arr_answer[i + 3] + 4;
                            //break;  
                        }
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x50) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x02))
                    {  // PC-200,100  probe off
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            stopCnt = 0;
                            lblSProbeValue.Content = "Off";
                            j = i + Arr_answer[i + 3] + 4;
                        } 
                    }
                    //+++
                    else if ((Arr_answer[i + 3] > 0) && (tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x53) && (Arr_answer[i + 4] == 0x01)) //and (Arr_answer[i+3] = $07)
                    {  // 发送参数包 血氧脉率
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                        {
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        }

                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            drawFlag = true;
                            spo2Idx = 1;
                            //spo2Pr[spo2Idx].SpO2 = Arr_answer[i+5];
                            //spo2Pr[spo2Idx].Pr = (Arr_answer[i+7] << 8) + Arr_answer[i+6];
                            spo2 = Arr_answer[i + 5];
                            //pr = (Arr_answer[i+7] * 256) + Arr_answer[i+6];
                            pr = (Arr_answer[i + 7] << 8) + Arr_answer[i + 6];

                            flowResp[flowIdx] = spo2;
                            flowSnore[flowIdx] = pr;
                            showFlow = true;
                            flowIdx = flowIdx + 1;
                            if (flowIdx > drawCnt)
                            {
                                flowIdx = 0;
                                rewFlow = true;
                            }
                            if ((timer_fsnore.IsEnabled == false) && (flowIdx > 1))
                                timer_fsnore.IsEnabled = true;

                            if (spo2 > 0)
                                lblSpO2Value.Content = spo2.ToString() + " (%)";
                            else
                                lblSpO2Value.Content = "--" + " (%)";

                            if (pr > 0)
                            {
                                interval2 = 20 * 1000 / pr;
                                lblSPRValue.Content = pr + " (bpm)";

                            }
                            else
                            {
                                lblSPRValue.Content = "--" + " (bpm)";
                                interval2 = 0;
                            }

                            if (pr < 0) pr = 0;

                            PIval = Arr_answer[i + 8] / 10.0;
                            lblSPIValue.Content = PIval + " (%)";
                            if (Arr_answer[i + 8] == 0)
                                lblSPIValue.Content = "--" + " (%)";

                            if ((Arr_answer[i + 9] & 0x02) == 0x02)
                            {
                                lblSpO2Value.Content = "--" + " (%)";
                                lblSPRValue.Content = "--" + " (bpm)";
                                lblSPIValue.Content = "--" + " (%)";
                                lblSProbeValue.Content = "Off";
                                restartFlag = true;
                                //showPulse = false;
                            }
                            else lblSProbeValue.Content = "On";
                            
                            j = i + Arr_answer[i + 3] + 4;
                        }
                    }
                    //+++
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x52) && (Arr_answer[i+3] == 0x04) && (Arr_answer[i + 4] == 0x01)) 
                    {   //波形发送
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                        {
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        }

                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            if (drawFlag)
                            {
                                for (k = 0; k <= 1; k++)
                                    ArrayPulseN[pulseIdx + k] = Arr_answer[i + 5 + k];// 
                                pulseIdx = pulseIdx + 2;
                                if (pulseIdx > drawCnt)
                                {
                                    pulseIdx = 0;
                                    rewPulse = true;
                                }

                                if ((timer_pulse.IsEnabled == false) && (pulseIdx > 50))  //50
                                    timer_pulse.IsEnabled = true;
                                showPulse = true;
                            } //if (drawFlag) then
                            j = i + Arr_answer[i + 3] + 4;
                        } //if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x40) && (Arr_answer[i + 3] == 0x03) && (Arr_answer[i + 4] == 0x03))
                    {     // return Nibp Mode
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            k = Arr_answer[i+5];
                            if ((k < 0) || (k > 2)) k = 0;
                            cboxNibpMode.Visibility = System.Windows.Visibility.Visible;
                            cboxNibpMode.SelectedIndex = k;
                            timer_NibpMode.IsEnabled = false;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x40) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x01))
                    {     // NIBP begin
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //+panel_sysWarn.Visible := false;
                            //+lblStatus.Caption := '';
                            //writeln(tmpfile,' SYSbegin ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+5],2)) ;
                            cboxNibpMode.IsEnabled = false;
                            //Panel_Irr.Visible := false;
                            //lblGradeVal.Caption := '';
                            lblIrr.Visibility = System.Windows.Visibility.Hidden;
                            lblSysValue.Content = "--";
                            lblDiaValue.Content = "--";
                            lblMapValue.Content = "--";
                            lblnPrValue.Content = "--";
                            lblNibpGrade.Content = "";
                            pbarNibp.Value = 0;
                            pbarNibp.Visibility = System.Windows.Visibility.Visible;
                            timer_nibpTest.IsEnabled = false;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x40) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x02))
                    {     // NIBP End
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            cboxNibpMode.IsEnabled = true;
                            lblSysValue.Content = "--";
                            lblDiaValue.Content = "--";
                            lblMapValue.Content = "--";
                            lblnPrValue.Content = "--";
                            pbarNibp.Value = 0;
                            pbarNibp.Visibility = System.Windows.Visibility.Hidden;
                            timer_nibpTest.IsEnabled = false;
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                        //-
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x42) && (Arr_answer[i + 3] == 0x04) && (Arr_answer[i + 4] == 0x01))
                    {     // NIBP Value tmp
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;

                            tmpSysVal = ((Arr_answer[i+5] & 0x0f) << 8) + Arr_answer[i+6];
                            if (cboxSys.SelectedIndex == 0)
                                lblSysValue.Content = tmpSysVal.ToString();
                            else
                                lblSysValue.Content = ( ((tmpSysVal*133.0)/100) / 10).ToString("#.#");
                            nibpTest_cnt = 0;
                            pbarNibp.Visibility = System.Windows.Visibility.Visible;
                            pbarNibp.Value = tmpSysVal;
                            lblDiaValue.Content = "--";
                            lblMapValue.Content = "";
                            lblNibpGrade.Content = "";
                            //lblGradeValue.Content = "";
                            if ( (Arr_answer[i+5] & 0x10) == 0 )
                            {
                                lblnPrValue.Content = "--";
                            }
                            else
                            {
                                lblnPrValue.Content = "--";
                                timerSys.IsEnabled = true;
                            }
                            tmpNibpMode = (Arr_answer[i+5] & 0xC0) >> 6;
                            //lblNibpModeB.Caption := 'modeV : ' +intToStr(tmpNibpMode);
                            if ( (tmpNibpMode < 0) || (tmpNibpMode > 2) )
                                tmpNibpMode = 0;
                            cboxNibpMode.SelectedIndex = tmpNibpMode;
                            if (cboxNibpMode.IsEnabled)
                                cboxNibpMode.IsEnabled = false;
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x43) && (Arr_answer[i + 3] == 0x07) && (Arr_answer[i + 4] == 0x01))
                    {     // NIBP Value
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            pbarNibp.Visibility = System.Windows.Visibility.Hidden;
                            timerSys.IsEnabled = false;
                            if ( (Arr_answer[i+5] & 0x80) == 0x80)
                                lblIrr.Visibility = System.Windows.Visibility.Visible;
                            else
                                lblIrr.Visibility = System.Windows.Visibility.Hidden;
              
                            tmpSysVal = ((Arr_answer[i+5] & 0x0f) << 8) + Arr_answer[i+6];
                            tmpMapVal = Arr_answer[i+7];
                            tmpDiaVal = Arr_answer[i+8];
                            tmpPrVal = Arr_answer[i+9];
                            tmpSysVal2 = (float)Math.Round(((tmpSysVal * 133.0) / 100) / 10, 1);
                            tmpDiaVal2 = (float)Math.Round(((tmpDiaVal * 133.0) / 100) / 10, 1);
                            tmpMapVal2 = (float)Math.Round(((tmpMapVal * 133.0) / 100) / 10, 1);
                            if ((nibpDate + intv2) < date1)
                                listBox_dev.Items.Add(tmpSysVal2.ToString("#.#") + " , " + tmpMapVal2.ToString("#.#") + " , " + tmpDiaVal2.ToString("#.#") + "  " + DateTime.Now.ToString());
                            nibpDate = date1;
                            if (cboxSys.SelectedIndex == 0)
                            {
                                lblSysValue.Content = tmpSysVal.ToString();
                                lblDiaValue.Content = tmpDiaVal.ToString();
                                lblMapValue.Content = tmpMapVal.ToString();
                            }
                            else
                            {
                                lblSysValue.Content = (((tmpSysVal*133.0)/100)/10).ToString("#.#");
                                lblDiaValue.Content = (((tmpDiaVal*133.0)/100)/10).ToString("#.#");
                                lblMapValue.Content = (((tmpMapVal*133.0)/100)/10).ToString("#.#");
                            }
                            lblnPrValue.Content = tmpPrVal.ToString();
                            tmpGradeVal = SysDiaGrade(tmpSysVal, tmpDiaVal);
                            if ((tmpGradeVal > 0) && (tmpGradeVal < 7))
                                lblNibpGrade.Content = tmpGradeVal.ToString() + " - " + nibpGrade[tmpGradeVal];
                            else
                                lblNibpGrade.Content = "";
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x43) && (Arr_answer[i + 3] == 0x03) && (Arr_answer[i + 4] == 0x02))
                    {     // NIBP Error
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            
                            timerSys.IsEnabled = false;
                            tmpSysVal = 0; //Arr_answer[i+5];
                            tmpDiaVal = 0;
                            tmpPrVal = 0;
                            lblSysValue.Content = "--";
                            lblDiaValue.Content = "--";
                            lblMapValue.Content = "";
                            lblnPrValue.Content = "--";
                            lblNibpGrade.Content = "";
                            pbarNibp.Visibility = System.Windows.Visibility.Hidden;
                            lblNibpStatus.Visibility = System.Windows.Visibility.Visible;
                            lblNibpStatus.Content = "";
                            if ( (Arr_answer[i+5] & 0x70) == 0x10 )
                            {
                                tmpErr = Arr_answer[i+5] & 0xf;
                                if ( (tmpErr >= 1) && (tmpErr <= 6) )
                                    lblNibpStatus.Content = "Error ! NIBP_type2[" + tmpErr.ToString()+ "]";//NIBP_type2[tmpErr];
                            }
                            else if ( (Arr_answer[i+5] & 0x70) == 0x20 )
                            {
                                tmpErr = Arr_answer[i+5] & 0xf;
                                if ( (tmpErr >= 1) && (tmpErr <= 15) )
                                    lblNibpStatus.Content = "Error ! NIBP_type3[" + tmpErr.ToString() + "]";//NIBP_type3[tmpErr];
                            }
                            else if ( (Arr_answer[i+5] & 0x70) == 0x0 )
                            {
              
                                if ( (Arr_answer[i+5] & 0x80) == 0x0 )
                                {
                                    if ( (Arr_answer[i+5] >= 0) && (Arr_answer[i+5] <= 14) )
                                        lblNibpStatus.Content = "Error ! NIBP_type0[" + Arr_answer[i+5].ToString() + "]";//NIBP_type0[Arr_answer[i+5]];
                                }
                                else if ( (Arr_answer[i+5] & 0x80) == 0x80 ) 
                                {
                                    tmpErr = Arr_answer[i+5] & 0x7f;
                                    if ( (tmpErr >= 1) && (tmpErr <= 14) ) //6
                                        lblNibpStatus.Content = "Error ! NIBP_type1[" + tmpErr.ToString() + "]"; //NIBP_type1[tmpErr];
                                }
                            }
                            if (lblNibpStatus.Content.ToString().Trim() == "")
                                lblNibpStatus.Content = "NIBP Error !";

                            lblNibpStatus.Content = lblNibpStatus.Content + "   No: " + (Arr_answer[i+5]).ToString();
                            cboxNibpMode.IsEnabled = true;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x40) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x04))
                    {     // //Nibp Calibrate for PC-200
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            edtTestNibp.Text =  "NIBP test Calibrate";
                            lblNibpStatus.Visibility = System.Windows.Visibility.Visible;
                            lblNibpStatus.Content = "NIBP Calibrate, click me to stop.";
                            nibpTest_cnt = 0;
                            timer_nibpTest.IsEnabled = true;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xFF) && (Arr_answer[i + 3] > 2) && (Arr_answer[i + 4] == 0x10))
                    {     // get CustomerID
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            customerId2 = (Arr_answer[i+5] + (Arr_answer[i+6] << 8)).ToString();
                            sbarCustomerId.Content = "Customer ID : " + customerId2;
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xFF) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x03))
                    {     // Equipment inquiry time.  slave向master查询时间
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            listBox_dev.Items.Add("Equipment inquiry time");
                            setDateTime();
                            timer_Datetime.IsEnabled = true;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x30) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x01))
                    {     // ECG start measuring
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            listBox_dev.Items.Add("ECG start measuring " + DateTime.Now.ToString());
                            vPbar.Visibility = System.Windows.Visibility.Hidden;
                            lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                            set12BitEcg(true);
                            if (ECG_startDr == false)
                            {
                                ECG_startDr = true;
                                ECGInit();
                                lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                lblEcgStatus.Content = Ecg_status[0];
                            }
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x30) && (Arr_answer[i + 3] == 0x02) && (Arr_answer[i + 4] == 0x02))
                    {     // ECG stop measuring
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            listBox_dev.Items.Add("ECG stop measuring " + DateTime.Now.ToString());
                            set12BitEcg(true);
                            ECG_startDr = false;
                            ECG_startM = false;
                            lblEcgStatus.Visibility = System.Windows.Visibility.Hidden;
                            lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            lblEcgLeadOff.Visibility = System.Windows.Visibility.Hidden;

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    //
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x31) && (Arr_answer[i + 3] == 0x03) && (Arr_answer[i + 4] == 0x02))
                    {     // H600 ECG working status
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //listBox_dev.Items.Add("31 03 02 ECG working status Arr_answer[i+5](x2) = " + Arr_answer[i + 5].ToString("x2") + " " + DateTime.Now.ToString());
                            
                            lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            if ((Arr_answer[i+5] & 0x40) == 0 )
                            {
                                //lblEcgCheck.Visibility = System.Windows.Visibility.Visible; temp for H600 test
                                ECG_startDr = false;
                                ECG_startM = false;
                                //writeln(tmpfile,' ECG check probe.');
                                listBox_dev.Items.Add("ECG check probe! " + DateTime.Now.ToString());
                            }
                            else
                                lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            if ((Arr_answer[i+5] & 0x80) == 0x80 )
                                lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            else
                            {
                                set12BitEcg(true);
                                ECG_startDr = false;    // 0 idle 待机中
                                ECG_startM = false;
                            }

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x31) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01))
                    {     //心电状态 协议 V2.1  2016-03-04 AA 55 31 05 01
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //listBox_dev.Items.Add("心电状态  AA 55 31 05 01" + DateTime.Now.ToString());
                            
                            lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            if (Arr_answer[i+5] == 0xff)
                            {
                                lblEcgCheck.Visibility = System.Windows.Visibility.Visible; // 模块故障或未接入。
                                ECG_startDr = false;
                                ECG_startM = false;
                            }
                            if (Arr_answer[i+5] == 0x01)
                            {
                                lblEcgCheck.Visibility = System.Windows.Visibility.Hidden; // 1测量中。模块忙或测量正在进行中
                            }
                            else if (Arr_answer[i+5] == 0)
                            {
                                set12BitEcg(true);
                                ECG_startDr = false;    // 0待机中
                                ECG_startM = false;
                            }

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x34) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01))
                    {     // //心电增益 ECG gain
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //listBox_dev.Items.Add("ECG Gain Arr_answer[i+5],6,7 = " + Arr_answer[i + 5].ToString("x2") + " , " + Arr_answer[i + 6].ToString("x2") + " , " + Arr_answer[i + 7].ToString("x2") + " ,  " + DateTime.Now.ToString());
                            ecg_1mv = (Arr_answer[i+5] << 8) + Arr_answer[i+6];
                            ecg_amp = Arr_answer[i+7];
                            //listBox_dev.Items.Add("ECG_1mv : " + ecg_1mv.ToString() + " , " + "ECG_amp : " + ecg_amp.ToString());
                            lblEcg_1mv.Content ="1mv:" + ecg_1mv.ToString();
                            lblEcg_amp.Content = "Amp:" +ecg_amp.ToString();

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    //-
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x32) && (Arr_answer[i + 3] == 0x37) && (Arr_answer[i + 4] == 0x01))
                    {     // ECG Wave
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //listBox_dev.Items.Add("ECG WAVE " + DateTime.Now.ToString());
                            if (ECG_startDr == false)
                            {
                                ECG_startDr = true;
                                ECGInit();
                                lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                lblEcgStatus.Content = Ecg_status[0];
                            }

                            for (k = 1; k <= 25; k++)
                            {
                                if ( (Arr_answer[i+4+k*2] & 0x40) == 0x40)
                                {
                                    prCnt = 0;
                                    image_heart.Visibility = System.Windows.Visibility.Visible;
                                    if (Arr_answer[i+56] > 0)
                                        lblHrValue.Content = Arr_answer[i+56].ToString();
                                }
                            }

                            if ((Arr_answer[i+57] & 0x80) == 0x80)
                                lblEcgLeadOff.Visibility = System.Windows.Visibility.Visible;
                            else
                                lblEcgLeadOff.Visibility = System.Windows.Visibility.Hidden;
                            //+ecgSavedDraw;
              
                            if (Arr_answer[i+5] == 0)
                            {
                                lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                lblEcgStatus.Content = Ecg_status[0];
                                for (k = 1; k <= 25; k++)
                                {
                                    if ( (Arr_answer[i+5+k*2-1] & 0x80) == 0x80 )
                                    {
                                        ecgFileData2.ECGRec[ecg_idx] = ((Arr_answer[i+5+k*2-1] & 0x0F) << 8) + Arr_answer[i+5+k*2];
                                        ecg_max = 4095;
                                    }
                                    else
                                    {
                                        ecgFileData2.ECGRec[ecg_idx] = Arr_answer[i+5+k*2];
                                        ecg_max = 255;
                                    }
                                    ecgFileData2.ECGMaxV = ecg_max;
                                    y_val = cvECG.Height - (ecgFileData2.ECGRec[ecg_idx]*cvECG.Height/ecg_max) ;
                                    imgEcgY[ecg_idx] = y_val/cvECG.Height;
                                    ecgSaveIdx = ecg_idx;
                                    //dY = cvECG.Height - cvECG.Height * ecgFileData2[EcgSt] / ecg_max;
                                    //ImageEcg1.Canvas.LineTo(round(ecg_idx*w_img/1500), y_val );
                                    if (ecg_idx == 0) 
                                        ECGCollection.Clear();
                                    ECGCollection.Add(new System.Windows.Point(ECGCollection.Count*cvECG.Width/4500, y_val));
                                    ecg_idx++;
                                }
                            }
                            //*
                            move_flag = false;
                            if ( (Arr_answer[i+5] > 0) && (ecg_idx < 4500) )
                            {
                                if (ECG_startM == false )
                                {
                                    amp_n = 1;

                                    //lblMaxV.Caption := 'MaxECGv : ' + intToStr(ecg_max);
                                    //lblIdx.Caption := 'idx: ' + intToStr(ecg_idx);
                                    if (ecg_max == 4095)
                                        amp_n = ecg_amp;
                                    lblEcg_amp.Content = "amp:" + amp_n.ToString();
                                    ECG_startDr = true;
                                    ECG_startM = true;
                                    lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                    ECGInit();
                                    ecgFileData2.stTime = DateTime.Now;
                                    listBox_dev.Items.Add("ECG Measuring start at " + ecgFileData2.stTime.ToString());
                                    lblEcgTime.Content = ecgFileData2.stTime.ToString();
                                }
                                lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                lblEcgStatus.Content = Ecg_status[1];
                                if ( (Arr_answer[i+5] > (send_idx+1)) && (send_idx > 0) )
                                {
                                    for (k = 1; k <= 25 * (Arr_answer[i+5] - (send_idx+1)); k++) 
                                    {
                                        ecgFileData2.ECGRec[ecg_idx] = -1;
                                        ecg_idx++;
                                    }
                                    move_flag = true;
                                    listBox_dev.Items.Add("Arr_answer[i+5] = " + Arr_answer[i+5].ToString() + " , move_flag = true , send_idx = " + send_idx.ToString() );
                                }

                                for (k = 1; k <= 25; k++)
                                {
                                    if ( (Arr_answer[i+5+k*2-1] & 0x80) == 0x80 )
                                    {
                                        ecgFileData2.ECGRec[ecg_idx] = ((Arr_answer[i+5+k*2-1] & 0x0F) << 8) + Arr_answer[i+5+k*2];
                                        ecg_max = 4095;
                                    }
                                    else
                                    {
                                        ecgFileData2.ECGRec[ecg_idx] = Arr_answer[i+5+k*2];
                                        ecg_max = 255;
                                    }
                                    ecgFileData2.ECGMaxV = ecg_max;
                                    lblMaxV.Content = "MaxV:" + ecg_max.ToString();

                                    y_val =  (ecgFileData2.ECGRec[ecg_idx]-ecg_max/2) * amp_n + ecg_max/2 ;
                                    if (y_val > ecg_max) y_val = ecg_max;
                                    else if (y_val <0 ) y_val = 0;
                                    y_val = cvECG.Height - y_val * cvECG.Height / ecg_max;
                                    imgEcgY[ecg_idx] = y_val/cvECG.Height;
                                    ecgSaveIdx = ecg_idx;
                                    // y_val := h_img - round(tmpEcgData.ECGRec[ecg_idx]*h_img/ecg_max) ;

                                    //if ( (ecg_idx mod 1500) = 0) or (move_flag = true) then
                                        //ImageEcg1.Canvas.MoveTo(round(ecg_idx*w_img/1500), y_val );
                                    //ImageEcg1.Canvas.LineTo(round(ecg_idx*w_img/1500), y_val );
                                    if (ecg_idx == 0) 
                                        ECGCollection.Clear();
                                    //if (move_flag)
                                        //space;
                                    ECGCollection.Add(new System.Windows.Point(ECGCollection.Count*cvECG.Width/4500, y_val));
                                    ecg_idx++;
                                    move_flag = false;
                                }
                            }
                            else if ( (Arr_answer[i+5] > 0) && (ecg_idx >= 4500) )
                            {
                                lblEcgStatus.Visibility = System.Windows.Visibility.Visible;
                                if (lblEcgStatus.Content.ToString() != Ecg_status[2])
                                    listBox_dev.Items.Add("ECG Measuring end at " + DateTime.Now);
                                lblEcgStatus.Content = Ecg_status[2];
                                
                            }

                            if (Arr_answer[i+5] > (send_idx+1) )
                            {
                                listBox_dev.Items.Add("leak ecg_testCnt = " + ecg_testCnt.ToString() + " , Arr_answer[i+5] = " + Arr_answer[i+5].ToString() + " leak , send_idx = " + send_idx.ToString() );
                                ecg_testCnt = 51;
                            }
                            send_idx = Arr_answer[i+5];

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x33) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01))
                    {     // ECG Result
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            listBox_dev.Items.Add("ECG Result " + DateTime.Now.ToString());
                            //.
                            ECG_startM = false;
                            ECG_startDr = false;
                            //sbtnECgStart.Enabled := true;

                            lblHrValue.Content = Arr_answer[i+7].ToString();
                            if (Arr_answer[i+5] > 16 )
                                Arr_answer[i+5] = 16;
                            ecgFileData2.ResultNo = Arr_answer[i+5];
                            ecgFileData2.HRavg = Arr_answer[i+7];
                            ecgFileData2.ECGMaxV = ecg_max;
                            amp_n = 1;

                            if (ecg_max == 4095)
                                amp_n = ecg_amp;
                            lblEcg_amp.Content = "amp:" + amp_n.ToString();
                            if (ecg_max == 4095)
                            {
                                ecgFileData2.ECGAmpV = amp_n;
                                ecgFileData2.ECG1mvV = ecg_1mv;
                            }
                            else
                            {
                                ecgFileData2.ECGAmpV = 1;
                                ecgFileData2.ECG1mvV = 28.5;
                            }
                            //+AddECGDataToPati;

                            lblMaxV.Content = "Max:" + ecg_max.ToString();

                            if ( (Arr_answer[i+5] >= 0) && (Arr_answer[i+5] <= 16) )
                                lblEcgReValue.Content = Arr_answer[i+5].ToString() + " , " + Vpc_Type_30s[Arr_answer[i+5]];
                            lblHr.Visibility = System.Windows.Visibility.Visible;
                            lblEcgRe.Visibility = System.Windows.Visibility.Visible;
                            //Panel_ECG.Visible := true;
                            checkVer();
                            lblEcgStatus.Visibility = System.Windows.Visibility.Hidden;
                            lblEcgCheck.Visibility = System.Windows.Visibility.Hidden;
                            lblEcgLeadOff.Visibility = System.Windows.Visibility.Hidden;
                            //.
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xE4) && (Arr_answer[i + 3] == 0x02) )
                    {     // GLU device type
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            k = Arr_answer[i+4];
                            if ( (k > 0) && (k < 4) )
                            {
                                cboxGluDevice.SelectedIndex = k;
                                cboxGluDevice_SelectionChangedPrc();
                            }
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ( ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0x73) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01)) ||
                    ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xE2) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x01)) )
                    {     // GLU value
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            
                            glu_unit = (Arr_answer[i+5] & 0x01);
                            if (glu_unit == 0)
                            {
                                tmpGlu0Val = (Arr_answer[i+6] & 0x0f) * 10 + ((Arr_answer[i+7] & 0xf0) >> 4) + (Arr_answer[i+7] & 0x0f)/10.0;
                                tmpGlu1Val = tmpGlu0Val;
                                tmpGlu2Val = (tmpGlu0Val * 18);
                            }
                            else
                            {
                                tmpGlu0Val = (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                                tmpGlu1Val = tmpGlu0Val / 18.0 ;
                                tmpGlu2Val = tmpGlu0Val;
                            }

                            if ( ( Arr_answer[i+5] & 0x30 ) == 0x10 )
                            {
                                tmpBSGradeVal = -1;
                                lblGluGrade.Content = "Low";
                                if (glu_unit == 0)
                                    tmpGlu0Val = 1;
                                else
                                    tmpGlu0Val = 18;
                                tmpGlu1Val = 1;
                                tmpGlu2Val = 18;
                                lblGluValue.Content = "";
                            }
                            else if ( (Arr_answer[i+5] & 0x30) == 0x20 )
                            {
                                tmpBSGradeVal = 1;
                                lblGluGrade.Content = "High";
                                if (glu_unit == 0)
                                    tmpGlu0Val = 28;
                                else
                                    tmpGlu0Val = 504;
                                tmpGlu1Val = 28.0;
                                tmpGlu2Val = 504;
                                lblGluValue.Content = "";
                            }
                            else
                            {
                                tmpBSGradeVal = 0;
                                lblGluGrade.Content = "";
                                lblGluValue.Content = tmpGlu0Val.ToString("#.#");
                            }
                            if ((gluDate + intv2) < date1)
                                listBox_dev.Items.Add("glu_unit:" + glu_unit.ToString() + "  tmpGlu1Val =" + tmpGlu1Val.ToString("#.#") + " ,  tmpGlu2Val =" + tmpGlu2Val.ToString() + "  " + DateTime.Now.ToString());
                            gluDate = date1;
                            cboxGlu_SelectionChanged(null, null);

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xE2) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x02))
                    {     // UA value
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            k = (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                            listBox_dev.Items.Add("UA:" + (k/10.0).ToString() + " mg/dL");
                            lblUaValue.Content = (k/10.0).ToString();
                            ua_unit = (Arr_answer[i+5] & 0x01);
                            tmpUa0Val = ((Arr_answer[i+6] << 8) + Arr_answer[i+7])/10.0;
                            tmpUa1Val = (float)Math.Round(tmpUa0Val / 16.81, 2);
                            tmpUa2Val = tmpUa0Val;

                            if ((uaDate + intv2) < date1)
                                listBox_dev.Items.Add("ua_unit:" + ua_unit.ToString() + "  tmpUa1Val =" + tmpUa1Val.ToString("0.##") + " ,  tmpUa2Val =" + tmpUa2Val.ToString("0.##") + "  " + DateTime.Now.ToString());
                            uaDate = date1;
                            if (ua_unit < 0) ua_unit = 0;
                            if (ua_unit > 1) ua_unit = 1;
                            cboxUa_SelectionChanged(null, null);

                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i + 1] == 0x55) && (Arr_answer[i + 2] == 0xE2) && (Arr_answer[i + 3] == 0x05) && (Arr_answer[i + 4] == 0x03))
                    {     // CHOL value
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            if (comOpen == false)
                            {
                                comOpen = true;
                                openInit();
                            }
                            stopCnt = 0;
                            //-
                            k = (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                            listBox_dev.Items.Add("CHOL:" + k.ToString() + " mg/dL");
                            lblCholValue.Content = k.ToString();
                            chol_unit = (Arr_answer[i+5] & 0x01);
                            tmpChol0Val = (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                            tmpChol1Val = (float)Math.Round(tmpChol0Val / 38.66, 2);
                            tmpChol2Val = tmpChol0Val;
                            if ((cholDate + intv2) < date1)
                                listBox_dev.Items.Add("chol_unit:" + chol_unit.ToString() + "  tmpChol1Val =" + tmpChol1Val.ToString("0.##") + " ,  tmpChol2Val =" + tmpChol2Val.ToString("0.##") + "  " + DateTime.Now.ToString());
                            cholDate = date1;
                            cboxChol_SelectionChanged(null, null);
                            
                            j = i + Arr_answer[i + 3] + 4;
                        }//if (crcVal == Arr_answer[i+Arr_answer[i+3]+3])
                    }
                    //----
                    //*
          /*else
          if ((i+6) < tmp_t) and (Arr_answer[i] = $AA) and (Arr_answer[i+1] = $55) and (Arr_answer[i+2] = $70) and (Arr_answer[i+3] = $03) and (Arr_answer[i+4]=$01)  then
          begin
            crcVal := 0;
            for k:=0 to 5 do
              crcVal := crcNew_table[crcVal xor Arr_answer[i+k]];
           // writeln(tmpfile,' Temp C ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+8],2)) ;
            if (crcVal = Arr_answer[i+6]) then
            begin
              j := i+7;
              if ((Arr_answer[i+5] and $40) = $40) then
              begin
                txtTemp.Caption := '--';
                tempStart := false;
                lblTemp.Visible := true;
                tmpTempVal := 0;
                tmpTempVal0 := 0;
                tmpTempInt0 := 0;
                panel_tempOver.Visible := false;
                if timerTemp.enabled = false then
                  timerTemp.Enabled := true;
                //writeln(tmpfile,' TempBegin1 C ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+6],2)) ;
              end;
           //   break;
            end;
          end*/
                    else
                    //if (((i+8) < tmp_t) && (Arr_answer[i] == 0xAA) && (Arr_answer[i+1] == 0x55) && (Arr_answer[i+2] == 0x72) && (Arr_answer[i+3] == 0x05) && (Arr_answer[i+4]==0x01))
                    if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i+1] == 0x55) && (Arr_answer[i+2] == 0x72) && (Arr_answer[i+3] == 0x05) && (Arr_answer[i+4]==0x01))
                    {
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        // writeln(tmpfile,' Temp C ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+8],2)) ;
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            j = i + Arr_answer[i + 3] + 4;
                            askTempMode_cnt = 0;
                            timer_askTempMode.IsEnabled = true;
                            //  lblTempValue.Content = "--";
                            if  ( (Arr_answer[i+5] & 0x10) == 0)  // and ((Arr_answer[i+5] and $60) <> $60) ) then
                            {
                                tmpTempInt0 = 3000 + (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                                tmpTempInt = 3000 + (Arr_answer[i+6] << 8) + Arr_answer[i+7];
                                tmpTempVal = Math.Floor(tmpTempInt / 100.0); //trunc(tmpTempInt/100);
                                k = (int)Math.Floor((tmpTempInt % 100) / 10.0);
                                //k := (tmpTempInt mod 100) div 10 ;
                                tmpTempVal = tmpTempVal + k/10.0 ;
                                if ((Arr_answer[i+5] & 0x60) == 0x20)
                                {
                                    //tempStart := true;
                                    if (cboTemp.SelectedIndex == 0)
                                        lblTempValue.Content = tmpTempVal.ToString();
                                    else
                                    {
                                        if (cboTempMode.IsVisible == false)
                                        {
                                            v2 = Math.Floor((tmpTempInt0 * 9) / 5.0);
                                            lblTempValue.Content = Math.Floor((v2 +3200) / 100.0).ToString(); //intToStr( ((tmpTempInt0 * 9 div 5) +3200) div 100)
                                        }
                                        else
                                        {
                                            v2 = (tmpTempInt0 * 9 / 5 +3200);
                                            lblTempValue.Content = (Math.Floor(v2 / 10 ) / 10).ToString(); //floatToStr(trunc( (tmpTempInt0 * 9 / 5 +3200) / 10 ) / 10);
                                        }
                                    }
                                }
                                else if ((Arr_answer[i+5] & 0x60) == 0x60)
                                {
                                    //tempStart = false;
                                    //timerTemp.Enabled := false;
                                    //lblTemp.Visible := true;
                                    lblTempValue.Content = "--";
                                    //lblTempWarnAll.Caption := lblTempOver.Caption ;
                                    lblTempWarn.Content = "Overtime";
                                    lblTempWarn.Visibility = System.Windows.Visibility.Visible;
                                }
                            }
                            else
                            {
                                tmpTempVal = 0;
                                tmpTempVal0 = 0;
                                tmpTempInt0 = 0;
                                lblTempValue.Content = "--";
                            }
                    
                            if ( ((Arr_answer[i+5] & 0x10) == 0) && ((Arr_answer[i+5] & 0x60) == 0x40) )
                            {
                                lblTempWarn.Visibility = System.Windows.Visibility.Hidden;
                                //tempStart = false;
                                //timerTemp.Enabled := false;
                                //lblTemp.Visible := true;
                                //writeln(tmpfile,' Temp C ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+8],2)) ;
                                //   tmpTempVal := 30 +  ( (Arr_answer[i+6] shl 8) + Arr_answer[i+7])/100;
                                if ( (tmpTempVal >= 32) && ( tmpTempVal <= 43.09 ) )  // 43, 42.9
                                {
                                    if (cboTemp.SelectedIndex == 0)
                                        lblTempValue.Content = tmpTempVal.ToString();
                                    
                                    else
                                    {
                                        if (cboTempMode.IsVisible == false)
                                        {
                                            v2 = Math.Floor((tmpTempInt0 * 9) / 5.0);
                                            lblTempValue.Content = Math.Floor((v2 +3200) / 100.0).ToString(); //intToStr( ((tmpTempInt0 * 9 div 5) +3200) div 100)
                                        }
                                        else
                                        {
                                            v2 = (tmpTempInt0 * 9 / 5 +3200);
                                            lblTempValue.Content = (Math.Floor(v2 / 10 ) / 10).ToString();//floatToStr(trunc( (tmpTempInt0 * 9 / 5 +3200) / 10 ) / 10);
                                        }
                                    }
                                }
                                else                       
                                {
                                    if (tmpTempVal < 32)
                                    {
                                        //lblTempWarnAll.Caption := lblTempLow.Caption;
                                        lblTempWarn.Content = "Low";
                                        lblTempWarn.Visibility = System.Windows.Visibility.Visible;
                                    }
                                    if (tmpTempVal > 43.09)
                                    {
                                        //lblTempWarnAll.Caption := lblTempHigh.Caption;
                                        lblTempWarn.Content = "High";
                                        lblTempWarn.Visibility = System.Windows.Visibility.Visible;
                                    }
                                    lblTempValue.Content = "--";
                                }
                                //lblTempV.Caption := intToHex(Arr_answer[i+6],2) + '-' + intToHex(Arr_answer[i+7],2);
                                if ( ((Arr_answer[i+6] << 8) + Arr_answer[i+7]) >= 0x0520 )
                                {
                                    if ( (Arr_answer[i+6] & 0xf0) == 0xf0 )
                                        lblTempWarn.Content = "Low";
                                    else
                                        lblTempWarn.Content = "High !!!";
                                    lblTempWarn.Visibility = System.Windows.Visibility.Visible;
                                }
                            }
                        }
                    }
                    else if ((tmp_t > (Arr_answer[i + 3] + 3 + i)) && (Arr_answer[i] == 0xAA) && (Arr_answer[i+1] == 0x55) && (Arr_answer[i+2] == 0x72) && (Arr_answer[i+3] == 0x03) && (Arr_answer[i+4] == 0x03))
                    {       // 奥极体温计模式、温度单位。
                        crcVal = 0;
                        for (k = 0; k <= (Arr_answer[i + 3] + 2); k++)
                            crcVal = crcNew_table[crcVal ^ Arr_answer[i + k]];
                        // writeln(tmpfile,' Temp C ---- crcval = '+ intToHex(crcVal,2) + ' , crc= ' + intToHex(Arr_answer[i+8],2)) ;
                        if (crcVal == Arr_answer[i + Arr_answer[i + 3] + 3])
                        {
                            j = i + Arr_answer[i + 3] + 4;
                            k = Arr_answer[i+5];
                            cboTempMode.Visibility = System.Windows.Visibility.Visible;
                            if ((k == 0x11) || (k == 0x12))
                            {
                                cboTempMode.SelectedIndex = 1;
                                if (cboTemp.SelectedIndex != (k - 0x11))
                                {
                                    cboTemp.SelectedIndex = (k - 0x11);
                                    cboTemp_SelectionChanged(null,null);
                                }
                            }
                            else if ((k == 0x21) || (k == 0x22))
                            {
                                cboTempMode.SelectedIndex = 2;
                                if (cboTemp.SelectedIndex != (k - 0x21))
                                {
                                    cboTemp.SelectedIndex = (k - 0x21);
                                    cboTemp_SelectionChanged(null,null);
                                }
                            }
                            timer_askTempMode.IsEnabled = false;
                        }
                    }
          //++
                    //----
                    //+++
                    if (j != -1)
                    {
                        j0 = j;
                        i = j - 1;
                        if (i >= tmp_t - 5) break;
                        j = -1;
                    }
                }//while (i < (tmp_t - 5))
                //--
                j = j0;
                if (j > 0)
                {
                    //lblCheckProbe.Visible := false;
                    //stopCnt := 0;
                    for (k = 0; k <= (tmp_t - j - 1); k++)
                        Arr_answer[k] = Arr_answer[j + k];
                    for (k = (tmp_t - j); k <= (tmp_t - 1); k++)
                        Arr_answer[k] = 0;

                    //move(Arr_answer[j], Arr_answer[0], tmp_t-j);
                    //fillchar(Arr_answer[tmp_t - j], j, #0);
                    tmp_t = tmp_t - j;
                    //writeln(tmpfile, 'move j = ' +  intToStr(j));
                }
                readCnt = tmp_t;
                if (restartFlag)
                {
                    startSend = 0;
                    timer_start.IsEnabled = true;
                }
            }//if (tmp_t >5) 
        }
        //---
        public void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort1.IsOpen & _keepReading)
            {
                int i = 0;
                byte[] readBuffer = new byte[serialPort1.ReadBufferSize + 1];
                try
                {
                    int count = serialPort1.Read(readBuffer, 0, serialPort1.ReadBufferSize);
                    //textBox1.Text = "count:" ;
                    if ((readCnt + count) > Arr_answer.Length)
                    {
                        readCnt = 0;
                        for (i = 0; i < Arr_answer.Length; i++)
                        {
                            Arr_answer[i] = 0;
                        }
                        if (count > Arr_answer.Length) return;
                    }
                    String SerialIn = System.Text.Encoding.ASCII.GetString(readBuffer, 0, count);
                    if (count != 0)
                    {    //byteToHexStr(readBuffer);  
                        //ThreadFunction(byteToHexStr(readBuffer,count));  

                        for (i = 0; i < count; i++)
                        {
                            Arr_answer[readCnt + i] = readBuffer[i];
                        }
                        readCnt = readCnt + count;
                    }
                }
                catch (Exception){ }
            }
        }
        //*********************** Initialization **************************
        public MainWindow()
        {
            InitializeComponent();
            string[] portList = System.IO.Ports.SerialPort.GetPortNames();
            comboBox1.Items.Clear();
            for (int i = 0; i < portList.Length; ++i)
            {
                string name = portList[i];
                comboBox1.Items.Add(name);
            }
            SpO2Collection.Add(new System.Windows.Point(0, 0));
            SpO2Line.Stroke = new SolidColorBrush(Colors.Red);
            SpO2Line.StrokeThickness = 1;
            SpO2Line.Points = SpO2Collection;
            SpO2Wave.Children.Add(SpO2Line);
            
            RespCollection.Add(new System.Windows.Point(0, 0));
            RespLine.Stroke = new SolidColorBrush(Colors.Red);
            RespLine.StrokeThickness = 1;
            RespLine.Points = RespCollection;
            cvFlow.Children.Add(RespLine);
            
            SnoreCollection.Add(new System.Windows.Point(0, 0));
            SnoreLine.Stroke = new SolidColorBrush(Colors.Green);
            SnoreLine.StrokeThickness = 1;
            SnoreLine.Points = SnoreCollection;
            cvSnore.Children.Add(SnoreLine);

            ECGCollection.Add(new System.Windows.Point(0, 0));
            ECGLine.Stroke = new SolidColorBrush(Colors.Green);
            ECGLine.StrokeThickness = 1;
            ECGLine.Points = ECGCollection;
            cvECG.Children.Add(ECGLine);
            cboDevice.SelectedIndex = 0;
            serialPort1.BaudRate = 115200;//115200:PC-300SNT,PC-100,PC-200;// 38400: H_600
            //com.PortName = "COM157";
            serialPort1.DataBits = 8;
            serialPort1.Close();
            //com.Open();//打开串口
            //3...发送数据
            //Byte[] TxData = { 1, 2, 3, 4, 5, 6, 7, 8 };
            //com.Write(TxData, 0, 8);
            //OnDataReceived(object sender, SerialDataReceivedEventArgs e);
            serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(OnDataReceived);
            //com.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(interfaceUpdateHandle);
            readCnt = 0;
            string s0 = byteToHexStr(Arr_answer, readCnt);
            //MessageBox.Show("s0 : " + s0, "Notice", MessageBoxButton.OK);
            
            timer_display.Dispatcher.Thread.IsBackground = true;
            timer_battery.Dispatcher.Thread.IsBackground = true;
            timer_start.Dispatcher.Thread.IsBackground = true;

            timer_pulse.Dispatcher.Thread.IsBackground = true;
            timer3.Dispatcher.Thread.IsBackground = true;
            timer_open.Dispatcher.Thread.IsBackground = true;
            timer_getSta.Dispatcher.Thread.IsBackground = true;

            timer4.Dispatcher.Thread.IsBackground = true;
            timer_id.Dispatcher.Thread.IsBackground = true;
            timer_NibpMode.Dispatcher.Thread.IsBackground = true;
            timer_nibpTest.Dispatcher.Thread.IsBackground = true;
            timerSys.Dispatcher.Thread.IsBackground = true;
            timer_Datetime.Dispatcher.Thread.IsBackground = true;
            timer_ecg.Dispatcher.Thread.IsBackground = true;
            timer_chkGluDev.Dispatcher.Thread.IsBackground = true;
            timer_askTempMode.Dispatcher.Thread.IsBackground = true;

            timer_display.Tick += new EventHandler(timer_display_Tick);
            timer_battery.Tick += new EventHandler(timer_battery_Tick);
            timer_start.Tick += new EventHandler(timer_start_Tick);

            timer_pulse.Tick += new EventHandler(timer_pulse_Tick);
            timer3.Tick += new EventHandler(timer3_Tick);
            timer_open.Tick += new EventHandler(timer_open_Tick);
            timer_getSta.Tick += new EventHandler(timer_getSta_Tick);
            timer4.Tick += new EventHandler(timer4_Tick);
            timer_id.Tick += new EventHandler(timer_id_Tick);
            timer_fsnore.Tick += new EventHandler(timer_fsnore_Tick);
            timer_NibpMode.Tick += new EventHandler(timer_NibpMode_Tick);
            timer_nibpTest.Tick += new EventHandler(timer_nibpTest_Tick);
            timerSys.Tick += new EventHandler(timerSys_Tick);
            timer_Datetime.Tick += new EventHandler(timer_Datetime_Tick);
            timer_ecg.Tick += new EventHandler(timer_ecg_Tick);
            timer_chkGluDev.Tick += new EventHandler(timer_chkGluDev_Tick);
            timer_askTempMode.Tick +=new EventHandler(timer_askTempMode_Tick);

            timer_display.IsEnabled = false;
            timer_battery.IsEnabled = false;
            timer_start.IsEnabled = false;
            timer_pulse.IsEnabled = false;
            timer_open.IsEnabled = false;
            
            timer3.IsEnabled = false;
            timer_id.IsEnabled = false;
            timer_NibpMode.IsEnabled = false;
            timer_nibpTest.IsEnabled = false;
            timerSys.IsEnabled = false;
            timer_Datetime.IsEnabled = false;
            timer_ecg.IsEnabled = false;
            timer_chkGluDev.IsEnabled = false;

            stopCnt = 0;
            timer4.IsEnabled = false;
            lblCheckProbe.Visibility = System.Windows.Visibility.Hidden;
            image_heart.Visibility = System.Windows.Visibility.Hidden;
            ECG_startDr = false;
            sbarCustomerId.Content = "";
            lblNibpMode_cnt.Content = "";
            cboxNibpMode.Visibility = System.Windows.Visibility.Visible;
            lblIrr.Visibility = System.Windows.Visibility.Hidden;
            cboxNibpMode.SelectedIndex = 0;
            cboxSys.SelectedIndex = 0;
            lblSysValue.Content = "--";
            lblDiaValue.Content = "--";
            lblMapValue.Content = "--";
            lblnPrValue.Content = "--";
            lblNibpGrade.Content = "";
            nibpDate = DateTime.Now;
            gluDate = DateTime.Now;
            uaDate = gluDate;
            cholDate = gluDate;

            ecgFileData2.ECGRec = new int[4500];
            curDir = Directory.GetCurrentDirectory();
            listBox_dev.Items.Add("curDir =" + curDir);
            tmpDir = curDir + "\\file";
            if (!(Directory.Exists(tmpDir)))
                Directory.CreateDirectory(tmpDir);
            listBox_dev.Items.Add("tmpDir =" + tmpDir);
            cboxGlu.SelectedIndex = 0;
            lblCharge.Content = "";
            btnDelGluData.Visibility = System.Windows.Visibility.Hidden;
            productID = "";
            cboTemp.SelectedIndex = 0;
            lblTempValue.Content = "--";
        }

        void  timer_askTempMode_Tick(object sender, EventArgs e)
        {
 	        //throw new NotImplementedException();
            if (serialPort1.IsOpen && comOpen) 
            {
                try
                {
                    serialPort1.Write(TxData_askTempMode, 0, TxData_askTempMode.Length);  
                    askTempMode_cnt++;
                    if (askTempMode_cnt > 2)
                        timer_askTempMode.IsEnabled = false;
                }
                catch (Exception)
                {
                    comOpen = false;
                    timer_askTempMode.IsEnabled = false;
                }
            }
        }

        void timer_getSta_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            tmrCnt_getSta++;
            lbl_tmrCnt_getSta.Content = "tmrCnt_getSta : " + tmrCnt_getSta.ToString();
            if (tmrCnt_getSta > 10000)
            {
                tmrCnt_getSta = 10000;
                lbl_tmrCnt_getSta.Content = "tmrCnt_getSta : " + tmrCnt_getSta.ToString() + " + ";
            }
           
            //btnGetSta_Click(sender, e);
            TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 50);
            try
            {
                if ((serialPort1.IsOpen) && (devAddr != ""))
                {
                    serialPort1.WriteLine("SPP:connect " + devAddr + " public \r\n\0");
                    Thread.Sleep(waitTime);
                }
                if (serialPort1.IsOpen)
                    serialPort1.WriteLine("SPP:setConnInt 10 20 0 200 \r\n\0");
            }
            catch (Exception)
            {
                comOpen = false;
                timer_getSta.IsEnabled = false;
            }
        }

        void timer_fsnore0_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            int i, x2, nLen;
            double dY;
            //dateF : TdateTime;
            if (showFlow == false) return;
            if (rewFlow)
            {
                if (flowSt + 5 > flowIdx)
                {
                    if (flowSt + 5 > drawCnt)
                    {
                        flowSt = 0;
                        rewFlow = false;
                        //rew2 := -1;
                    }
                }

            }
            else if (flowSt + 5 > flowIdx)   //15
                return;

            if (cvFlow.Visibility == System.Windows.Visibility.Hidden)
            {
                cvFlow.Visibility = System.Windows.Visibility.Visible;
                cvSnore.Visibility = System.Windows.Visibility.Visible;
            }

            if (flowSt > drawCnt)
            {
                flowSt = 0;
                //rewFlow := false;
            }

            nLen = (int)cvFlow.Width; //1200
            if (flowEnd + 40 < flowSt)   //15
            {
                //chart_flow.Series[0].Points.AddXY((flowEnd % 1200), 0);
                //chart_snore.Series[0].Points.AddXY((flowEnd % 1200), 0);
                //ss_flow.AddNullXY(flowEnd % 1200, 0,'');
                //ss_snore.AddNullXY(flowEnd % 1200 , 0,'');
                //RespCollection.Add(new System.Windows.Point(RespCollection.Count, 0));
                RespCollection.Add(new System.Windows.Point((flowEnd % nLen), 0));
                SnoreCollection.Add(new System.Windows.Point((flowEnd % nLen), 0));
            }
            for (i = 0; i <= 4; i++)
            {
                x2 = flowSt % nLen;
                if (x2 == 0)
                {
                    RespCollection.Clear();
                    SnoreCollection.Clear();
                }
                else
                {
                    //chart_flow.Series[0].Points.AddXY(x2, flowResp[flowSt]);
                    //chart_snore.Series[0].Points.AddXY(x2, flowSnore[flowSt]);
                    dY = cvFlow.Height - cvFlow.Height * flowResp[flowSt] / 4096;
                    RespCollection.Add(new System.Windows.Point(RespCollection.Count, dY));
                    dY = cvSnore.Height - cvSnore.Height * flowSnore[flowSt] / 2048;
                    SnoreCollection.Add(new System.Windows.Point(SnoreCollection.Count, dY));
                }
                flowSt = flowSt + 1;
                if (flowSt > drawCnt)
                {
                    flowSt = 0;
                    //rewFlow := false;
                }
            }
            flowEnd = flowSt;
        }
        //--
        void timer_fsnore_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            int i, x2, nLen, nStep;
            double dY;
            //dateF : TdateTime;
            nStep = 1;
            if (showFlow == false) return;
            if (rewFlow)
            {
                if (flowSt + nStep > flowIdx)
                {
                    if (flowSt + nStep > drawCnt)
                    {
                        flowSt = 0;
                        rewFlow = false;
                        //rew2 := -1;
                    }
                }

            }
            else if (flowSt + nStep > flowIdx)   //15
                return;

            if (cvFlow.Visibility == System.Windows.Visibility.Hidden)
            {
                cvFlow.Visibility = System.Windows.Visibility.Visible;
                cvSnore.Visibility = System.Windows.Visibility.Visible;
            }

            if (flowSt > drawCnt)
            {
                flowSt = 0;
                //rewFlow := false;
            }

            nLen = (int)cvFlow.Width; //1200
            if (flowEnd + nStep+1 < flowSt)   //15 //40
            {
                //chart_flow.Series[0].Points.AddXY((flowEnd % 1200), 0);
                //chart_snore.Series[0].Points.AddXY((flowEnd % 1200), 0);
                //ss_flow.AddNullXY(flowEnd % 1200, 0,'');
                //ss_snore.AddNullXY(flowEnd % 1200 , 0,'');
                //RespCollection.Add(new System.Windows.Point(RespCollection.Count, 0));
                RespCollection.Add(new System.Windows.Point((flowEnd % nLen), 0));
                SnoreCollection.Add(new System.Windows.Point((flowEnd % nLen), 0));
            }
            for (i = 0; i <= nStep-1; i++)
            {
                x2 = flowSt % nLen;
                if (x2 == 0)
                {
                    RespCollection.Clear();
                    SnoreCollection.Clear();
                }
                else
                {
                    //chart_flow.Series[0].Points.AddXY(x2, flowResp[flowSt]);
                    //chart_snore.Series[0].Points.AddXY(x2, flowSnore[flowSt]);
                    dY = cvFlow.Height - cvFlow.Height * flowResp[flowSt] / 100;
                    RespCollection.Add(new System.Windows.Point(RespCollection.Count, dY));
                    dY = cvSnore.Height - cvSnore.Height * flowSnore[flowSt] / 256;
                    SnoreCollection.Add(new System.Windows.Point(SnoreCollection.Count, dY));
                }
                flowSt = flowSt + 1;
                if (flowSt > drawCnt)
                {
                    flowSt = 0;
                    //rewFlow := false;
                }
            }
            flowEnd = flowSt;
        }
        //--
        void timer_ecg_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            int i, x2, nLen, nStep;
            double dY;
            //dateF : TdateTime;
            nStep = 25;
            if (showEcg == false) return;
            if (rewEcg)
            {
                if (EcgSt + nStep > ecg_idx)
                {
                    if (EcgSt + nStep > ecgCnt)
                    {
                        EcgSt = 0;
                        rewEcg = false;
                        //rew2 := -1;
                    }
                }

            }
            else if (EcgSt + nStep > ecg_idx)   //15
                return;

            if (cvECG.Visibility == System.Windows.Visibility.Hidden)
                cvECG.Visibility = System.Windows.Visibility.Visible;

            if (EcgSt > ecgCnt)
            {
                EcgSt = 0;
                //rewFlow := false;
            }

            //nLen = (int)cvECG.Width; //1200
            nLen = 4500;
            if (EcgEnd + nStep+1 < EcgSt)   //15 //40
                ECGCollection.Add(new System.Windows.Point((EcgEnd % nLen), 0));
            for (i = 0; i <= nStep-1; i++)
            {
                x2 = EcgSt % nLen;
                if (x2 == 0)
                    ECGCollection.Clear();
                else
                {
                    dY = cvECG.Height - cvECG.Height * ecgFileData2.ECGRec[EcgSt] / ecg_max;
                    //ImageEcg3.Canvas.LineTo(round((ecg_idx-3000)*w_img/1500), y_val );
                    ECGCollection.Add(new System.Windows.Point(ECGCollection.Count*cvECG.Width/4500, dY));

                    //dY = cvSnore.Height - cvSnore.Height * flowSnore[flowSt] / 256;
                    //SnoreCollection.Add(new System.Windows.Point(SnoreCollection.Count, dY));
                }
                EcgSt = EcgSt + 1;
                if (EcgSt > ecgCnt)
                {
                    EcgSt = 0;
                    //rewEcg := false;
                }
            }
            EcgEnd = EcgSt;
        }

        void timer_id_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            idCnt++;
            TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 30);
            if (idCnt > 50) timer_id.IsEnabled = false;
            try
            {
                serialPort1.Write(TxData_verH600, 0, TxData_verH600.Length);
                Thread.Sleep(waitTime);
                serialPort1.Write(TxData_id, 0, TxData_id.Length);
                Thread.Sleep(waitTime);
                serialPort1.Write(TxData_ver1, 0, TxData_ver1.Length);
                Thread.Sleep(waitTime);
                serialPort1.Write(TxData_ver2, 0, TxData_ver2.Length);
                if (verSendFF == false)
                {
                    Thread.Sleep(waitTime);
                    serialPort1.Write(TxData_ver0, 0, TxData_ver0.Length);
                }
                Thread.Sleep(waitTime);
                serialPort1.Write(TxData_CustomerId, 0, TxData_CustomerId.Length);
            }
            catch (Exception) 
            {
                timer_id.IsEnabled = false;
                comOpen = false;
            }
        }

        void timer4_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            stopCnt++;
            lblStopCnt.Content = "stopCnt : " + stopCnt.ToString();
            if (stopCnt > 3)
            {
                showPulse = false;
                showFlow = false;
                verSendFF = false;
                stopCnt = 0;
                comOpen = false;
                lblTempWarn.Visibility = System.Windows.Visibility.Hidden;
                lblCheckProbe.Visibility = System.Windows.Visibility.Visible;
                timer_open.IsEnabled = true;
            }
            this.Show();
        }

        void timer_NibpMode_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            NibpMode_cnt++;
            //lblNibpMode_cnt.Content = NibpMode_cnt.ToString();
            if (NibpMode_cnt > 10) timer_NibpMode.IsEnabled = false;
            if (serialPort1.IsOpen && comOpen)
            {
                try
                {
                    serialPort1.Write(TxData_chkNibpMode, 0, TxData_chkNibpMode.Length);
                }
                catch (Exception)
                {
                    timer_NibpMode.IsEnabled = false;
                }
            }
        }
        
        void timer_nibpTest_Tick(object sender, EventArgs e)
        {
            nibpTest_cnt++;
            listBox_dev.Items.Add("NIBP Calibrate nibpTest_cnt = " + nibpTest_cnt.ToString() + "  " + DateTime.Now.ToString());
            TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 300);
            if (nibpTest_cnt > 2)
            {
                btnCalibrate_Click(null, null);
                Thread.Sleep(waitTime);
                btnCalibrate_Click(null, null);
            }
        }

        void timerSys_Tick(object sender, EventArgs e)
        {
            lblnPrValue.Content = "";
            timerSys.IsEnabled = false;
        }

        void setDateTime()
        {
            int i, k;
            byte crcVal;
            int tmp3, tmp4;
            DateTime date1;
            string str;
            byte[] txData = new byte[40];

            for (k = 0; k < 2; k++)
            {
                txData[0] = 0xAA;
                txData[1] = 0x55;
                txData[2] = 0xFF;
                if (k == 0)
                    txData[3] = 0x08;     
                else
                    txData[3] = 0x09;     //0x09 is for old version
                txData[4] = 0x03;
                date1 = DateTime.Now;
                tmp3 = date1.Year;
                str = (tmp3.ToString()).Substring(2, 2);
                tmp4 = int.Parse(str);
                txData[5] = (byte)tmp4;
                txData[6] = (byte)date1.Month;
                txData[7] = (byte)date1.Day;
                txData[8] = (byte)date1.Hour;
                txData[9] = (byte)date1.Minute;
                txData[10] = (byte)date1.Second;
                crcVal = 0;
                for (i = 0; i <= 10; i++)
                    crcVal = crcNew_table[crcVal ^ txData[i]];
                txData[11] = crcVal;
                str = "";
                for (i = 0; i <= 11; i++)
                    str = str + txData[i].ToString() + " , ";
                try
                {
                    serialPort1.Write(txData, 0, 12);
                }
                catch (Exception) { };
                if (k == 0)
                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, 40));
                listBox_dev.Items.Add("set time : " + str + " now : " + DateTime.Now.ToString());
            }
        }

        void timer_Datetime_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            if (serialPort1.IsOpen && comOpen)
            {
                setDateTime();
            }
            timer_Datetime.IsEnabled = false;
        }

        private void ThreadComSafe()
        {
            try
            {
                serialPort1.Close();
                TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 100);
                Thread.Sleep(waitTime);
                serialPort1.Open();
                thr_cnt = 1;
                serialPort1.Close();
            }
            catch (Exception)
            {
                thr_cnt = 2;
            }
            /*try
            {
                serialPort1.Close();
            }
            catch (Exception) { }*/
        }

        void timer_open_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            if (comOpen)
            {
                timer_open.IsEnabled = false;
                return;
            }
            lblThr_cnt.Content = "thr : " + thr_cnt.ToString();
            //Thread comThread = null;
            if ((thr_cnt == 0) && (devAddr == ""))
            {
                comThread = new Thread(new ThreadStart(this.ThreadComSafe));
                comThread.Start();
                lblThr_cnt.Content = lblThr_cnt.Content + " Start";
                return;
            }
            lblThr_cnt.Content = lblThr_cnt.Content + " , " + thr_cnt.ToString();
            if (thr_cnt == 2)
            {
                thr_cnt = 0;
                return;
            }
            thr_cnt = 0;
            try
            {
                //throw new NotImplementedException();
                timer_getSta.IsEnabled = false;
                TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 70);
                serialPort1.Close();
                Thread.Sleep(waitTime);
                serialPort1.Open();
                Thread.Sleep(waitTime);
                serialPort1.WriteLine("SPP:disconnect \r\n\0");
                Thread.Sleep(waitTime);
                serialPort1.WriteLine(bdScanOff);
                Thread.Sleep(waitTime);
                serialPort1.WriteLine(bdScanOn);
                Thread.Sleep(waitTime);

                if (devAddr != "")
                {
                    serialPort1.WriteLine("SPP:connect " + devAddr + " public \r\n\0");
                    Thread.Sleep(waitTime);
                    serialPort1.WriteLine("SPP:connect " + devAddr + " public \r\n\0");
                    Thread.Sleep(waitTime);
                }
                serialPort1.WriteLine("SPP:setConnInt 10 20 0 200 \r\n\0");
                tmrCnt_getSta = 0;
                timer_getSta.IsEnabled = true;
                idCnt = 0;
                timer_id.IsEnabled = true;
                //--
                /*TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 70);
                serialPort1.Close();
                Thread.Sleep(waitTime);
                serialPort1.Open();
                idCnt = 0;
                timer_id.IsEnabled = true;*/
            }
            catch (Exception) 
            {
                comOpen = false;
                thr_cnt = 0;
                timer_open.IsEnabled = false;
                lblCheckProbe.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void timer3_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            int i;
            i = prCnt * timer3.Interval.Milliseconds;
            if (i >= interval2)
                image_heart.Visibility = System.Windows.Visibility.Hidden;
            //lblSProbe.Visibility = System.Windows.Visibility.Hidden;
            else
                image_heart.Visibility = System.Windows.Visibility.Visible;
            //lblSProbe.Visibility = System.Windows.Visibility.Visible;
            prCnt = prCnt + 1;
            if (prCnt > 20000) prCnt = 20000;
        }

        void timer_pulse_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            int i, k, v2, rew2, x2, x0, y0, nLen;
            double dY;
            if (showPulse == false)
                return;
            rew2 = 0;
            if (rewPulse)
            {
                if (pulseSt + 5 > pulseIdx)
                {
                    if (pulseSt + 5 > drawCnt)
                    {
                        pulseSt = 0;
                        rewPulse = false;
                        rew2 = -1;
                    }
                }
            }
            else if (pulseSt + 5 > pulseIdx)  //15
                return;
            if (pulseSt > drawCnt)
            {
                pulseSt = 0;
                //rewPulse = false;
            }

            nLen = (int)SpO2Wave.Width; //600;
            if (pulseEnd + 40 < pulseSt)    //40
            {
                ///chart_wave.Series[0].Points.AddXY((pulseEnd % 600), 0);
                //ss_Spo2Wave.AddNullXY(pulseEnd mod 600, 0,'');
                SpO2Collection.Add(new System.Windows.Point((pulseEnd % nLen), 0));
            }
            for (k = 0; k <= 4; k++)
            {
                v2 = (ArrayPulseN[pulseSt] & 0x7f);
                x2 = pulseSt % nLen; //600
                vPbar.Value = v2; // 128 - v2;
                if ((ArrayPulseN[pulseSt] & 0x80) == 0x80)
                {
                    prCnt = 0;
                    image_heart.Visibility = System.Windows.Visibility.Visible; //image2.Visible := true;
                }
                if (x2 == 0)
                {
                    SpO2Collection.Clear();
                    ///chart_wave.Series[0].Points.Clear();
                    //ss_Spo2Wave.Clear;
                }
                else
                {
                    //dY = SpO2Wave.Height - SpO2Wave.Height * arrWave[i].nData / SpO2_Data_Top_Limit;
                    dY = SpO2Wave.Height - SpO2Wave.Height * v2 / SpO2_Data_Top_Limit;
                    SpO2Collection.Add(new System.Windows.Point(SpO2Collection.Count, dY));
                    ///chart_wave.Series[0].Points.AddXY(x2, v2);
                }
                pulseSt = pulseSt + 1;
                if (pulseSt > drawCnt)
                {
                    pulseSt = 0;
                    //rewPulse := false;
                }
            }
            //pulseSt := pulseSt + 5;
            pulseEnd = pulseSt; 
        }

        void checkVer()
        {
            //throw new NotImplementedException();
            try
            {
                //throw new NotImplementedException();
                if (serialPort1.IsOpen && comOpen)
                {
                    TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 30);

                    serialPort1.Write(TxData_verH600, 0, TxData_verH600.Length);
                    Thread.Sleep(waitTime);
                    serialPort1.Write(TxData_ver1, 0, TxData_ver1.Length);
                    Thread.Sleep(waitTime);
                    serialPort1.Write(TxData_ver2, 0, TxData_ver2.Length);
                    if (verSendFF == false)
                    {
                        Thread.Sleep(waitTime);
                        serialPort1.Write(TxData_ver0, 0, TxData_ver0.Length);
                    }
                }
            }
            catch (Exception)
            {
                comOpen = false;
                timer_battery.IsEnabled = false;
            }
        }

        void timer_battery_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            try
            {
                //throw new NotImplementedException();
                if (serialPort1.IsOpen && comOpen && (ECG_startDr == false) )
                //if (serialPort1.IsOpen && comOpen)
                {
                    TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 30);
                    //serialPort1.Write(TxData_verH600, 0, TxData_verH600.Length);
                    //Thread.Sleep(waitTime);
                    serialPort1.Write(TxData_ver1, 0, TxData_ver1.Length);
                    Thread.Sleep(waitTime);
                    serialPort1.Write(TxData_ver2, 0, TxData_ver2.Length);
                    if (verSendFF == false)
                    {
                        Thread.Sleep(waitTime);
                        serialPort1.Write(TxData_ver0, 0, TxData_ver0.Length);
                    }
                }
            }
            catch (Exception)
            {
                comOpen = false;
                timer_battery.IsEnabled = false;
            }
        }

        void timer_chkGluDev_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen && comOpen) 
            {
                try
                {
                    serialPort1.Write(TxData_chkGluDevice, 0, TxData_chkGluDevice.Length);  
                    chkGluDev_cnt++;
                    if (chkGluDev_cnt > 2)
                        timer_chkGluDev.IsEnabled = false;
                }
                catch (Exception)
                {
                    comOpen = false;
                    timer_chkGluDev.IsEnabled = false;
                }
            }
        }

        void timer_start_Tick(object sender, EventArgs e)
        {
            startSend++;
            if (startSend > 10)
                timer_start.IsEnabled = false;
            //throw new NotImplementedException();
            try
            {
                //throw new NotImplementedException();
                if (serialPort1.IsOpen && comOpen && (ECG_startDr == false))
                //if (serialPort1.IsOpen && comOpen)
                {
                    serialPort1.Write(TxData_send, 0, TxData_send.Length);
                }
            }
            catch (Exception)
            {
                comOpen = false;
                timer_start.IsEnabled = false;
            }
        }

        void timer_display_Tick(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
            if (serialPort1.IsOpen)
            {
                _keepReading = false;
                dataPrc();
                _keepReading = true;
            }
        }

        private void btnComRef_Click(object sender, RoutedEventArgs e)
        {
            string[] portList = System.IO.Ports.SerialPort.GetPortNames();
            comboBox1.Items.Clear();
            for (int i = 0; i < portList.Length; ++i)
            {
                string name = portList[i];
                comboBox1.Items.Add(name);
            }
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            cboTempMode.Visibility = System.Windows.Visibility.Hidden;
            serialPort1.Close();
            if (cboDevice.SelectedIndex == 0)
                serialPort1.BaudRate = 115200;
            else if (cboDevice.SelectedIndex == 1)
                serialPort1.BaudRate = 9600;
            else
                serialPort1.BaudRate = 38400;

            verSendFF = false;
            listBox_dev.Items.Clear();
            timer_open.IsEnabled = false;
            timer_display.IsEnabled = false;
            timer_battery.IsEnabled = false;
            timer_start.IsEnabled = false;
            timer_pulse.IsEnabled = false;
            timer3.IsEnabled = false;
            image_heart.Visibility = System.Windows.Visibility.Hidden;
            int i = 0;
            readCnt = 0;
            for (i = 0; i < Arr_answer.Length; i++)
                Arr_answer[i] = 0;
            for (i = 0; i < ArrayPulseN.Length; i++)
                ArrayPulseN[i] = 0;
            for (i = 0; i < flowResp.Length; i++)
            {
                flowResp[i] = 0;
                flowSnore[i] = 0;
            }
            pulseIdx = 0;
            pulseSt = 0;
            pulseEnd = 0;
            rewPulse = false;
            showPulse = false;

            flowIdx = 0;
            flowSt = 0;
            flowEnd = 0;
            rewFlow = false;
            showFlow = false;

            drawFlag = true;

            _keepReading = false;
            
            comOpen = false;
            devAddr = "";
            try
            {
                serialPort1.Close();
                serialPort1.PortName = comboBox1.Text;
                serialPort1.Open();
                _keepReading = true;
                btnOpen.Visibility = System.Windows.Visibility.Hidden;
            }
            catch (Exception)
            {
                MessageBox.Show("Open Com Error", "Notice", System.Windows.MessageBoxButton.OK);
                //throw new NotImplementedException();
            }
            comOpen = false;
            if (_keepReading)
            {
                drawFlag = true;
                image_heart.Visibility = System.Windows.Visibility.Visible;
                timer_display.IsEnabled = true;
                
                TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 70);
                serialPort1.Close();
                Thread.Sleep(waitTime);
                serialPort1.Open();
                
                stopCnt = 0;
                idCnt = 0;
                timer_id.IsEnabled = true;
                thr_cnt = 0;
                timer_open.IsEnabled = true;
                timer4.IsEnabled = true;
            }
        }
        
        private void btnRead_Click(object sender, RoutedEventArgs e)
        {
            string s0 = byteToHexStr(Arr_answer, readCnt);

            MessageBox.Show("s0 : " + s0, "Notice", MessageBoxButton.OK);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            lblTempWarn.Visibility = System.Windows.Visibility.Hidden;
            comOpen = false;
            timer_open.IsEnabled = false;
            listBox_dev.Items.Clear();
            devAddr = "";
            sbarProduct.Content = "";
            sbarVersion.Content = "";
            lblCharge.Content = "";
            sbarBattery.Content = "";
            sbarCustomerId.Content = "";

            image_heart.Visibility = System.Windows.Visibility.Hidden;
            timer4.IsEnabled = false;
            timer_id.IsEnabled = false;

            timer_getSta.IsEnabled = false;

            timer_display.IsEnabled = false;
            timer_battery.IsEnabled = false;
            timer_start.IsEnabled = false;
            timer_open.IsEnabled = false;
            timer3.IsEnabled = false;
            if (comThread != null)
            {
                    comThread.Abort();
                    comThread.Join();
            }
            int i = 0;
            readCnt = 0;
            for (i = 0; i < Arr_answer.Length; i++)
                Arr_answer[i] = 0;
            for (i = 0; i < ArrayPulseN.Length; i++)
                ArrayPulseN[i] = 0;
            for (i = 0; i < flowResp.Length; i++)
            {
                flowResp[i] = 0;
                flowSnore[i] = 0;
            }
            pulseEnd = 0;
            pulseSt = 0;
            _keepReading = false;
            showPulse = false;
            showFlow = false;

            drawFlag = false;
            comOpen = false;
            
            TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 80);
            try
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.WriteLine("SPP:disconnect \r\n\0");
                    Thread.Sleep(waitTime);
                    serialPort1.WriteLine(bdScanOff);
                    Thread.Sleep(waitTime);
                }
            }
            catch (Exception) { }
            try
            {
                serialPort1.Close();
            }
            catch (Exception)
            {
                //MessageBox.Show("Close Com Error", "Notice", System.Windows.MessageBoxButton.OK);
                //throw new NotImplementedException();
            }
            btnOpen.Visibility = System.Windows.Visibility.Visible;
            comOpen = false;
            timer4.IsEnabled = false;
            timer_id.IsEnabled = false;
            timer_display.IsEnabled = false;
            timer_battery.IsEnabled = false;
            timer_start.IsEnabled = false;
            timer_open.IsEnabled = false;
            timer3.IsEnabled = false;
            try
            {
                serialPort1.Close();
            }
            catch (Exception)
            {
                //MessageBox.Show("Close Com Error", "Notice", System.Windows.MessageBoxButton.OK);
                //throw new NotImplementedException();
            }
        }
        
        private void Window_Closed(object sender, EventArgs e)
        {
            end_flag = true;

            btnClose_Click(sender, null);
            btnCloseBth_Click(sender, null);
            /*Process[] myProgress;
            myProgress = Process.GetProcesses();　　　　　　　　　　//获取当前启动的所有进程
            foreach (Process p in myProgress)　　　　　　　　　　　　//关闭当前启动的Excel进程
            {
                if ((p.ProcessName == "WireSer") || (p.ProcessName == "Wireless Device"))　//通过进程名来寻找
                {
                    p.CloseMainWindow();
                    p.Kill();
                    p.Dispose();
                    break;
                }
            }*/
            System.Environment.Exit(1);
            this.Dispatcher.Thread.Abort();
            this.Dispatcher.Thread.Join();
        }

        private void BtnThrState_Click(object sender, RoutedEventArgs e)
        {
            if (comThread != null)
                lblThrState.Content = comThread.IsAlive.ToString() + " , " + comThread.ThreadState.ToString();
            else
                lblThrState.Content = "null";
        }

        private void btnBth_Click(object sender, RoutedEventArgs e)
        {
            Process[] myProgress;
            myProgress = Process.GetProcesses();　　　　　　　　　　//获取当前启动的所有进程
            foreach (Process p in myProgress)　　　　　　　　　　　　//关闭当前启动的Excel进程
            {
                if ((p.ProcessName == "WireSer") || (p.ProcessName == "Wireless Device"))　//通过进程名来寻找
                {
                    //p.Close();
                    if (MessageBox.Show("Are you sure you want to restart the Bluetooth ?", "System Info", MessageBoxButton.YesNo) == MessageBoxResult.No)
                        break;
                    p.Kill();
                    p.Dispose();
                    break;
                }
            }

            //System.Diagnostics.Process.Start("WireSer.exe");
            if (File.Exists("WireSer.exe"))
                Process.Start(@"WireSer.exe");
            /*Process p = new System.Diagnostics.Process(); 
            p.StartInfo.FileName = @"WireSer.exe"; 
            p.StartInfo.Arguments = ""; 
            p.StartInfo.UseShellExecute = false; 
            p.StartInfo.RedirectStandardInput = true; 
            p.StartInfo.RedirectStandardOutput = true; 
            p.StartInfo.RedirectStandardError = true; 
            p.StartInfo.CreateNoWindow = false; 
            p.Start(); */
        }

        private void btnCloseBth_Click(object sender, RoutedEventArgs e)
        {
            if (end_flag == false)
            {
                if (MessageBox.Show("Are you sure you want to turn off the Bluetooth connection?", "System Info", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return;

                string fname = curDir + "\\dev.ini";
                if (File.Exists(fname))
                {
                    StringBuilder sb = new StringBuilder();
                    int i = GetPrivateProfileString("System", "COM", "", sb, 255, @fname);
                    if (i > 0)
                    {
                        i = int.Parse(sb.ToString());
                        if (i > 0)
                        {
                            if (comOpen && (serialPort1.PortName == "COM" + i.ToString()))
                            {
                                //MessageBox.Show(serialPort1.PortName);
                                btnClose_Click(null, null);
                            }
                        }
                    }
                }

            }

            /*Process[] myProgress;
            myProgress = Process.GetProcesses();　　　　　　　　　　//获取当前启动的所有进程
            foreach (Process p in myProgress)　　　　　　　　　　　　//关闭当前启动的Excel进程
            {
                if ((p.ProcessName == "WireSer") || (p.ProcessName == "Wireless Device"))　//通过进程名来寻找
                {
                  
                    p.CloseMainWindow();
                    p.Kill();
                    p.Dispose();
                    break;
                }
            }*/
            string txtFilename = String.Format(curDir + "\\BTD_quit.ini");
            FileStream savefs = new FileStream(txtFilename, FileMode.Create);
            StreamWriter savesw = new StreamWriter(savefs);
            savesw.Flush();
            savesw.WriteLine("[BDT]");
            savesw.WriteLine("close_flag = 1");
            savesw.Close();
            IntPtr hWnd = FindWindow("TApplication", "Wireless Device");
            if (hWnd != IntPtr.Zero)
            {
                //SendMessage(hWnd, WM_CLOSE, null, null);
                SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void cboxNibpMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lblNibpMode_cnt.Content = cboxNibpMode.SelectedIndex.ToString();
            setNibpMode(cboxNibpMode.SelectedIndex);
        }

        private void btnSysStart_Click(object sender, RoutedEventArgs e)
        {
            btnSysEnd_Click(sender, e);
            Thread.Sleep(new TimeSpan(0, 0, 0, 0, 600));//100
            try
            {
                //throw new NotImplementedException();
                if (serialPort1.IsOpen && comOpen)
                {
                    serialPort1.Write(TxData_NibpStart, 0, TxData_NibpStart.Length);
                }
            }
            catch (Exception)
            { }
        }

        private void btnSysEnd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //throw new NotImplementedException();
                if (serialPort1.IsOpen && comOpen)
                {
                    serialPort1.Write(TxData_NibpEnd, 0, TxData_NibpEnd.Length);
                }
            }
            catch (Exception)
            { }
            lblNibpStatus.Visibility = System.Windows.Visibility.Hidden;
            lblIrr.Visibility = System.Windows.Visibility.Hidden;
            edtTestNibp.Text = "";
            cboxNibpMode.IsEnabled = true;
        }

        private void cboxSys_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboxSys.SelectedIndex == 0)
            {
                if ( (lblDiaValue.Content.ToString() != "--") && (lblDiaValue.Content.ToString() != "") ) 
                {
                    lblSysValue.Content = tmpSysVal.ToString();
                    lblDiaValue.Content = tmpDiaVal.ToString();
                    lblMapValue.Content = tmpMapVal.ToString();
                }
            }
            else
            {
                if ((lblDiaValue.Content.ToString() != "--") && (lblDiaValue.Content.ToString() != ""))
                {
                    lblSysValue.Content = (((tmpSysVal * 133.0) / 100) / 10).ToString("#.#");
                    lblDiaValue.Content = (((tmpDiaVal * 133.0) / 100) / 10).ToString("#.#");
                    lblMapValue.Content = (((tmpMapVal * 133.0) / 100) / 10).ToString("#.#");
                }
            }
        }

        private void btnCalibrate_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort1.IsOpen && comOpen)
            {
                if ((lblNibpStatus.Visibility== System.Windows.Visibility.Visible) && (edtTestNibp.Text.ToString() != "") )
                {
                    /*try {
                        serialPort1.Write(TxData_NibpEnd, 0, TxData_NibpEnd.Length);
                    }
                    catch(Exception){}*/
                    btnSysEnd_Click(sender, e);
                    edtTestNibp.Text = "";
                }
                else
                {
                    /*try {
                        serialPort1.Write(TxData_NibpEnd, 0, TxData_NibpEnd.Length);
                    }
                    catch(Exception){}*/
                    btnSysEnd_Click(sender, e);
                    edtTestNibp.Text = "";
                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, 100));//30
                    edtTestNibp.Text = "";
                    try
                    {
                        serialPort1.Write(TxData_NibpTest, 0, TxData_NibpTest.Length);
                    }
                    catch (Exception) { }
                }
            }
        }

        private void lblNibpStatus_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lblNibpStatus.Visibility = System.Windows.Visibility.Hidden;
            if (edtTestNibp.Text.ToString() != "")
            {
                listBox_dev.Items.Add("MouseDown btnSysEnd_Click(null, null) " + DateTime.Now.ToString());
                btnSysEnd_Click(sender, e);
            }
        }

        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            listBox_dev.Items.Clear();
        }

        private void btnSetDateTime_Click(object sender, RoutedEventArgs e)
        {
            setDateTime();
        }

        private void btnSaveEcg_Click(object sender, RoutedEventArgs e)
        {
            string ecgFilename;
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            DateTime date2 = ecgFileData2.stTime;
            string filename2 = String.Format("{0}-{1:D2}-{2:D2}_{3:D2}-{4:D2}-{5:D2}", date2.Year, date2.Month, date2.Day, date2.Hour, date2.Minute, date2.Second);
            listBox_dev.Items.Add(filename2);
           
            saveFileDialog.Filter = "(*.ecg300)|*.ecg300";
            saveFileDialog.FileName = filename2;
            var result = saveFileDialog.ShowDialog();
            int i = 0, k;
            if (result == true)
            {
                ecgFilename = saveFileDialog.FileName;
                bw_ecg = new BinaryWriter(File.Open(ecgFilename, FileMode.Create) );
                /*ecgFileData2.ID = 1;
                ecgFileData2.HRavg = 60;
                ecgFileData2.ResultNo = 4;
                ecgFileData2.stTime = DateTime.Now;
                ecgFileData2.ECGMaxV = 0xFF;
                ecgDataB[0] = 0x0A;
                ecgDataB[8999] = 0xf;

                ecgFileData2.ECGRec[0] = 0x09;
                ecgFileData2.ECGRec[4499] = 0x090;
                ecgFileData2.ECGAmpV = 1;
                ecgFileData2.ECG1mvV = 28.5;*/
                //bw_ecg.Flush();
                for (k = 0; k < 1; k++)
                {
                    bw_ecg.Write(ecgFileData2.ID);
                    bw_ecg.Write(i);
                    bw_ecg.Write(ecgFileData2.stTime.ToOADate());
                    bw_ecg.Write(ecgFileData2.ResultNo);
                    bw_ecg.Write(ecgFileData2.HRavg);
                    bw_ecg.Write(ecgFileData2.ECGMaxV);
                    bw_ecg.Write(i);
                    bw_ecg.Write(ecgFileData2.ECGAmpV);
                    bw_ecg.Write(ecgFileData2.ECG1mvV);
                    for (i = 0; i < 4500; i++)
                        bw_ecg.Write(ecgFileData2.ECGRec[i]);
                }
                bw_ecg.Close();
            }
            /*var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                        saveFileDialog.Filter = "TXT File(*.txt)|*.txt";
                        var result = saveFileDialog.ShowDialog();
                        if (result == true)
                        {

                            FileStream savefs = new FileStream(saveFileDialog.FileName, FileMode.Create);
                            StreamWriter savesw = new StreamWriter(savefs);
                            savesw.Flush();
                            savesw.WriteLine("sdfssdfsdf");
                            savesw.Close();
                        }*/
        }

        private void btnOpenEcg_Click(object sender, RoutedEventArgs e)
        {
            string txtFilename = String.Format(tmpDir + "\\1.txt");
            string ecgFilename, s_tmp;
            int i;
            FileStream savefs = new FileStream(txtFilename, FileMode.Create);
            StreamWriter savesw = new StreamWriter(savefs);
            savesw.Flush();
            savesw.WriteLine(DateTime.Now.ToString());
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "(*.ecg300)|*.ecg300";
            openFileDialog.Title = "Please select a .ecg300 file to display.";
            var result = openFileDialog.ShowDialog();
            if (result == true)
            {
                BinaryReader rd_ecg = new BinaryReader(File.Open(openFileDialog.FileName, FileMode.Open));
                ECG_FILE_DATA ecgFileDataR = new ECG_FILE_DATA();
                try
                {
                    ecgFileDataR.ECGRec = new int[4500];
                    ecgFileDataR.ID = rd_ecg.ReadInt32();
                    rd_ecg.ReadInt32();
                    ecgFileDataR.stTime = DateTime.FromOADate(rd_ecg.ReadDouble());
                    ecgFileDataR.ResultNo = rd_ecg.ReadInt32();
                    ecgFileDataR.HRavg = rd_ecg.ReadInt32();
                    ecgFileDataR.ECGMaxV = rd_ecg.ReadInt32();
                    rd_ecg.ReadInt32();
                    ecgFileDataR.ECGAmpV = rd_ecg.ReadDouble();
                    ecgFileDataR.ECG1mvV = rd_ecg.ReadDouble();
                    s_tmp = "";
                    for (i = 0; i < 4500; i++)
                    {
                        ecgFileDataR.ECGRec[i] = rd_ecg.ReadInt32();
                        s_tmp = s_tmp + " , " + ecgFileDataR.ECGRec[i].ToString();
                    }
                    savesw.WriteLine("ID:" + ecgFileDataR.ID.ToString() + " , stTime:" + ecgFileDataR.stTime.ToString() + " , RE:" + ecgFileDataR.ResultNo.ToString() + " , HR:" + ecgFileDataR.HRavg.ToString() +
                        " , MaxV:" + ecgFileDataR.ECGMaxV.ToString() + " , AMP:" + ecgFileDataR.ECGAmpV.ToString() + " , 1mV:" + ecgFileDataR.ECG1mvV.ToString());
                    savesw.WriteLine(s_tmp);
                }
                catch (Exception) { }
                rd_ecg.Close();
                drawECG(ecgFileDataR);
            }

            savesw.Close();
        }

        private void drawECG(ECG_FILE_DATA ecgfiledata)
        {
            double y_val;
            int i, ecg_max;
            double amp_n;
            string s2;
            ECGCollection.Clear();
            lblEcgTime.Content = ecgfiledata.stTime.ToString();
            lblEcgReValue.Content = ecgfiledata.ResultNo.ToString() + " , " + Vpc_Type_30s[ecgfiledata.ResultNo];
            lblHrValue.Content = ecgfiledata.HRavg.ToString();
            lblMaxV.Content = ecgfiledata.ECGMaxV.ToString();
            lblEcg_amp.Content = ecgfiledata.ECGAmpV.ToString();
            lblEcg_1mv.Content = ecgfiledata.ECG1mvV.ToString();
            ecg_max = ecgfiledata.ECGMaxV;
            amp_n = ecgfiledata.ECGAmpV;
            //if (ecg_max == 255) amp_n = 1;
            s2 = "";
            for (i = 0; i < 4500; i++)
            {
                s2 = s2 + ecgfiledata.ECGRec[i].ToString() + " , ";
                y_val = (ecgfiledata.ECGRec[i] - ecg_max / 2.0) * amp_n + ecg_max / 2.0;

                if (y_val > ecg_max) y_val = ecg_max;
                else if (y_val < 0) y_val = 0;

                y_val = cvECG.Height - y_val * cvECG.Height * 1.0 / ecg_max;

                
                //imgEcgY[i] = y_val / cvECG.Height;
                ECGCollection.Add(new System.Windows.Point(ECGCollection.Count * cvECG.Width / 4500, y_val));
            }
            listBox_dev.Items.Add(s2);
            //MessageBox.Show(ecgfiledata.stTime.ToString() + " , " + ecgfiledata.HRavg.ToString());
        }
        
        void cboxGluDevice_SelectionChangedPrc()
        {
            if (btnDelGluData == null) return;
            btnDelGluData.Visibility = System.Windows.Visibility.Hidden;
            if (comOpen && serialPort1.IsOpen && (cboxGluDevice.SelectedIndex > 0) && (cboxGluDevice.SelectedIndex < 4))
            {
                setGluDevice(cboxGluDevice.SelectedIndex);
                if (cboxGluDevice.SelectedIndex == 3)
                    btnDelGluData.Visibility = System.Windows.Visibility.Visible;
                else
                    btnDelGluData.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private void cboxGluDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cboxGluDevice_SelectionChangedPrc(); 
        }

        private void cboxGlu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboxGlu.SelectedIndex == 0)
            {
                if ((lblGluValue.Content.ToString() != "--") && (lblGluValue.Content.ToString() != ""))
                    lblGluValue.Content = tmpGlu1Val.ToString("0.#");// +" ( mmol/L )"; ;
            }
            else
            {
                if ((lblGluValue.Content.ToString() != "--") && (lblGluValue.Content.ToString() != ""))
                    lblGluValue.Content = tmpGlu2Val.ToString("0.#");// +" ( mg/dL )"; ; 
            }

            /*string s2; 
            s2 = cboxGlu.SelectedItem.ToString();
            int i;
            i = s2.IndexOf(":",0);
            listBox_dev.Items.Add("cboxGlu.SelectedIndex =" + cboxGlu.SelectedIndex.ToString() 
             + " , " + cboxGlu.SelectedItem.ToString() + " , s2=" + s2 + " , i=" + i.ToString() + " , sub:" + s2.Substring(i+1).Trim());*/
        }

        private void cboxUa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboxUa.SelectedIndex == 0)
            {
                if ((lblUaValue.Content.ToString() != "--") && (lblUaValue.Content.ToString() != ""))
                    lblUaValue.Content = tmpUa1Val.ToString("0.##");// +" ( mmol/L )";
            }
            else
            {
                if ((lblUaValue.Content.ToString() != "--") && (lblUaValue.Content.ToString() != ""))
                    lblUaValue.Content = tmpUa2Val.ToString("0.##");// +" ( mg/dL )";
            }
        }

        private void cboxChol_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboxChol.SelectedIndex == 0)
            {
                if ((lblCholValue.Content.ToString() != "--") && (lblCholValue.Content.ToString() != ""))
                    lblCholValue.Content = tmpChol1Val.ToString("0.##");// +" ( mmol/L )";
            }
            else
            {
                if ((lblCholValue.Content.ToString() != "--") && (lblCholValue.Content.ToString() != ""))
                    lblCholValue.Content = tmpChol2Val.ToString("0.##");// +" ( mg/dL )";
            }
        }

        private void cboxGluDevice_MouseDown(object sender, MouseButtonEventArgs e)
        {
            cboxGluDevice_SelectionChangedPrc();
        }

        private void btnDelGluData_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort1.IsOpen && comOpen)
            {
                if (MessageBox.Show("Are you sure you want to delete the glucose device's data ?", "System Info", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return;
                try
                {
                    serialPort1.Write(TxData_delGluDevData, 0, TxData_delGluDevData.Length);
                }
                catch (Exception)
                {
                    comOpen = false;
                }
            }
        }

        private void cboTemp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboTemp.SelectedIndex == 0)
            {
                if ( (lblTempValue.Content != "--") && (lblTempValue.Content != "") )
                    lblTempValue.Content = tmpTempVal.ToString();
            }
            else
            {
                if ( (lblTempValue.Content != "--") && (lblTempValue.Content != "") )
                {
                    if (cboTempMode.IsVisible == false)
                        lblTempValue.Content = Math.Floor( (Math.Floor(tmpTempInt0 * 9 / 5.0) +3200) / 100).ToString();//intToStr( ((tmpTempInt0 * 9 div 5) +3200) div 100)
                    else
                        //txtTemp.Caption := format('%5.1f', [( (tmpTempInt0 * 9 / 5) +3200) / 100]);
                        lblTempValue.Content = (Math.Floor((tmpTempInt0 * 9 / 5 + 3200) / 10.0) / 10).ToString();//floatToStr(trunc( (tmpTempInt0 * 9 / 5 +3200) / 10 ) / 10);
                }
            }
        }

        private void cboTempMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i;
            if (comOpen && serialPort1.IsOpen && (cboTempMode.SelectedIndex > 0) && (cboTempMode.SelectedIndex < cboTempMode.Items.Count) )
            {
                i = cboTempMode.SelectedIndex * 16 + 1;
                //MessageBox.Show("i = " + i.ToString("x2"));
                setTempMode(i);
                Thread.Sleep(50);
                setTempMode(i);
            }
        }

    }
}
