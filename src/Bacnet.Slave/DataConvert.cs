using System;
namespace Bacnet.Slave
{
    public static class DataConvert
    {
        public static float ToFloat(this ushort[] args)
        {

            if (args.Length != 2)
                throw new ArgumentException("Input Array length invalid - Array langth must be '2'");

            var highValue = BitConverter.IsLittleEndian ? args[1] : args[0];
            var lowValue = BitConverter.IsLittleEndian ? args[0] : args[1];


            byte[] highRegisterBytes = BitConverter.GetBytes(highValue);
            byte[] lowRegisterBytes = BitConverter.GetBytes(lowValue);

            byte[] doubleBytes = {
                         highRegisterBytes[0],
                         highRegisterBytes[1],
                         lowRegisterBytes[0],
                         lowRegisterBytes[1],
         };

            return BitConverter.ToSingle(doubleBytes, 0);
        }

        public static ushort[] ToUnsignedShortArray(this float fValue)
        {
            var value = BitConverter.SingleToInt32Bits(fValue);
            ushort low = (ushort)(value & 0x0000ffff);
            ushort high = (ushort)((value & 0xffff0000) >> 16);

            var highRegister = BitConverter.IsLittleEndian ? low : high;
            var lowRegister = BitConverter.IsLittleEndian ? high : low;

            var registers = new ushort[2] { lowRegister, highRegister };
            return registers;
        }
    }
}

