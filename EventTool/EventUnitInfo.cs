using System;
using System.IO;

namespace EventTool
{
    public class EventUnitInfo
    {
        public string EventName;
        public TimeSection[] TimeSections;
        public string[] IntParams;
        public float[] FloatParams;
        public string[] StringParams;

        internal void Read(BinaryReader reader, Dictionaries dictionaries)
        {
            uint eventNameHash = reader.ReadUInt32();
            EventName = dictionaries.EventNameDictionary.TryGetValue(eventNameHash, out string eventName) ? eventName : eventNameHash.ToString();
            Console.WriteLine($"Event name is {EventName}");
            byte timeSectionInfo = reader.ReadByte();
            byte timeSectionCount = (byte)(timeSectionInfo & 0b111111);
            TimeSections = new TimeSection[timeSectionCount];
            Console.WriteLine($"Event has {timeSectionCount} time sections");
            byte timeSectionFormat = (byte)((timeSectionInfo & 0b11000000) >> 6);
            byte intParamCount = reader.ReadByte();
            IntParams = new string[intParamCount];
            Console.WriteLine($"Event has {intParamCount} int params");
            byte floatParamCount = reader.ReadByte();
            FloatParams = new float[floatParamCount];
            Console.WriteLine($"Event has {floatParamCount} float params");
            byte stringParamCount = reader.ReadByte();
            StringParams = new string[stringParamCount];
            Console.WriteLine($"Event has {stringParamCount} string params");

            for (int i = 0; i < timeSectionCount; i++)
            {
                if (timeSectionFormat == 0)
                {
                    TimeSections[i] = new TimeSectionInt();
                    TimeSections[i].Read(reader);
                    Console.WriteLine($"Time section format is 0/int");
                }
                else if (timeSectionFormat == 1)
                {
                    TimeSections[i] = new TimeSectionShort();
                    TimeSections[i].Read(reader);
                    Console.WriteLine($"Time section format is 1/short");
                }
                else if (timeSectionFormat == 2)
                {
                    TimeSections[i] = new TimeSectionByte();
                    TimeSections[i].Read(reader);
                    Console.WriteLine($"Time section format is 2/byte");
                }
            }

            reader.AlignStream(4);

            for (int i = 0; i < intParamCount; i++)
            {
                uint intParamHash = reader.ReadUInt32();
                IntParams[i] = dictionaries.IntDictionary.TryGetValue(intParamHash, out string intParam) ? intParam : intParamHash.ToString();
                Console.WriteLine($"Int param #{i} is {IntParams[i]}");
            }

            for (int i = 0; i < floatParamCount; i++)
            {
                FloatParams[i] = reader.ReadSingle();
                Console.WriteLine($"Float param #{i} is {FloatParams[i]}");
            }

            for (int i = 0; i < stringParamCount; i++)
            {
                ulong stringHash = reader.ReadUInt64();
                StringParams[i] = dictionaries.StringDictionary.TryGetValue(stringHash, out string stringParam) ? stringParam : stringHash.ToString();
                Console.WriteLine($"String param #{i} is {StringParams[i]}");
            }
        }

        internal void Write(BinaryWriter writer, byte timeSectionType, Tuple<int, int> packetBounds)
        {
            uint eventNameHash = uint.TryParse(EventName, out eventNameHash) ? eventNameHash : StrCode.StrCode32(EventName);
            writer.Write(eventNameHash);
            byte timeSectionInfo = (byte)((timeSectionType & 0b11 << 6) | (byte)TimeSections.Length & 0b111111);
            writer.Write(timeSectionInfo);
            writer.Write((byte)IntParams.Length);
            writer.Write((byte)FloatParams.Length);
            writer.Write((byte)StringParams.Length);
            foreach (TimeSection timeSection in TimeSections)
            {
                timeSection.Write(writer,timeSectionType, packetBounds);
            }

            writer.AlignStream(4);

            foreach (string intParam in IntParams)
            {
                uint intParamHash = uint.TryParse(intParam, out intParamHash) ? intParamHash : StrCode.StrCode32(intParam);
                writer.Write(intParamHash);
            }

            foreach (float floatParam in FloatParams)
            {
                writer.Write(floatParam);
            }

            foreach (string stringParam in StringParams)
            {
                ulong stringParamHash = ulong.TryParse(stringParam, out stringParamHash) ? stringParamHash : StrCode.StrCode64(stringParam);
                writer.Write(stringParamHash);
            }
        }
    }
}
