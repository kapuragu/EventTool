using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EventTool
{
    public class Dictionaries
    {
        public Dictionary<uint, string> CategoryNameDictionary;
        public Dictionary<uint, string> EventNameDictionary;
        public Dictionary<ulong, string> StringDictionary;
        public Dictionary<uint, string> IntDictionary;
    }
    internal static class Program
    {
        private const string CategoryNameDictionaryName = "evf_categoryName.txt";
        private const string EventNameDictionaryName = "evf_eventName.txt";
        private const string StringDictionaryName = "evf_stringParam.txt";
        private const string IntDictionaryName = "evf_intParam.txt";
        private const string DeserializeExtension = ".evf.json";
        private const string StreamExtension = ".fsm";

        private static void Main(string[] args)
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var dictionaries = new Dictionaries();

            var categoryNameDictionaryDirectory = directory + "\\" + CategoryNameDictionaryName;
            var eventNameDictionaryDirectory = directory + "\\" + EventNameDictionaryName;
            var stringDictionaryDirectory = directory + "\\" + StringDictionaryName;
            var intDictionaryDirectory = directory + "\\" + IntDictionaryName;

            dictionaries.CategoryNameDictionary = CreateDictionary32(categoryNameDictionaryDirectory);
            dictionaries.EventNameDictionary = CreateDictionary32(eventNameDictionaryDirectory);
            dictionaries.StringDictionary = CreateDictionary64(stringDictionaryDirectory);
            dictionaries.IntDictionary = CreateDictionary32(intDictionaryDirectory);

            foreach (var arg in args)
            {
                if (!File.Exists(arg)) continue;

                if (arg.Contains(DeserializeExtension))
                {
                    var serializedFilePath = arg.Replace(DeserializeExtension, string.Empty);
                    if (!File.Exists(serializedFilePath))
                    {
                        Console.WriteLine($"{serializedFilePath} doesn't exist, can't inject serialized events!");
                        return;
                    }
                    var reader = new BinaryReader(new FileStream(serializedFilePath, FileMode.Open));
                    if (!ReadDemoPacket(reader,true)) continue;
                    var packetBounds = new Tuple<int, int>(-1,-1);
                    var eventPosition = reader.BaseStream.Position;
                    long endOfPacketPosition = 0x14;
                    reader.BaseStream.Position = 0x10;
                    var demoPacketType = reader.ReadUInt32();
                    var flags = reader.ReadUInt32();
                    if (demoPacketType==0)
                    {
                        endOfPacketPosition += 0x10;
                        reader.BaseStream.Position = 0x18;
                        var startFrame = reader.ReadInt32();
                        var packetFrameLength = reader.ReadInt32();
                        packetBounds = new Tuple<int,int>(startFrame, startFrame+packetFrameLength);
                    }
                    reader.Close();

                    var writer = new BinaryWriter(new FileStream(serializedFilePath, FileMode.Open));
                    File.Copy(serializedFilePath, serializedFilePath+".o",true);
                    SerializePacket(arg, writer, eventPosition, endOfPacketPosition, packetBounds, flags);
                }
                else
                {
                    var reader = new BinaryReader(new FileStream(arg, FileMode.Open));
                    var evpData = new EvpData();
                    var newEvents = new List<EventUnitInfo>();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        if (!ReadDemoPacket(reader,false)) continue;
                        var evpPacket = ReadEvpData(reader, dictionaries);
                        if (evpData.CategoryName == null)
                            evpData.CategoryName = evpPacket.CategoryName;
                        
                        foreach (var evpEvent in evpPacket.Events)
                        {
                            var isInBounds = true;
                            if (evpEvent.TimeSections.First() != null)
                            {
                                if (evpEvent.TimeSections.First().IsStartOutOfBounds)
                                {
                                    isInBounds = false;
                                }
                            }
                            if (isInBounds)
                            {
                                newEvents.Add(evpEvent);
                            }
                            else
                            {
                                Console.WriteLine($"Event #{Array.IndexOf(evpPacket.Events,evpEvent)} {evpEvent.EventName} is out of bounds!!!");
                            }
                        }
                        reader.AlignStream(0x10);
                    }
                    evpData.Events=newEvents.ToArray();
                    File.WriteAllText(Path.GetFileNameWithoutExtension(arg) + Path.GetExtension(arg) + DeserializeExtension, JsonConvert.SerializeObject(evpData, Newtonsoft.Json.Formatting.Indented));
                }
            }
            Console.Read();
        }

        private static void SerializePacket(string filePath, BinaryWriter writer, long eventPosition, long endOfPacketPositon, Tuple<int, int> packetBounds, uint flags)
        {
            var evpData = JsonConvert.DeserializeObject<EvpData>(File.ReadAllText(filePath));

            var isWriteEmpty = false;

            if (evpData.CategoryName==null||evpData.Events==null)
            {
                isWriteEmpty = true;
                eventPosition = 0;
            }

            if (eventPosition==0)
            {
                eventPosition = writer.BaseStream.Length;
            }

            if (!isWriteEmpty)
            {
                writer.BaseStream.Position = eventPosition;

                evpData.Write(writer, 0, packetBounds);

                writer.AlignStream(0x10);
            }

            var endOfPacket = writer.BaseStream.Position;
            if (isWriteEmpty)
                endOfPacket = writer.BaseStream.Length;

            writer.BaseStream.Position = 4;
            writer.Write((uint)endOfPacket);

            writer.BaseStream.Position = 0x14;
            var newFlags = flags;
            if (!isWriteEmpty)
            {
                newFlags |= 0b1000;
            }
            else
            {
                newFlags ^= 0b1000;
            }
            //writer.Write(newFlags);

            writer.BaseStream.Position = endOfPacketPositon;
            writer.Write((uint)endOfPacket - (0x10));
            switch (writer.BaseStream.Position)
            {
                case 0x28 when !isWriteEmpty:
                    writer.Write((uint)eventPosition - (0x10));
                    break;
                case 0x28:
                case 0x18:
                    writer.WriteZeroes(4);
                    break;
            }
            writer.BaseStream.Position = endOfPacket;
            writer.BaseStream.SetLength(endOfPacket);
            writer.BaseStream.Close();
        }

        private static EvpData ReadEvpData(BinaryReader reader, Dictionaries dictionaries)
        {
            var evpData = new EvpData();
            evpData.Read(reader, dictionaries);
            return evpData;
        }

        private static bool ReadDemoPacket(BinaryReader reader, bool isWriteCheck)
        {
            var startOfPacket = reader.BaseStream.Position;
            Console.WriteLine($"packet start: @{startOfPacket}");
            var signature = reader.ReadUInt32();
            var packetSize = reader.ReadUInt32();
            Console.WriteLine($"packet size: {packetSize} bytes");
            if (signature != 1330464068)
            {
                Console.WriteLine($"{signature} isn't DEMO packet signature!!!");
                if (reader.BaseStream.Length - 8 < packetSize)
                {
                    Console.WriteLine($"Packet size exceeds file Length!!!");
                }
                else
                {
                    Console.WriteLine($"Skipping {signature} packet...");
                    reader.BaseStream.Position += packetSize - 8;
                }
                return false;
            }
            var packetStartTime = reader.ReadDouble();
            Console.WriteLine($"packet start time: {packetStartTime} seconds");
            var demoPacketHeaderStart = reader.BaseStream.Position;
            var demoPacketType = reader.ReadUInt32();
            if (demoPacketType == 0)
            {
                var demoPacketFlags = reader.ReadUInt32();
                var frameStart = reader.ReadInt32();
                var frameEnd = reader.ReadInt32();
                var segmentCount = reader.ReadInt32();
            }
            var offsetToPacketEnd = reader.ReadUInt32();
            var offsetToEvents = reader.ReadUInt32();
            switch (demoPacketType)
            {
                case 1:
                    Console.WriteLine($"Node packets can't have events");
                    return false;
                case 2:
                    return true;
            }

            if (0==offsetToEvents||offsetToEvents >= packetSize && demoPacketType==0)
            {
                Console.WriteLine($"packet event offset {offsetToEvents} is zero or out of bounds of {packetSize}!!!");
                if (isWriteCheck)
                {
                    reader.BaseStream.Position = startOfPacket;
                    return true;
                }
                else
                {
                    reader.BaseStream.Position = startOfPacket + packetSize;
                    return false;
                }
            }
            reader.BaseStream.Position = demoPacketHeaderStart + offsetToEvents;
            return true;
        }

        private static Dictionary<ulong, string> CreateDictionary64(string dictionaryFilePath)
        {
            var stringDictionary = new Dictionary<ulong, string> { { StrCode.StrCode64(string.Empty), string.Empty } };

            if (File.Exists(dictionaryFilePath))
            {
                var strings = File.ReadAllLines(dictionaryFilePath).Distinct().ToArray();
                foreach (var value in strings)
                {
                    stringDictionary[StrCode.StrCode64(value)] = value;
                }
            }

            return stringDictionary;
        }

        private static Dictionary<uint, string> CreateDictionary32(string dictionaryFilePath)
        {
            var stringDictionary = new Dictionary<uint, string> { { StrCode.StrCode32(string.Empty), string.Empty } };

            if (!File.Exists(dictionaryFilePath)) return stringDictionary;
            var strings = File.ReadAllLines(dictionaryFilePath).Distinct().ToArray();
            foreach (var value in strings)
            {
                stringDictionary[StrCode.StrCode32(value)]=value;
            }

            return stringDictionary;
        }
    }
}
