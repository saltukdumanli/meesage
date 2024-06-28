using ProtocolDll;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ctest
{
    public class Ih110Connector : IDisposable
    {
        private readonly ManualResetEvent _waiter;
        private const int WaiterTimeOut = 2000;
        private readonly SerialPort _port;
        private readonly object _receiverLock;
        public byte[] _receivedBytes;
        private Thread _connectionThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Queue<byte[]> m_queWmCopyData = new Queue<byte[]>();
        private Protocol m_ProtocolDll;
        
        LocalInfo _localInfo = new LocalInfo();
        public List<SniBody> _serialNumbers;
        public List<SniBody> _notSerialNumbers;
        private string deviceModel;

        public Denomination Denomination { get; set; } = new Denomination();

        private readonly int MaxCountingTime = 1200;

        private Stopwatch _stopWatch;

        byte[] _values = new byte[48];
        byte[] _pieces = new byte[48];
        byte[] _currency = new byte[4];

        private static Ih110Connector _instance = null;

        public static Ih110Connector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Ih110Connector();
                }
                return _instance;
            }
        }

        public Ih110Connector() //: base(115200)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _connectionThread = new Thread(() => InitializeDevice(_cancellationTokenSource.Token));
            _waiter = new ManualResetEvent(false);
        }

        public bool Connect()
        {
            if (ControlConnection())
                return true;
            try
            {
                if (!_connectionThread.IsAlive)
                {
                    _connectionThread.SetApartmentState(ApartmentState.STA);
                    _connectionThread.Start();
                }

                // Thread içerisinde connection açılmasını bekleyelim.
                _waiter.WaitOne(2000);

                return ControlConnection();
            }
            catch (IOException exception)
            {
                DisposePort();
                throw; //throw new Exception(exception); //CcTalkException(Statics.ConnectionOpenError, exception);
            }
        }

        private bool ControlConnection()
        {
            if (m_ProtocolDll == null)
                return false;

            m_ProtocolDll.PrtcGetSeries();
            Thread.Sleep(200);

            if ((deviceModel??"").Contains("110"))
                return true;
            else
                return false;
        }

        private void InitializeDevice(CancellationToken cancelToken)
        {
            _stopWatch = new Stopwatch();

            m_ProtocolDll = new Protocol();
            m_ProtocolDll.SNIDataEvent += M_ProtocolDll_SNIDataEvent;
            m_ProtocolDll.ProtocolEvent += M_ProtocolDll_ProtocolEvent;

            if (Protocol.CheckDll())
            {
                bool connected = m_ProtocolDll.OpenSerialPort("COM6", 115200, 0);

                if (!connected)
                {
                    _cancellationTokenSource.Cancel();
                    _waiter.Set();
                    return;
                }
            }

            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                    return;

                // Her para sayıldığında windows message queue'ya ekleniyor.
                // Queue içerisindeki mesajları tektikleyelim
                if (this.m_queWmCopyData.Count > 0)
                {
                    byte[] wmCopyData = this.m_queWmCopyData.Dequeue();
                    this.ProtocolHandling(wmCopyData);
                }
                else
                {
                    Thread.Sleep(50);
                }

                _stopWatch.Stop();
                // Thread bir şekilde kapanmadıysa 20 dakika sonra kapatalım.
                if (_stopWatch.Elapsed.TotalSeconds > MaxCountingTime)
                    DisposePort();
                _stopWatch.Start();

                _waiter.Set();
            }
        }

        private void M_ProtocolDll_ProtocolEvent(byte[] protocolData)
        {
            m_queWmCopyData.Enqueue(protocolData);
        }


        private void M_ProtocolDll_SNIDataEvent(byte[] data)
        {
            this.ProtocolHandling(data);
        }

        private void ProtocolHandling(byte[] protocolData)
        {
            if (protocolData[0] == 0x0C)// Common Protocol
            {
                switch (protocolData[1])
                {
                    case 0x0D:// [12-13] Get Counted Result
                        {
                            byte[] countResult = new byte[protocolData.Length - 2];
                            Array.Clear(countResult, 0, protocolData.Length - 2);
                            Array.Copy(protocolData, 2, countResult, 0, protocolData.Length - 2);

                            GetCountedResult(countResult);
                        }
                        break;

                    case 0x11: // [12-17] Get Denom Info
                        {
                            byte[] denomInfo = new byte[protocolData.Length - 2];
                            Array.Clear(denomInfo, 0, protocolData.Length - 2);
                            Array.Copy(protocolData, 2, denomInfo, 0, protocolData.Length - 2);

                            GetDenomInfo(denomInfo);
                        }
                        break;
                    case 0x10: // [12-16] Get Local
                        {
                            byte[] localData = new byte[protocolData.Length - 2];
                            Array.Clear(localData, 0, protocolData.Length - 2);
                            Array.Copy(protocolData, 2, localData, 0, protocolData.Length - 2);

                            GetLocal(localData);
                        }
                        break;

                    case 0x49: // [12-73] Get Counting Finish New
                        {
                            //if (_chequeQRList.Count > 0)
                            //{
                            //    if (_snviewerForm.Visible)
                            //    {
                            //        _snviewerForm.ClearList();
                            //        _snviewerForm.ChangeColumn(false);

                            //        for (int i = 0; i < _chequeQRList.Count; i++)
                            //        {
                            //            _CHEQUE_QR_ chequeQR = _chequeQRList[i];
                            //            _snviewerForm.ProcessChequeQR(ref chequeQR);
                            //        }
                            //    }

                            //    _imageIndex = 0;
                            //    _strFImgPath = string.Empty;
                            //    _chequeQRList.Clear();
                            //}

                            byte[] recvData = new byte[protocolData.Length - 2];
                            Array.Clear(recvData, 0, protocolData.Length - 2);
                            Array.Copy(protocolData, 2, recvData, 0, protocolData.Length - 2);

                           PrintGetCountingFinishNew(recvData);
                        }
                        break;
                    case 0x4A: // [12-74] Get Local Info
                        {
                            string result = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] : ";
                            result += "[Protocol:12-74] Get Local Info";
                            //SetLogWriteLine(result);

                            _localInfo.UsePoint = protocolData[2];
                            _localInfo.CallFlag = protocolData[3];
                            Array.Copy(protocolData, 4, _localInfo.Local, 0, 4);

                            result = " - Local Name : " + Encoding.ASCII.GetString(_localInfo.Local, 0, _localInfo.Local.Length).Trim('\0');
                            //SetLogWriteLine(result);

                            //SetLogWriteLine(" - CallFlag = " + Convert.ToString(_localInfo.CallFlag));

                            for (int i = 0; i < 10; i++)
                            {
                                _localInfo.Denom[i] = (UInt32)protocolData[8 + (i * 4) + 0];
                                _localInfo.Denom[i] |= (UInt32)protocolData[8 + (i * 4) + 1] << 8;
                                _localInfo.Denom[i] |= (UInt32)protocolData[8 + (i * 4) + 2] << 16;
                                _localInfo.Denom[i] |= (UInt32)protocolData[8 + (i * 4) + 3] << 24;

                                if (_localInfo.Denom[i] == 0)
                                {
                                    _gtotalInfo.DenomCount = i;
                                    break;
                                }
                            }

                            // Denom Info not exsit
                            if (protocolData.Length - 2 == 1 && protocolData[2] == 0)
                            {
                                // Next Index Local
                                _localIndex++;
                                GetNewLocalInfo();
                            }
                            else
                            {
                               SettingLocalInfo(_localInfo);
                            }
                        }
                        break;
                    case 0x4B: // [12-75] Get GTotal Info
                        {
                            string result = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] : ";
                            result += "[Protocol:12-75] Get GTotal Info";
                            //SetLogWriteLine(result);

                            byte[] recvData = new byte[protocolData.Length - 2];
                            Array.Clear(recvData, 0, protocolData.Length - 2);
                            Array.Copy(protocolData, 2, recvData, 0, protocolData.Length - 2);

                            SettingGTotalInfo(recvData);
                        }
                        break;
                    case 0x15: // [12-21] Get Series
                        {
                            GetSeries(protocolData[2]);
                        }
                        break;
                    //case 0x49: // [12-73] Get Counting Finish New
                    //    {
                    //        //Reset();
                    //    }
                        //break;
                }
            }
            else if (protocolData[0] == 0x0A) // SNI Protocol & Upgrade Protocol
            {
                switch (protocolData[1])
                {
                    case 0x15:// [10-21] Real Time SNI Send Data
                        {
                            //SNI BODY
                            byte[] recvData = new byte[protocolData.Length - 2];
                            Array.Copy(protocolData, 2, recvData, 0, protocolData.Length - 2);
                            GCHandle handle = GCHandle.Alloc(recvData, GCHandleType.Pinned);
                            SniBody body = new SniBody();
                            try
                            {
                                body = (SniBody)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SniBody));
                            }
                            catch
                            {
                                handle.Free();
                            }

                            ReadSerialNumbers(body);
                        }
                        break;
                }
            }
        }
        public int _callFlag = 0;
        GTotalInfo _gtotalInfo=new GTotalInfo();


        private void SettingLocalInfo(LocalInfo localInfo)
        {
            // Invalid LocalInfo
            if (localInfo.CallFlag == 0)
            {
                _localIndex++;
                GetNewLocalInfo();
                return;
            }

            CallFlag callFlag = (CallFlag)localInfo.CallFlag;
            if (callFlag.HasFlag(CallFlag.ATM))
            {
                _gtotalInfo.UseAtm = 1;
                _queCallFlag.Enqueue(1);
            }

            if (callFlag.HasFlag(CallFlag.FIT))
            {
                _gtotalInfo.UseFit = 1;
                _queCallFlag.Enqueue(2);
            }

            if (callFlag.HasFlag(CallFlag.UNFIT))
            {
                _gtotalInfo.UseUnfit = 1;
                _queCallFlag.Enqueue(3);
            }

            if (callFlag.HasFlag(CallFlag.VALUE))
            {
                _gtotalInfo.UseValue = 1;
                _queCallFlag.Enqueue(4);
            }

            if (callFlag.HasFlag(CallFlag.MANUAL))
            {
                _gtotalInfo.UseManual = 1;
                _queCallFlag.Enqueue(5);
            }

            if (callFlag.HasFlag(CallFlag.REJECT))
            {
                _gtotalInfo.UseReject = 1;
                _queCallFlag.Enqueue(6);
            }

            // Denomination setting
            for (int i = 0; i < _gtotalInfo.DenomCount; i++)
            {
                if (_localInfo.UsePoint == 1)
                {
                    _gtotalInfo.DenomData[i] = (double)localInfo.Denom[i] / 100;
                }
                else
                {
                    _gtotalInfo.DenomData[i] = (double)localInfo.Denom[i];
                }
            }

            // Request GTotal Info By CallFlag
            GetNewGtotalInfo();
        }
        public void GetNewGtotalInfo()
        {
            if (_queCallFlag.Count() == 0)
            {
                PrintLocalGtotalInfo();
            }
            else
            {
                _callFlag = _queCallFlag.Dequeue();
                m_ProtocolDll.PrtcGetGTotalInfo(_localIndex, _callFlag);
            }
        }
        private void PrintGetCountingFinishNew(byte[] recvData)
        {
            string result = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] : ";
            result += "[Protocol:12-73] Counting Finish New";
            //SetLogWriteLine(result);

            result = "";

            if (recvData.Length > 1)
            {
                // 전송 타입
                bool bGTotal = false;
                bool bCoupon = false;
                bool bSNImage = false;
                byte ucType1 = recvData[0];
                byte ucType2 = recvData[1];
                if (ucType2 != 0)
                {
                    byte ucBit = 1;
                    if (Convert.ToBoolean(ucType2 & ucBit))
                    {
                        bSNImage = true;
                        result += " - SN Image counting";
                    }

                    ucBit = Convert.ToByte(ucBit << 1);
                    if (Convert.ToBoolean(ucType2 & ucBit))
                    {
                        result += " - Full Image counting";
                    }

                    ucBit = Convert.ToByte(ucBit << 1);
                    if (Convert.ToBoolean(ucType2 & ucBit))
                    {
                        bCoupon = true;
                        result += " - Coupon counting";
                    }
                }
                else
                {
                    bGTotal = true;
                    result = " - Normal counting";
                }
                //SetLogWriteLine(result);


                // Local Count 
                _localIndex = 0;
                _localCount = recvData[2];
                result = " - Local Count : " + _localCount.ToString();
                //SetLogWriteLine(result);
                result = "";

                // Bank, Branch, Teller 분리
                int nInfoLen = recvData[3];
                byte[] ucInfo = new byte[nInfoLen];
                Array.Copy(recvData, 4, ucInfo, 0, nInfoLen);

                // ASCII TO Unicode
                string strInfo = Encoding.Unicode.GetString(ucInfo, 0, nInfoLen);
                char[] sep = { '|' };

                // Info 분리
                //string[] strSplit = strInfo.Split(sep);
                //foreach (var item in strSplit)
                //{
                //    if (item[0] == 'B')
                //    {
                //        result = " - Bank Name : " + item.Substring(1);
                //    }
                //    if (item[0] == 'R')
                //    {
                //        result = " - Branch Name : " + item.Substring(1);
                //    }
                //    if (item[0] == 'T')
                //    {
                //        result = " - Teller Name : " + item.Substring(1);
                //    }
                //    //SetLogWriteLine(result);
                //}
                 
                if (bGTotal)
                {
                    GetNewLocalInfo();
                }
                if (bCoupon && !bSNImage)
                {
                    m_ProtocolDll.PrtcGetCouponInfo();
                }
            }
            else
            {
                result += "DTL Error! \r\n";

                //SetLogWriteLine(result);
            }
        }
        private void SettingGTotalInfo(byte[] recvData)
        {
            if(_gtotalInfo==null)
                _gtotalInfo = new GTotalInfo();
            if (_callFlag == 1) // ATM
            {
                Buffer.BlockCopy(recvData, 0, _gtotalInfo.AtmCount, 0, 120);
            }
            else if (_callFlag == 2) // FIT
            {
                Buffer.BlockCopy(recvData, 0, _gtotalInfo.FitCount, 0, 120);
            }
            else if (_callFlag == 3) // UNFIT
            {
                Buffer.BlockCopy(recvData, 0, _gtotalInfo.UnfitCount, 0, 120);
            }
            else if (_callFlag == 4) // VALUE
            {
                Buffer.BlockCopy(recvData, 0, _gtotalInfo.ValueCount, 0, 120);
            }
            else if (_callFlag == 5) // MANUAL
            {
                Buffer.BlockCopy(recvData, 0, _gtotalInfo.ManualCount, 0, 120);
            }
            else if (_callFlag == 6) // REJECT
            {
                uint[] RejectCountCopy = new uint[30];
                Buffer.BlockCopy(recvData, 0, RejectCountCopy, 0, 120);

                _gtotalInfo.RejectCount = 0;
                for (int i = 0; i < 30; i++)
                {
                    if (RejectCountCopy[i] > 0)
                    {
                        _gtotalInfo.RejectCount += RejectCountCopy[i];
                    }
                }
            }
            if (_queCallFlag.Count() == 0)
            {
               PrintLocalGtotalInfo();
            }
            else
            {
                _callFlag = _queCallFlag.Dequeue();
                m_ProtocolDll.PrtcGetGTotalInfo(_localIndex, _callFlag);
            }
        }

        private void PrintLocalGtotalInfo()
        {
           
            string result = string.Empty;

            uint DenomTotal = 0;
            uint TotalCount = 0;
            double TotalAmount = 0;
            _notSerialNumbers = new List<SniBody>();

            for (int i = 0; i < _gtotalInfo.DenomCount; i++)
            {
                if (_gtotalInfo.UseAtm == 1)
                {
                    DenomTotal += _gtotalInfo.AtmCount[i] + _gtotalInfo.AtmCount[10 + i] + _gtotalInfo.AtmCount[20 + i];
                }
                if (_gtotalInfo.UseFit == 1)
                {
                    DenomTotal += _gtotalInfo.FitCount[i] + _gtotalInfo.FitCount[10 + i] + _gtotalInfo.FitCount[20 + i];
                }
                if (_gtotalInfo.UseUnfit == 1)
                {
                    DenomTotal += _gtotalInfo.UnfitCount[i] + _gtotalInfo.UnfitCount[10 + i] + _gtotalInfo.UnfitCount[20 + i];
                }
                if (_gtotalInfo.UseValue == 1)
                {
                    DenomTotal += _gtotalInfo.ValueCount[i] + _gtotalInfo.ValueCount[10 + i] + _gtotalInfo.ValueCount[20 + i];
                }
                if (_gtotalInfo.UseManual == 1)
                {
                    DenomTotal += _gtotalInfo.ManualCount[i] + _gtotalInfo.ManualCount[10 + i] + _gtotalInfo.ManualCount[20 + i];
                }

                result += " - Denom[" + i.ToString() + " = " + _gtotalInfo.DenomData[i].ToString() + "] : " + DenomTotal.ToString() + "\r\n";

                TotalCount += DenomTotal;
                TotalAmount += DenomTotal * _gtotalInfo.DenomData[i];
                if (DenomTotal.ToString() != "0")
                    _notSerialNumbers.Add(new SniBody { Denomination = _gtotalInfo.DenomData[i].ToString(), CountDenom = DenomTotal });
                DenomTotal = 0;
            }

            if (_gtotalInfo.UseReject == 1)
            {
                // Reject Count
                TotalCount += _gtotalInfo.RejectCount;
                result += " - Reject Count : " + _gtotalInfo.RejectCount.ToString() + "\r\n";
            }

            // Total Count
            result += " - Total Count : " + TotalCount.ToString() + "\r\n";
            result += " - Total Amount : " + TotalAmount.ToString() + "\r\n";


            //SetLogWriteLine(result);

            // Request Next Local
            _localIndex++;
            m_ProtocolDll.PrtcGetLocal();
        }

        public void GetNewLocalInfo()
        {
            if (_localIndex < _localCount)
            {
                m_ProtocolDll.PrtcGetLocalInfo(_localIndex);
            }
            else
            {
                _callFlag = 0;
                _localIndex = 0;
                _localCount = 0;
                _gtotalIndex = 0;
                _gtotalInfo.Clear();
            }
        }
        Queue<int> _queCallFlag = new Queue<int>();

        public int _gtotalIndex = 0;
        public int _localCount = 0;
        public int _localIndex = 0;
        public void Read()
        {

            //// İlk 4 döviz kodu
            //// 4-52 arası döviz değerleri
            //// 52-100 arası parça sayısı
            //// 100-... arası seri numaralar tutulur. Her bir seri numara 32 byte uzunluğunda. Parser içerisinde stringe çevirilecek.

            //// Eğer makinenin seri numara okuması açık ise sonuç sıfırdan büyük olacaktır.
            ////int serialNumbersLength = _serialNumbers == null ? 0 : _serialNumbers.Length;
            //var str = Encoding.ASCII.GetString(_currency, 1, 3);
            ////     byte[] totalResult = new byte[_currency.Length + _values.Length + _pieces.Length + serialNumbersLength];
            ////     Buffer.BlockCopy(_currency, 0, totalResult, 0, _currency.Length);
            ////     Buffer.BlockCopy(_values, 0, totalResult, _currency.Length, _values.Length);
            ////     Buffer.BlockCopy(_pieces, 0, totalResult, _currency.Length + _values.Length, _pieces.Length);

            ////     // Eğer makinenin seri numara okuması açık değilse sonuca _serialNumbers eklenmesin.
            ////     if (_serialNumbers != null)
            ////         Buffer.BlockCopy(_serialNumbers, 0, totalResult, _currency.Length + _values.Length + _pieces.Length, _serialNumbers.Length);
            ////     _receivedBytes = totalResult;
        }

        private void ReadSerialNumbers(SniBody ocrStringTex)
        {
            if (_serialNumbers == null)
            {
                _serialNumbers = new() ;
            }
            if (!_serialNumbers.Any(y => y.SerialNumber == ocrStringTex.SerialNumber))
                _serialNumbers.Add(ocrStringTex);
            //if (string.IsNullOrEmpty(ocrStringText))
            //    return;

            //byte[] ocrString = new byte[32];
            //double cvalue = 0;

            //byte[] arr = Encoding.ASCII.GetBytes(ocrStringText);
            //arr.CopyTo(ocrString, 0);

            //cvalue = Convert.ToDouble(cValueText);

            //// 32 byte seri numaranın başına 8 byte currency value ekleyelim,
            //// parser tarafında hangi banknota ait olduğunu bulmak için kullanacağız.
            //byte[] cvalueAsBytes = BitConverter.GetBytes(cvalue);

            //byte[] ocrTotal = new byte[ocrString.Length + cvalueAsBytes.Length];
            //cvalueAsBytes.CopyTo(ocrTotal, 0);
            //ocrString.CopyTo(ocrTotal, cvalueAsBytes.Length);
            //ocrString = ocrTotal;

            //if (_serialNumbers == null)
            //{
            //    _serialNumbers = ocrString;
            //}
            //else
            //{
            //    var result = new byte[_serialNumbers.Length + ocrString.Length];
            //    _serialNumbers.CopyTo(result, 0);
            //    ocrString.CopyTo(result, _serialNumbers.Length);
            //    _serialNumbers = result;
            //}
        }

        private void GetDenomInfo(byte[] denomInfo)
        {
            int index = 2;
            int denomCount = denomInfo[1];
            byte[] denomData = new byte[4];
            int[] intArr = new int[denomCount];

            for (int i = 0; i < denomCount; i++)
            {
                Array.Copy(denomInfo, index, denomData, 0, 4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(denomData);

                intArr[i] = BitConverter.ToInt32(denomData, 0);
                index += 4;
            }

            GetBytes(intArr).CopyTo(_values, 0);
        }

        private void GetCountedResult(byte[] countResult)
        {
            int index = 2;
            int denomCount = countResult[1];
            byte[] countData = new byte[4];
            int[] intArr = new int[denomCount];

            for (int i = 0; i < denomCount; i++)
            {
                Array.Copy(countResult, index, countData, 0, 4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(countData);

                intArr[i] = BitConverter.ToInt32(countData, 0);
                index += 4;
            }

            GetBytes(intArr).CopyTo(_pieces, 0);
        }

        private void GetLocal(byte[] localData)
        {
            String cry = "";
            localData.CopyTo(_currency, 0);
            if (localData.Length == 4)
            {
                cry = Encoding.ASCII.GetString(localData, 1, 3);
            }
            if (_notSerialNumbers != null)
            {
                foreach (var item in _notSerialNumbers)
                {
                    item.Local = cry;
                }
            }
            
        }

        private void GetSeries(byte series)
        {
            // Series'e göre model değişiyor.
            //if (series == 3)

            deviceModel = "110";
        }

        private byte[] GetBytes(int[] values)
        {
            var result = new byte[values.Length * sizeof(int)];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
        }

        public void DisposePort()
        {
            m_queWmCopyData.Clear();
            _instance = null;
            _receivedBytes = null;
            _serialNumbers = null;
            _notSerialNumbers = null;
            Denomination = new Denomination();
            _values = new byte[48];
            _pieces = new byte[48];
            _currency = new byte[4];
            _waiter.Set();

            if (_connectionThread != null && _connectionThread.IsAlive)
                _cancellationTokenSource.Cancel();

            m_ProtocolDll.CloseDll();
        }

        public void Reset()
        {
            _serialNumbers = null;
            _notSerialNumbers = null;
            _receivedBytes = null;
            _values = new byte[48];
            _pieces = new byte[48];
            _currency = new byte[4];
            Denomination = new Denomination();
            // TO DO : Makine reset fonksiyonu
            //m_ProtocolDll.
        }

        public void Dispose()
        {
            DisposePort();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class SniBody
    {
        public byte Used; // Start Flag
        public byte Type; // Serial Number = 1
        public UInt32 Index; // Note Count Index
        public byte Count; // SN Count
        public byte SizeX1; // SN X Size1
        public byte SizeX2; // SN X Size2
        public byte SizeY; // SN Y Size
        public byte OutputPocket; // Out Pocket

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
        public String Denomination; // Denomination

        public byte CurrencyIndex; // Denomination Index
        public byte NoteState; // Note State : ATM, FIT, UNFIT, COMMON, REJECT
        public byte HiCode; // Error Hi Code
        public byte LowCode; // Error Low Code
        public byte NoteVersion; // Note Version : old, new, veryNew

        public UInt16 CountDate; // Count Date
        public UInt16 CountTime; // Count Time

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public String Local; // Local

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public String SerialNumber; // Serial Number

        public UInt16 DataSize; // Serial Image Size
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5280)]
        public byte[] Data; // SerialI Image Data 
        public uint CountDenom;
    };
}

