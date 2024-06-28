using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ctest
{


    public class MessageParser
    {
        private static readonly string[] currencyNames = new string[] { "TL", "EUR", "USD", "GBP", "RUB" };
        private List<SniBody> denomLines;
        private Denomination denom;
        public MessageParser()
        {
            denom = new Denomination();
        }
        public string Parse(List<SniBody> _serialNumber, List<SniBody> _notSerialNumber)
        {
            return ParseDenomination(_serialNumber, _notSerialNumber);
        }
        private string ParseDenomination(List<SniBody> _serialNumber, List<SniBody> _notSerialNumber)
        {
            if(_serialNumber!=null && _serialNumber.Count>0)
            {
                denomLines = _serialNumber;
            }else
            {
                denomLines = _notSerialNumber;
            }
            denomLines = denomLines ?? new List<SniBody>();
            SetCurrency();
            SetNotes();
           // SetTotalValue();
            return string.Format("{0}|{1}", Regex.Replace(JsonConvert.SerializeObject(denom), "[()]", String.Empty), String.Empty);
        }
        //private void SetTotalValue()
        //{
        //    byte[] amValue = new byte[8];
        //    Buffer.BlockCopy(denomLines, 10 + (2 * 12 * sizeof(int)), amValue, 0, 8);
        //    denom.MainValue = (int)BitConverter.ToDouble(amValue);
        //}
        private void SetNotes()
        {
            // İlk 10 döviz kodu 
            // 10-58 arası döviz değerleri
            // 58-106 arası parça sayısı
            // 106-114 arası toplam değer
            // 114-... arası seri numaralar tutulur. Her bir seri numara 32 byte uzunluğunda. Parser içerisinde stringe çevirilecek.


            if (denomLines.Where(t=>string.IsNullOrEmpty( t.SerialNumber)).Count()==denomLines.Count)
            {

                denomLines.ForEach(t =>
                {
                    denom.AddNote(Convert.ToInt32(t.Denomination), Convert.ToInt32(t.CountDenom), Convert.ToInt32(t.Denomination), null);
                });
                denom.MainValue = denomLines.Sum(t => Convert.ToInt32(t.Denomination)*Convert.ToInt32( t.CountDenom));

                denom.MainCount = denomLines.Sum(t =>  Convert.ToInt32(t.CountDenom));
            }
            else
            {
                denomLines.GroupBy(t => Convert.ToInt32(t.Denomination)).ToList().ForEach(t =>
                {
                    denom.AddNote(t.Key, t.Count(), t.Key, t.Select(x => new SerialNumber { Text = x.SerialNumber }).ToArray());
                });

                denom.MainValue = denomLines.Sum(t => Convert.ToInt32(t.Denomination));

                denom.MainCount = denomLines.Count;
            }

          
            //byte[] cyValue = new byte[12 * sizeof(int)];
            //byte[] cyPiece = new byte[12 * sizeof(int)];
            //Buffer.BlockCopy(denomLines, 10, cyValue, 0, cyValue.Length);
            //Buffer.BlockCopy(denomLines, (10 + cyValue.Length), cyPiece, 0, cyPiece.Length);
            //int[] values = GetIntArray(cyValue);
            //int[] pieces = GetIntArray(cyPiece);
            //for (int i = 0; i < values.Length; i++)
            //{
            //    if (values[i] == 0 || pieces[i] == 0)
            //        continue;
            //    SerialNumber[] serialNumbers = GetSerialNumbers(values[i]);
            //    denom.MainCount += pieces[i];
            //    if (serialNumbers != null)
            //        ValidateNote(pieces[i], serialNumbers.Length);
            //    denom.AddNote(values[i], pieces[i], values[i], serialNumbers);
            //}
        }
        private void ValidateNote(int pieces, int serialNumbers)
        {
            if (serialNumbers != 0 && pieces != serialNumbers)
                throw new Exception("ValidateNote");
        } 
        private int[] GetIntArray(byte[] bytes)
        {
            var result = new int[bytes.Length / sizeof(int)];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }
        private void SetCurrency()
        {
            //byte[] cName = new byte[10];
            //Buffer.BlockCopy(denomLines, 1, cName, 0, 3);
            //denom.Currency = Encoding.ASCII.GetString(cName).Replace("\0","")
            //    .Replace("TRY", "TL");
             bool isFound = false;
            if (denomLines.Any())
                denom.Currency = denomLines.FirstOrDefault().Local.Replace("TRY", "TL");

            foreach (var currencyName in currencyNames)
            {
                if (currencyName == denom.Currency)
                {
                    isFound = true;
                    break;
                }
            }
            if (!isFound)
            {
                throw new Exception("error curreny");
            }
        }
    }



    public class Denomination
    {
        public Denomination()
        {
            Notes = new Dictionary<string, Note>();
        }
        //
        public string Currency { get; set; }
        public string RawData { get; set; }
        public Dictionary<string, Note> Notes { get; set; }
      
        public int MainCount { get; set; }
      
        public int MainValue { get; set; }
      
        public int AddCount { get; set; }
      
        public int AddValue { get; set; }
        public void AddNote(int denom, int count, int value, SerialNumber[] serialNumbers = null)
        {
            var note = new Note(denom, count, value, serialNumbers);
            Notes.Add(denom.ToString(), note);
        }
    }
    public class Machine
    {
        public string UserName { get; set; }
        public string ProductName { get; set; }
        public int WorkingState { get; set; }
        public bool IsReadyToWork { get; set; }
        public int Mode { get; set; }
        public int Fitness { get; set; }
        public int Direction { get; set; }
        public bool IsVersionModeOn { get; set; }
        public bool IsSerialModeOn { get; set; }
        public bool IsFullImageModeOn { get; set; }
        public bool IsAddModeOn { get; set; }
        public bool IsGtModeOn { get; set; }
    }
    public class Note 
    {
        public Note(int denom, int count, int value, SerialNumber[] serialNumbers = null)
        {
            Denom = denom;
            Count = count;
            Value = value;
            SerialNumbers = serialNumbers;
        }
      
        public int Denom { get; set; }
      
        public int Count { get; set; }
      
        public int Value { get; set; }
        //[Json]
        public SerialNumber[] SerialNumbers { get; set; }
    }
    public class SerialNumber
    {
        public string Text { get; set; }
    }
    public enum Directions
    {
        Off,
        Face,
        Org
    }
    public enum Fitness
    {
        Value,
        Fit,
        Atm
    }
    public enum Modes
    {
        Mix,
        Sp,
        Sg
    }
    public enum WorkingStates
    {
        Waiting,
        Counting,
        Setting
    }
}
