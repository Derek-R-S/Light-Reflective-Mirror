using System;
using System.Collections.Generic;
using System.Text;

namespace LightReflectiveMirror
{
    public static class DataHandler
    {
        public static void WriteByte(this byte[] data, ref int position, byte value)
        {
            data[position] = value;
            position += 1;
        }

        public static byte ReadByte(this byte[] data, ref int position)
        {
            byte value = data[position];
            position += 1;
            return value;
        }

        public static void WriteBool(this byte[] data, ref int position, bool value)
        {
            unsafe
            {
                fixed(byte* dataPtr = &data[position])
                {
                    bool* valuePtr = (bool*)dataPtr;
                    *valuePtr = value;
                    position += 1;
                }
            }
        }

        public static bool ReadBool(this byte[] data, ref int position)
        {
            bool value = BitConverter.ToBoolean(data, position);
            position += 1;
            return value;
        }

        public static void WriteString(this byte[] data, ref int position, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // Incase string is null or empty, just write nothing.
                data.WriteInt(ref position, 0);
            }
            else
            {
                data.WriteInt(ref position, value.Length);
                for (int i = 0; i < value.Length; i++)
                    data.WriteChar(ref position, value[i]);
            }
        }

        public static string ReadString(this byte[] data, ref int position)
        {
            string value = default;

            int stringSize = data.ReadInt(ref position);

            for (int i = 0; i < stringSize; i++)
                value += data.ReadChar(ref position);

            return value;
        }

        public static void WriteBytes(this byte[] data, ref int position, byte[] value)
        {
            data.WriteInt(ref position, value.Length);
            for (int i = 0; i < value.Length; i++)
                data.WriteByte(ref position, value[i]);
        }

        public static byte[] ReadBytes(this byte[] data, ref int position)
        {
            int byteSize = data.ReadInt(ref position);

            byte[] value = new byte[byteSize];

            for (int i = 0; i < byteSize; i++)
                value[i] = data.ReadByte(ref position);

            return value;
        }

        public static void WriteChar(this byte[] data, ref int position, char value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &data[position])
                {
                    char* valuePtr = (char*)dataPtr;
                    *valuePtr = value;
                    position += 2;
                }
            }
        }

        public static char ReadChar(this byte[] data, ref int position)
        {
            char value = BitConverter.ToChar(data, position);
            position += 2;
            return value;
        }

        public static void WriteInt(this byte[] data, ref int position, int value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &data[position])
                {
                    int* valuePtr = (int*)dataPtr;
                    *valuePtr = value;
                    position += 4;
                }
            }
        }

        public static int ReadInt(this byte[] data, ref int position)
        {
            int value = BitConverter.ToInt32(data, position);
            position += 4;
            return value;
        }
    }
}
