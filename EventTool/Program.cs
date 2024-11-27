using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;

namespace EventTool
{
    public class Dictionaries
    {
        public Dictionary<uint, string> categoryNameDictionary;
        public Dictionary<uint, string> eventNameDictionary;
        public Dictionary<ulong, string> stringDictionary;
        public Dictionary<uint, string> intDictionary;
    }
    internal class Program
    {
        private const string categoryNameDictionaryName = "evf_categoryName.txt";
        private const string eventNameDictionaryName = "evf_eventName.txt";
        private const string stringDictionaryName = "evf_stringParam.txt";
        private const string intDictionaryName = "evf_intParam.txt";
        private const string deserializeExtension = ".evf.json";
        static void Main(string[] args)
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Dictionaries dictionaries = new Dictionaries();

            string categoryNameDictionaryDirectory = directory + "\\" + categoryNameDictionaryName;
            string eventNameDictionaryDirectory = directory + "\\" + eventNameDictionaryName;
            string stringDictionaryDirectory = directory + "\\" + stringDictionaryName;
            string intDictionaryDirectory = directory + "\\" + intDictionaryName;

            dictionaries.categoryNameDictionary = CreateDictionary32(categoryNameDictionaryDirectory);
            dictionaries.eventNameDictionary = CreateDictionary32(eventNameDictionaryDirectory);
            dictionaries.stringDictionary = CreateDictionary64(stringDictionaryDirectory);
            dictionaries.intDictionary = CreateDictionary32(intDictionaryDirectory);

            foreach (var arg in args)
            {
                if (File.Exists(arg))
                {
                    var filePath = arg;

                    if (filePath.Contains(deserializeExtension))
                    {
                        string deserializedFilePath = filePath;
                        string serializedFilePath = deserializedFilePath.Replace(deserializeExtension, string.Empty);
                        if (!File.Exists(serializedFilePath))
                        {
                            Console.WriteLine($"{serializedFilePath} doesn't exist, can't inject serialized events!");
                            return;
                        }
                        BinaryReader reader = new BinaryReader(new FileStream(serializedFilePath, FileMode.Open));
                        if (ReadDemoPacket(reader))
                        {
                            Tuple<int, int> packetBounds = new Tuple<int, int>(0,-1);
                            long eventPosition = reader.BaseStream.Position;
                            long endOfPacketPosition = 0x14;
                            reader.BaseStream.Position = 0x10;
                            uint demoPacketType = reader.ReadUInt32();
                            if (demoPacketType==0)
                            {
                                endOfPacketPosition += 0x10;
                                reader.BaseStream.Position = 0x18;
                                int startFrame = reader.ReadInt32();
                                int packetFrameLength = reader.ReadInt32();
                                packetBounds = new Tuple<int,int>(startFrame, startFrame+packetFrameLength);
                            }
                            reader.Close();

                            BinaryWriter writer = new BinaryWriter(new FileStream(serializedFilePath, FileMode.Open));
                            File.Copy(serializedFilePath, serializedFilePath+".o",true);
                            SerializePacket(deserializedFilePath, writer, eventPosition, endOfPacketPosition, packetBounds);
                        }
                    }
                    else
                    {
                        BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open));
                        if (ReadDemoPacket(reader))
                        {
                            long eventPosition = reader.BaseStream.Position;
                            if (eventPosition == 0)
                            {
                                EvpData evpData = new EvpData();
                                File.WriteAllText(Path.GetFileNameWithoutExtension(filePath) + Path.GetExtension(filePath) + deserializeExtension, JsonConvert.SerializeObject(evpData, Newtonsoft.Json.Formatting.Indented));
                            }
                            else
                                ReadEvpData(reader, filePath, dictionaries);
                        }
                    }
                }
            }
            //Console.Read();
        }
        static void SerializePacket(string filePath, BinaryWriter writer, long eventPosition, long endOfPacketPositon, Tuple<int, int> packetBounds)
        {
            EvpData evpData = JsonConvert.DeserializeObject<EvpData>(File.ReadAllText(filePath));

            bool isWriteEmpty = false;

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

            long endOfPacket = writer.BaseStream.Position;
            if (isWriteEmpty)
                endOfPacket = writer.BaseStream.Length;

            writer.BaseStream.Position = 4;
            writer.Write((uint)endOfPacket);

            writer.BaseStream.Position = endOfPacketPositon;
            writer.Write((uint)endOfPacket - (0x10));
            if (writer.BaseStream.Position==0x28)
            {
                if (!isWriteEmpty)
                    writer.Write((uint)eventPosition - (0x10));
                else
                    writer.WriteZeroes(4);
            }
            else if (writer.BaseStream.Position==0x18)
            {
                writer.WriteZeroes(4);
            }
            writer.BaseStream.Position = endOfPacket;
            writer.BaseStream.SetLength(endOfPacket);
            writer.BaseStream.Close();
        }
        static void ReadEvpData(BinaryReader reader, string filePath, Dictionaries dictionaries)
        {
            EvpData evpData = new EvpData();
            evpData.Read(reader, dictionaries);
            File.WriteAllText(Path.GetFileNameWithoutExtension(filePath) + Path.GetExtension(filePath) + deserializeExtension, JsonConvert.SerializeObject(evpData, Newtonsoft.Json.Formatting.Indented));
        }
        static bool ReadDemoPacket(BinaryReader reader)
        {
            uint signature = reader.ReadUInt32();
            if (signature != 1330464068)
            {
                Console.WriteLine($"{signature} isn't DEMO packet signature!!!");
                return false;
            }
            uint packetSize = reader.ReadUInt32();
            Console.WriteLine($"packet size: {packetSize} bytes");
            double packetStartTime = reader.ReadDouble();
            Console.WriteLine($"packet start time: {packetStartTime} seconds");
            long demoPacketHeaderStart = reader.BaseStream.Position;
            uint demoPacketType = reader.ReadUInt32();
            if (demoPacketType == 0)
            {
                uint demoPacketFlags = reader.ReadUInt32();
                int frameStart = reader.ReadInt32();
                int frameEnd = reader.ReadInt32();
                int segmentCount = reader.ReadInt32();
            }
            uint offsetToPacketEnd = reader.ReadUInt32();
            uint offsetToEvents = reader.ReadUInt32();
            if (demoPacketType==2)
            {
                Console.WriteLine($"Node packets can't have events");
                return false;
            }
            if (0==offsetToEvents||offsetToEvents>=packetSize)
            {
                Console.WriteLine($"packet event offset {offsetToEvents} is zero or out of bounds of {packetSize}!!!");
                reader.BaseStream.Position = 0;
                return true;
            }
            reader.BaseStream.Position = demoPacketHeaderStart + offsetToEvents;
            return true;
        }
        public static Dictionary<ulong, string> CreateDictionary64(string dictionaryFilePath)
        {
            Dictionary<ulong, string> stringDictionary = new Dictionary<ulong, string>();
            stringDictionary.Add(StrCode.StrCode64(string.Empty), string.Empty);

            if (File.Exists(dictionaryFilePath))
            {
                string[] strings = File.ReadAllLines(dictionaryFilePath).Distinct().ToArray();
                foreach (string value in strings)
                {
                    stringDictionary[StrCode.StrCode64(value)] = value;
                }
            };

            return stringDictionary;
        }
        public static Dictionary<uint, string> CreateDictionary32(string dictionaryFilePath)
        {
            Dictionary<uint, string> stringDictionary = new Dictionary<uint, string>();
            stringDictionary.Add(StrCode.StrCode32(string.Empty), string.Empty);

            if (File.Exists(dictionaryFilePath))
            {
                string[] strings = File.ReadAllLines(dictionaryFilePath).Distinct().ToArray();
                foreach (string value in strings)
                {
                    stringDictionary[StrCode.StrCode32(value)]=value;
                }
            };

            return stringDictionary;
        }
    }
}
