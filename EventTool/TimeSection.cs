using System;
using System.IO;
using Newtonsoft.Json;

namespace EventTool
{
    public class TimeSection
    {
        public int StartFrame;
        [JsonIgnore]
        public bool IsStartOutOfBounds;
        public int EndFrame;
        [JsonIgnore]
        public bool IsEndOutOfBounds;
        public virtual void Read(BinaryReader reader)
        {

        }

        public void Write(BinaryWriter writer, byte timeSectionType, Tuple<int,int> packetBounds)
        {
            StartFrame = StartFrame < 0 ? StartFrame : (int)(StartFrame & 0xBFFFFFFF);
            EndFrame = EndFrame < 0 ? EndFrame : (int)(EndFrame & 0xBFFFFFFF);
            if (packetBounds.Item1>0 && StartFrame <= packetBounds.Item1)
                StartFrame |= 0x40000000;
            if (EndFrame > packetBounds.Item2)
                EndFrame |= 0x40000000;

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
            IsStartOutOfBounds = (StartFrame & 0x40000000)!=0;
            StartFrame = StartFrame < 0 ? StartFrame : (int)(StartFrame & 0xBFFFFFFF);
            EndFrame = reader.ReadInt32();
            IsEndOutOfBounds = (EndFrame>0)&(EndFrame & 0x40000000)!=0;
            EndFrame = EndFrame < 0 ? EndFrame : (int)(EndFrame & 0xBFFFFFFF);
            Console.WriteLine($"Time section starts at {StartFrame} and ends at {EndFrame}");
        }
    }
}
