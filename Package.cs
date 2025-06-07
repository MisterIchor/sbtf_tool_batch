using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Diagnostics;
using System.Xml;

namespace sbtftool
{
    [DataContract]
    public class PackageFile
    {
        [DataMember] public string FilePath { get; private set; }
        [DataMember] public short Unknown1 { get; private set; }
        [DataMember] public short Unknown2 { get; private set; }
        [DataMember] public int Unknown3 { get; private set; }

        public int Size { get; set; }
        public int ComputedOffset { get; set; }

        public PackageFile(string filePath, short unknown1, short unknown2, int unknown3)
        {
            FilePath = filePath;
            Unknown1 = unknown1;
            Unknown2 = unknown2;
            Unknown3 = unknown3;
        }
    }

    [DataContract]
    public class Package
    {
        [DataMember] public List<PackageFile> Files { get; private set; }

        public static bool VerifyFile(Stream file)
        {
            using BinaryReader reader = new BinaryReader(file);
            int magic = reader.ReadInt32();

            return magic == 0x6E776660;
        }

        public static Package ReadPackageFromNwf(Stream file)
        {
            using BinaryReader reader = new BinaryReader(file, Encoding.ASCII, true);
            var files = new List<PackageFile>();

            try
            {
                int magic = reader.ReadInt32();
                Console.WriteLine(magic);
                if (magic != 0x6E776660)
                {
                    throw new PackageParseException("Magic number is incorrect.");
                }

                // Unknown value
                var _ = reader.ReadInt32();
                var fileCount = reader.ReadInt32();
                Trace.WriteLine($"File count: {fileCount}");

                for (int i = 0; i < fileCount; i++)
                {
                    var fileNameLength = reader.ReadInt32();
                    var fileNameBytes = reader.ReadBytes(fileNameLength);
                    var fileName = Encoding.UTF8.GetString(fileNameBytes);
                    Trace.WriteLine($"File name: {fileName}");

                    var unknown1 = reader.ReadInt16();
                    var unknown2 = reader.ReadInt16();
                    var unknown3 = reader.ReadInt32();
                    Trace.WriteLine($"\tUnknown values: {unknown1} {unknown2} {unknown3}");

                    var offset = reader.ReadInt32();
                    var size = reader.ReadInt32();
                    Trace.WriteLine($"\tOffset: {offset}, size: {size}");

                    files.Add(new PackageFile(fileName, unknown1, unknown2, unknown3)
                    {
                        ComputedOffset = offset,
                        Size = size
                    });
                }

                Trace.WriteLine("Done reading package file");

                return new Package { Files = files };
            }
            catch (IOException io)
            {
                throw new PackageParseException("Error while parsing package.", io);
            }
        }

        public static Package ReadPackageDefinitionFromXml(Stream input)
        {
            var ser = new DataContractSerializer(typeof(Package));
            return (Package)ser.ReadObject(input);
        }

        public void WritePackageDefinitionToXml(Stream output)
        {
            var ser = new DataContractSerializer(GetType());
            using var @out = XmlWriter.Create(output, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                CloseOutput = false
            });

            ser.WriteObject(@out, this);
        }

        public class PackageParseException : Exception
        {
            public PackageParseException()
            {
            }

            public PackageParseException(string message)
                : base(message)
            {
            }

            public PackageParseException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}
