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

        public static object Decode(byte[] bytes)
        {
            int index = 0;
            return DecodeObject(bytes, ref index);
        }

        public static object DecodeFile(string filepath)
        {
            if (!File.Exists(filepath))
            {
                return null;
            }

            return Decode(File.ReadAllBytes(filepath));
        }

        private static object DecodeObject(byte[] bytes, ref int startIndex)
        {
            switch (bytes[startIndex])
            {
                case ListStart:
                    startIndex++;
                    return DecodeList(bytes, ref startIndex);
                case DictionaryStart:
                    startIndex++;
                    return DecodeDictionary(bytes, ref startIndex);
                case NumberStart:
                    startIndex++;
                    return DecodeNumber(bytes, ref startIndex);
                default:
                    return DecodeByteArray(bytes, ref startIndex);
            }
        }

        private static List<object> DecodeList(byte[] bytes, ref int startIndex)
        {
            List<object> objs = new List<object>();

            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == ListEnd)
                {
                    startIndex = i + 1;
                    return objs;
                }
                objs.Add(DecodeObject(bytes, ref i));
            }

            //we shoudln't get past the end of the byte array unless it's malformed
            startIndex = bytes.Length;
            return null;
        }

        private static long DecodeNumber(byte[] bytes, ref int startIndex)
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

            startIndex = endIndex + 1;

            if (endIndex == -1)
            {
                return -1;
            }

            long val;
            if (Int64.TryParse(Encoding.UTF8.GetString(bytes, startIndex, endIndex - startIndex), out val))
            {
                return val;
            }

            return -1;
        }

        private static Dictionary<string, object> DecodeDictionary(byte[] bytes, ref int startIndex)
        {
            Dictionary<string, object> objs = new Dictionary<string, object>();

            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] == DictionaryEnd)
                {
                    startIndex = i + 1;
                    return objs;
                }
                string key = Encoding.UTF8.GetString(DecodeByteArray(bytes, ref i));
                object val = DecodeObject(bytes, ref i);

                objs.Add(key, val);
            }
            //we shoudln't get past the end of the byte array unless it's malformed
            startIndex = bytes.Length;
            return null;
        }

        private static byte[] DecodeByteArray(byte[] bytes, ref int startIndex)
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
            if (endIndex == -1)
            {
                startIndex = bytes.Length;
                return new byte[0];
            }
            int length;
            if (Int32.TryParse(Encoding.UTF8.GetString(bytes, startIndex, endIndex - startIndex), out length))
            {
                endIndex++;
                startIndex = endIndex + length;
                byte[] outArray = new byte[length];
                Array.Copy(bytes, endIndex, outArray, 0, length);
            }

            //we shoudln't get past the end of the byte array unless it's malformed
            startIndex = bytes.Length;
            return new byte[0];
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
                    EncodeByteArray(objToEncode, ms);
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

        private static void EncodeNumber(long num, MemoryStream ms)
        {
            string str = string.Format("{0}{1}{2}", NumberStart, num, NumberEnd);
            ms.Write(Encoding.UTF8.GetBytes(str), 0, str.Length);
        }

        private static void EncodeList(List<object> lst, MemoryStream ms)
        {
            ms.WriteByte(ListStart);
            foreach(var obj in lst)
            {
                EncodeObject(obj, ms);
            }
            ms.WriteByte(ListEnd);
        }

        private static void EncodeDictionary(Dictionary<string, object> dict, MemoryStream ms)
        {
            ms.WriteByte(DictionaryStart);
            foreach(var obj in dict)
            {
                EncodeByteArray(Encoding.UTF8.GetBytes(obj.Key), ms);
                EncodeObject(obj.Value, ms);
            }
            ms.WriteByte(DictionaryEnd);
        }

        private static void EncodeByteArray(byte[] bytes, MemoryStream ms)
        {
            string length = string.Format("{0}", bytes.Length);
            ms.Write(Encoding.UTF8.GetBytes(length), 0, length.Length);
            ms.WriteByte(ByteArrayDivider);
            ms.Write(bytes, 0, bytes.Length);
        }

        #endregion
    }
}
