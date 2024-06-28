using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ctest
{
        [Flags]
        public enum CallFlag : byte
        {
            ATM    = 1 << 0,
            FIT    = 1 << 1,
            UNFIT  = 1 << 2,
            VALUE  = 1 << 3,
            MANUAL = 1 << 4,
            REJECT = 1 << 5
        };

    public class LocalInfo
    {
        public byte UsePoint;
        public byte CallFlag;
        public byte[] Local = new byte[4];
        public UInt32[] Denom = new UInt32[10];

        public LocalInfo() { }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct _CHEQUE_QR_
    {
        public UInt16 CountDate;
        public UInt16 CountTime;

        public UInt32 NoteIndex;
        public byte NoteState;     // 0 = Reject, 1 = Stacker
        public byte NoteDirection; // 0 = FF, 1 = FR, 2 = BF, 3 = BR

        public byte NoteErrorCodeHi;
        public byte NoteErrorCodeLow;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] MachineSN;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] Teller;

        public UInt32 BankCode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] BankName;

        public UInt32 BranchCode;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
        public byte[] BranchName;

        public int MICRXSize;
        public int MICRYSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24000)]
        public byte[] MICRImage;

        public byte MICRCodeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
        public byte[] MICRCode;

        public byte QRCodeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        public byte[] QRCode;

        public UInt16 FullImageWidth;
        public UInt16 FullImageHeight;
    }
    public class GTotalInfo
    {
        public int DenomCount;
        public double[] DenomData = new double[10];

        public byte UseAtm;
        public uint[] AtmCount = new uint[30];
        public double[] AtmAmount = new double[30];

        public byte UseFit;
        public uint[] FitCount = new uint[30];
        public double[] FitAmount = new double[30];

        public byte UseUnfit;
        public uint[] UnfitCount = new uint[30];
        public double[] UnfitAmount = new double[30];

        public byte UseValue;
        public uint[] ValueCount = new uint[30];
        public double[] ValueAmount = new double[30];

        public byte UseManual;
        public uint[] ManualCount = new uint[30];
        public double[] ManualAmount = new double[30];

        public byte UseReject;
        public uint RejectCount;

        public GTotalInfo() { }

        public void Clear()
        {
            DenomCount = 0;
            UseAtm = 0;
            UseFit = 0;
            UseUnfit = 0;
            UseValue = 0;
            UseManual = 0;
            UseReject = 0;
            RejectCount = 0;

            Array.Clear(DenomData, 0, DenomData.Length);
            Array.Clear(AtmCount, 0, AtmCount.Length);
            Array.Clear(AtmAmount, 0, AtmAmount.Length);
            Array.Clear(FitCount, 0, FitCount.Length);
            Array.Clear(FitAmount, 0, FitAmount.Length);
            Array.Clear(UnfitCount, 0, UnfitCount.Length);
            Array.Clear(UnfitAmount, 0, UnfitAmount.Length);
            Array.Clear(ValueCount, 0, ValueCount.Length);
            Array.Clear(ValueAmount, 0, ValueAmount.Length);
            Array.Clear(ManualCount, 0, ManualCount.Length);
            Array.Clear(ManualAmount, 0, ManualAmount.Length);
        }
    }
}
