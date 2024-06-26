using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventTool
{
    public static class Extensions
    {
        public static void ReadZeroes(this BinaryReader reader, int count)
        {
            byte[] zeroes = reader.ReadBytes(count);
            foreach (byte zero in zeroes)
            {
                if (zero != 0)
                {
                    Console.WriteLine($"Padding at {reader.BaseStream.Position} isn't zero!!!");
                    throw new Exception();
                }
            }
        } //WriteZeroes
        public static void WriteZeroes(this BinaryWriter writer, int count)
        {
            byte[] array = new byte[count];

            writer.Write(array);
        } //WriteZeroes
        public static void AlignStream(this BinaryReader reader, byte div)
        {
            long pos = reader.BaseStream.Position;
            if (pos % div != 0)
                reader.BaseStream.Position += div - pos % div;
        }
        public static void AlignStream(this BinaryWriter writer, byte div)
        {
            long pos = writer.BaseStream.Position;
            if (pos % div != 0)
                writer.WriteZeroes((int)(div - pos % div));
        }
    }
}
