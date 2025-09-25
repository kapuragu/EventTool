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

            var isGz = false;

            foreach (var arg in args)
            {
                if (arg.ToLower() == "-gz")
                {
                    isGz = true;
                }
                
                if (!File.Exists(arg)) continue;

                if (arg.Contains(DeserializeExtension))
                {
                    var serializedFilePath = arg.Replace(DeserializeExtension, string.Empty);
                    if (!File.Exists(serializedFilePath))
                    {
                        Console.WriteLine($"{serializedFilePath} doesn't exist, can't inject serialized events!");
                        continue;
                    }
                    
                    if (arg.Contains(StreamExtension))
                    {
                        SerializeStream(serializedFilePath,arg,isGz);
                        continue;
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
            //Console.Read();
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
                if ((newFlags & 0b1000) == 0)
                {
                    newFlags |= 0b1000;
                }

                if ((newFlags & 0b1) != 0)
                {
                    newFlags ^= 0b1;
                }
            }
            writer.Write(newFlags);

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
            if (signature == 541347397)
            {
                Console.WriteLine($"{signature} is END packet signature!!!");
                reader.BaseStream.Position = reader.BaseStream.Length;
                return false;
            }
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
                    reader.BaseStream.Position = startOfPacket + packetSize;
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

        private static void SerializeStream(string serializedFilePath, string arg, bool isGz)
        {
            File.Move(serializedFilePath, serializedFilePath+".o");

            var packetBounds = new Tuple<int, int>(-1,-1);
            var isNextEvents = false;
                        
            var evpFullData = JsonConvert.DeserializeObject<EvpData>(File.ReadAllText(arg));
                        
            var reader = new BinaryReader(new FileStream(serializedFilePath+".o", FileMode.Open));
            var isEnd = false;
            var fileMode = FileMode.Create;
            var writer = new BinaryWriter(new FileStream(serializedFilePath+".n", fileMode));
            while (!isEnd && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                Console.WriteLine($"read@{reader.BaseStream.Position} Start");
                var ignorePacket = false;
                var startOfPacket = reader.BaseStream.Position;
                var packetType = reader.ReadUInt32();
                var bodySize = reader.ReadInt32();
                var time = reader.ReadDouble();
                reader.BaseStream.Position=startOfPacket;
                var data = reader.ReadBytes(bodySize);
                reader.BaseStream.Position=startOfPacket;

                uint chunkFlags=0;

                Console.WriteLine($"read@{reader.BaseStream.Position} write@{writer.BaseStream.Position} packet type {packetType}");
                if (packetType == 1330464068)
                {
                    reader.BaseStream.Position=startOfPacket+0x10;
                    var demoChunkType = reader.ReadUInt32();
                    if (demoChunkType == 0)
                    {
                        chunkFlags = reader.ReadUInt32();
                        isNextEvents = (chunkFlags & 0x1)!=0;
                        var frameStart = reader.ReadInt32();
                        var frameEnd = reader.ReadInt32();
                        packetBounds = new Tuple<int, int>(frameStart,frameStart+frameEnd);
                        var segmentCount = reader.ReadUInt32();
                    }
                    else if (demoChunkType == 2)
                    {
                        if (isNextEvents)
                        {
                            if (!isGz)
                            {
                                ignorePacket = true;
                                //move these events back to the segment chunk. don't write this event chunk
                            }
                        }
                        Console.WriteLine($"isNextEvents={isNextEvents},isGz={isGz},ignorePacket={ignorePacket},");
                    }

                    var offsetToChunkEnd = reader.ReadUInt32();
                    var offsetToEvents = reader.ReadUInt32();

                    long eventPosition=0;
                    if (0==offsetToEvents||offsetToEvents >= bodySize && demoChunkType==0)
                    {
                        Console.WriteLine($"packet event offset {offsetToEvents} is zero or out of bounds of {bodySize}!!!");
                        
                        eventPosition = bodySize;
                    }
                    else
                    {
                        eventPosition = offsetToEvents + 0x10;
                    }
                    
                    if (demoChunkType == 1)
                    {
                        reader.BaseStream.Position += 4;
                    }

                    if (!ignorePacket)
                    {
                        var startOfWritePacket = writer.BaseStream.Position;
                        writer.Write(data);
                        writer.AlignStream(0x10);
                        var endOfWritePacket = writer.BaseStream.Position;
                                    
                        if (demoChunkType == 0||demoChunkType == 2)
                        {
                            Console.WriteLine($"packetBounds={packetBounds.Item1},{packetBounds.Item2}");
                            var evpUnits = new List<EventUnitInfo>();
                            foreach (var evpUnit in evpFullData.Events)
                            {
                                var newEvpUnit = evpUnit;
                                foreach (var timeSection in evpUnit.TimeSections)
                                {
                                    var timeSectionIndex = Array.IndexOf(evpUnit.TimeSections,timeSection);

                                    if ((timeSection.StartFrame & 0x40000000) != 0)
                                    {
                                        timeSection.StartFrame = timeSection.StartFrame < 0 ? timeSection.StartFrame : (int)(timeSection.StartFrame & 0xBFFFFFFF);
                                    }
                                    if ((timeSection.EndFrame & 0x40000000) != 0)
                                    {
                                        timeSection.EndFrame = timeSection.EndFrame < 0 ? timeSection.EndFrame : (int)(timeSection.EndFrame & 0xBFFFFFFF);
                                    }
                                    
                                    newEvpUnit.TimeSections[timeSectionIndex].IsStartOutOfBounds = false;
                                    if ((packetBounds.Item1 > 0 && timeSection.StartFrame <= packetBounds.Item1)
                                        || timeSection.StartFrame > packetBounds.Item2)
                                    {
                                        newEvpUnit.TimeSections[timeSectionIndex].IsStartOutOfBounds = true;
                                    }

                                    newEvpUnit.TimeSections[timeSectionIndex].IsEndOutOfBounds = false;
                                    if ((timeSection.EndFrame > packetBounds.Item2)
                                        || timeSection.EndFrame < packetBounds.Item1)
                                    {
                                        newEvpUnit.TimeSections[timeSectionIndex].IsEndOutOfBounds = true;
                                    }

                                    if (!newEvpUnit.TimeSections[timeSectionIndex].IsStartOutOfBounds ||
                                        !newEvpUnit.TimeSections[timeSectionIndex].IsEndOutOfBounds)
                                    {
                                        //Console.WriteLine($"startFrame={timeSection.StartFrame},endFrame={timeSection.EndFrame},packetBounds={packetBounds.Item1},{packetBounds.Item2}");
                                        evpUnits.Add(newEvpUnit);
                                        break;
                                    }
                                    else
                                    {
                                        //Console.WriteLine($"NO startFrame={timeSection.StartFrame},endFrame={timeSection.EndFrame},packetBounds={packetBounds.Item1},{packetBounds.Item2}");
                                    }
                                }
                            }

                            var eventsArray = evpUnits.ToArray();
                            Console.WriteLine($"eventsArray.Length {eventsArray.Length}");
                            var isNoEvents = eventsArray.Length == 0 || (isGz && isNextEvents && demoChunkType == 0);
                            var evpData = new EvpData
                            {
                                CategoryName = "Normal",
                                Events = eventsArray
                            };
                            
                            if (isNoEvents)
                            {
                                eventPosition = 0;
                            }

                            writer.BaseStream.Position = startOfWritePacket;
                            
                            if (eventPosition==0 && demoChunkType==0)
                            {
                                eventPosition = endOfWritePacket-startOfWritePacket;
                            }
                            
                            if (demoChunkType==2 && isGz && isNextEvents)
                            {
                                eventPosition = 0x1c;
                            }

                            if (!isNoEvents || (isGz && isNextEvents && demoChunkType == 2))
                            {
                                //isNextEvents = false;
                                writer.BaseStream.Position = startOfWritePacket+eventPosition;
                                
                                evpData.Write(writer, 0, packetBounds);

                                writer.AlignStream(0x10);
                            }

                            var endOfPacket = writer.BaseStream.Position-startOfWritePacket;
                            if (isNoEvents)
                                endOfPacket = writer.BaseStream.Length-startOfWritePacket;

                            writer.BaseStream.Position = startOfWritePacket+4;
                            writer.Write((uint)endOfPacket);

                            if (chunkFlags != 0 && demoChunkType==0)
                            {
                                writer.BaseStream.Position = startOfWritePacket+0x14;
                                var newFlags = chunkFlags;
                                if (!isNoEvents)
                                {
                                    if ((newFlags & 0b1000) == 0)
                                    {
                                        newFlags |= 0b1000;
                                    }

                                    if ((newFlags & 0b1) != 0)
                                    {
                                        newFlags ^= 0b1;
                                    }
                                }
                                else
                                {
                                    //newFlags ^= 0b1000;
                                }
                                writer.Write(newFlags);
                            }

                            long endOfPacketPosition = 0x14;
                            if (demoChunkType == 0)
                            {
                                endOfPacketPosition += 0x10;
                            }
                            writer.BaseStream.Position = startOfWritePacket + endOfPacketPosition;
                            writer.Write((uint)endOfPacket - 0x10);

                            writer.BaseStream.Position = startOfWritePacket + endOfPacketPosition + 0x4;
                            if (demoChunkType == 0 && !isNoEvents)
                            {
                                writer.Write((uint)eventPosition-0x10);
                            }
                            else
                            {
                                writer.WriteZeroes(4);
                            }
                            
                            writer.BaseStream.Position = startOfWritePacket + endOfPacket;
                            writer.AlignStream(0x10);
                        }
                    }

                }
                else
                {
                    var startOfWritePacket = writer.BaseStream.Position;
                    writer.Write(data);
                    writer.AlignStream(0x10);
                    if (packetType == 541347397)
                    {
                        isEnd = true;
                        reader.BaseStream.Position = startOfPacket + bodySize;
                        Console.WriteLine($"length={reader.BaseStream.Length} startOfPacket={startOfPacket} bodySize={bodySize} removal: {startOfPacket + bodySize} remainder: {reader.BaseStream.Length - (startOfPacket + bodySize)}");
                        var endData = reader.ReadBytes((int)((int)reader.BaseStream.Length - (startOfPacket + bodySize)));
                        writer.Write(endData);
                    }
                }

                reader.BaseStream.Position = startOfPacket + bodySize;
            }
        }
    }
}
