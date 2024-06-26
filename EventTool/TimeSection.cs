using System;
using System.IO;

namespace EventTool
{
    public class TimeSection
    {
        public int StartFrame;
        public int EndFrame;
        public virtual void Read(BinaryReader reader)
        {

        }

        public void Write(BinaryWriter writer, byte timeSectionType)
        {
            StartFrame = StartFrame < 0 ? StartFrame : (int)(StartFrame & 0xBFFFFFFF);
            EndFrame = EndFrame < 0 ? EndFrame : (int)(EndFrame & 0xBFFFFFFF);
            if (timeSectionType==0)
            {
                writer.Write((int)StartFrame);
                writer.Write((int)EndFrame);
            }
            else if (timeSectionType == 1)
            {
                writer.Write((short)StartFrame);
                writer.Write((short)EndFrame);
            }
            else if (timeSectionType == 2)
            {
                writer.Write((byte)StartFrame);
                writer.Write((byte)EndFrame);
            }
            else if (timeSectionType == 3)
            {

            }
        }
    }
    public class TimeSectionShort : TimeSection
    {
        public new short StartFrame;
        public new short EndFrame;
        public override void Read(BinaryReader reader)
        {
            StartFrame = reader.ReadInt16();
            EndFrame = reader.ReadInt16();
            Console.WriteLine($"Time section starts at {StartFrame} and ends at {EndFrame}");
        }
    }
    public class TimeSectionByte : TimeSection
    {
        public new byte StartFrame;
        public new byte EndFrame;
        public override void Read(BinaryReader reader)
        {
            StartFrame = reader.ReadByte();
            EndFrame = reader.ReadByte();
            Console.WriteLine($"Time section starts at {StartFrame} and ends at {EndFrame}");
        }
    }
    public class TimeSectionInt : TimeSection
    {
        public new int StartFrame;
        public new int EndFrame;
        public override void Read(BinaryReader reader)
        {
            StartFrame = reader.ReadInt32();
            StartFrame = StartFrame < 0 ? StartFrame : (int)(StartFrame & 0xBFFFFFFF);
            EndFrame = reader.ReadInt32();
            EndFrame = EndFrame < 0 ? EndFrame : (int)(EndFrame & 0xBFFFFFFF);
            Console.WriteLine($"Time section starts at {StartFrame} and ends at {EndFrame}");
        }
    }
}
