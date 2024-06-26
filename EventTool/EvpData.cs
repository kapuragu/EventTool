using System;
using System.IO;
using System.Runtime.Remoting.Messaging;

namespace EventTool
{
    public class EvpData
    {
        public string CategoryName;
        public EventUnitInfo[] Events;

        public void Read(BinaryReader reader, Dictionaries dictionaries)
        {
            long evpDataStart = reader.BaseStream.Position;
            uint categoryNameHash = reader.ReadUInt32();
            CategoryName = dictionaries.categoryNameDictionary.TryGetValue(categoryNameHash,out string categoryName) ? categoryName : categoryNameHash.ToString();
            Console.WriteLine($"Category name is {CategoryName}");
            ushort eventCount = reader.ReadUInt16();
            Console.WriteLine($"Contains {eventCount} events");
            Events = new EventUnitInfo[eventCount];
            ushort extraSectionOffset = reader.ReadUInt16();
            Console.WriteLine($"Extra section offset is {extraSectionOffset}");
            uint[] offsetToEvent = new uint[eventCount];
            long eventOffsetArrayStart = reader.BaseStream.Position;
            for (int i = 0; i < eventCount; i++)
            {
                reader.BaseStream.Position = eventOffsetArrayStart + (i * sizeof(uint));
                offsetToEvent[i] = reader.ReadUInt32();
                reader.BaseStream.Position = evpDataStart + offsetToEvent[i];
                Console.WriteLine($"Offset to event #{i} is {reader.BaseStream.Position}");
                EventUnitInfo eventUnitInfo = new EventUnitInfo();
                eventUnitInfo.Read(reader,dictionaries);
                Events[i] = eventUnitInfo;
            }
        }

        internal void Write(BinaryWriter writer, byte timeSectionType)
        {
            long eventsStartPositon = writer.BaseStream.Position;
            uint categoryNameHash = uint.TryParse(CategoryName, out categoryNameHash) ? categoryNameHash : StrCode.StrCode32(CategoryName);
            writer.Write(categoryNameHash);
            writer.Write((short)Events.Length);
            writer.Write((short)0);
            long[] eventOffsetArrayStarts = new long[Events.Length];
            uint[] eventOffsetArray = new uint[Events.Length];
            for (int i = 0; i < Events.Length; i++)
            {
                eventOffsetArrayStarts[i]=writer.BaseStream.Position;
                writer.Write((uint)0);
            }
            writer.WriteZeroes(0xC);
            for (int i = 0; i < Events.Length; i++)
            {
                long eventStartPositon = writer.BaseStream.Position;
                writer.BaseStream.Position = eventOffsetArrayStarts[i];
                writer.Write(eventStartPositon - eventsStartPositon);

                writer.BaseStream.Position = eventStartPositon;
                Events[i].Write(writer,timeSectionType);
            }
        }
    }
}
