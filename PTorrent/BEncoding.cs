using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTorrent
{
    /// <summary>
    /// 
    /// </summary>
    public static class BEncoding
    {
        private const byte DictionaryStart = 100; //"d"
        private const byte DictionaryEnd = 101; //"e"
        private const byte ListStart = 108;  //"l"
        private const byte ListEnd = 101;  //"e"
        private const byte NumberStart = 105; //"i"
        private const byte NumberEnd = 101; //"e"
        private const byte ByteArrayDivider = 58; //":"

        #region Decode Methods

        public static bool Decode(byte[] bytes, out object decodedObject)
        {
            int index = 0;
            return DecodeObject(bytes, ref index, out decodedObject);
        }

        public static bool DecodeFile(string filepath, out object decodedFile)
        {
            decodedFile = null;
            if (!File.Exists(filepath))
            {
                return false;
            }

            return Decode(File.ReadAllBytes(filepath), out decodedFile);
        }

        private static bool DecodeObject(byte[] bytes, ref int startIndex, out object decodedObject)
        {
            switch (bytes[startIndex])
            {
                case ListStart:
                    startIndex++;
                    return DecodeList(bytes, ref startIndex, out decodedObject);
                case DictionaryStart:
                    startIndex++;
                    return DecodeDictionary(bytes, ref startIndex, out decodedObject);
                case NumberStart:
                    startIndex++;
                    return DecodeNumber(bytes, ref startIndex, out decodedObject);
                default:
                    return DecodeByteArray(bytes, ref startIndex, out decodedObject);
            }
        }

        private static bool DecodeList(byte[] bytes, ref int startIndex, out object decodedList)
        {
            var objs = new List<object>();
            decodedList = objs;

            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == ListEnd)
                {
                    startIndex = i + 1;
                    return true;
                }
                if (DecodeObject(bytes, ref i, out object decodedObject))
                {
                    objs.Add(decodedObject);
                    i--;//subtract one on i because we are about to ++ it when the loop goes around, was causing missed numbers
                }
            }
            return false;
        }

        private static bool DecodeNumber(byte[] bytes, ref int startIndex, out object decodedNumber)
        {
            int endIndex = -1;

            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == NumberEnd)
                {
                    endIndex = i;
                    break;
                }
            }

            decodedNumber = 0;

            if (endIndex == -1)
            {
                startIndex = bytes.Length;
                return false;
            }

            long val;
            if (Int64.TryParse(Encoding.UTF8.GetString(bytes, startIndex, endIndex - startIndex), out val))
            {
                decodedNumber = val;
                startIndex = endIndex + 1;
                return true;
            }

            startIndex = bytes.Length;
            return false;
        }

        private static bool DecodeDictionary(byte[] bytes, ref int startIndex, out object decodedDict)
        {
            var objs = new Dictionary<string, object>();
            decodedDict = objs;

            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == DictionaryEnd)
                {
                    startIndex = i + 1;
                    return true;
                }
                if(DecodeByteArray(bytes, ref i, out object key) && DecodeObject(bytes, ref i, out object val))
                {
                    var keyString = Encoding.UTF8.GetString((byte[])key);
                    objs.Add(keyString, val);
                    i--;//subtract one on i because we are about to ++ it when the loop goes around, was causing missed numbers
                }

            }
            //we shoudln't get past the end of the byte array unless it's malformed
            startIndex = bytes.Length;
            return false;
        }

        private static bool DecodeByteArray(byte[] bytes, ref int startIndex, out object decodedByteArr)
        {
            int endIndex = -1;
            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == ByteArrayDivider)
                {
                    endIndex = i;
                    break;
                }
            }
            decodedByteArr = null;

            if (endIndex == -1)
            {
                startIndex = bytes.Length;
                return false;
            }
            int length;
            if (Int32.TryParse(Encoding.UTF8.GetString(bytes, startIndex, endIndex - startIndex), out length))
            {
                endIndex++;
                startIndex = endIndex + length;
                byte[] outArray = new byte[length];
                Array.Copy(bytes, endIndex, outArray, 0, length);
                decodedByteArr = outArray;
                return true;
            }

            //we shoudln't get past the end of the byte array unless it's malformed
            startIndex = bytes.Length;
            return false;
        }

        #endregion

        #region Encode Methods

        public static byte[] Encode(object objToEncode)
        {
            MemoryStream ms = new MemoryStream();
            EncodeObject(objToEncode, ms);
            return ms.ToArray();
        }

        public static void EncodeToFile(object objToEncode, string filepath)
        {
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }
            File.WriteAllBytes(filepath, Encode(objToEncode));
        }

        private static void EncodeObject(object objToEncode, MemoryStream ms)
        {
            switch (objToEncode)
            {
                case byte[] bytes:
                    EncodeByteArray(bytes, ms);
                    break;
                case string str:
                    EncodeByteArray(Encoding.UTF8.GetBytes(str), ms);
                    break;
                case Dictionary<string, object> dict:
                    EncodeDictionary(dict, ms);
                    break;
                case List<object> lst:
                    EncodeList(lst, ms);
                    break;
                case long l:
                    EncodeNumber(l, ms);
                    break;
                default:
                    break;
                case null:
                    break;
            }
        }

        public static void EncodeNumber(long num, MemoryStream ms)
        {
            string str = EncodeNumber(num);
            ms.Write(Encoding.UTF8.GetBytes(str), 0, str.Length);
        }

        public static string EncodeNumber(long num)
        {
            return string.Format("{0}{1}{2}", NumberStart, num, NumberEnd);
        }

        public static void EncodeList(List<object> lst, MemoryStream ms)
        {
            ms.WriteByte(ListStart);
            foreach(var obj in lst)
            {
                EncodeObject(obj, ms);
            }
            ms.WriteByte(ListEnd);
        }

        public static void EncodeDictionary(Dictionary<string, object> dict, MemoryStream ms)
        {
            ms.WriteByte(DictionaryStart);
            foreach(var obj in dict)
            {
                EncodeByteArray(Encoding.UTF8.GetBytes(obj.Key), ms);
                EncodeObject(obj.Value, ms);
            }
            ms.WriteByte(DictionaryEnd);
        }

        public static void EncodeByteArray(byte[] bytes, MemoryStream ms)
        {
            string length = string.Format("{0}", bytes.Length);
            ms.Write(Encoding.UTF8.GetBytes(length), 0, length.Length);
            ms.WriteByte(ByteArrayDivider);
            ms.Write(bytes, 0, bytes.Length);
        }

        #endregion
    }
}
