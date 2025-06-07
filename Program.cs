using System;
using System.IO;
using System.Text;
using System.Linq;
using CommandLine;

namespace sbtftool
{
    class Program
    {
        [Verb("unpack", HelpText = "Unpack a .nwf file")]
        class UnpackOptions
        {
            [Value(0, Required = true, HelpText = "Path to your sbtf_pub.nwf file")]
            public string NwfFile { get; set; }

            [Option('o', "output", Default = "output", HelpText = "Directory to unpack files into")]
            public string Output { get; set; }
        }

        [Verb("schema", HelpText = "Save the structure of a sbtf_pub.nwf file as XML file (for use in repacking)")]
        class GenerateSchemaOptions
        {
            [Value(0, Required = true, HelpText = "sbtf_pub.nwf file to generate schema from")]
            public string NwfFile { get; set; }

            [Value(1, Default = "schema.xml", HelpText = "Output file location")]
            public string SchemaFile { get; set; }
        }

        [Verb("repack", HelpText = "Pack files back into .nwf")]
        class RepackOptions
        {
            [Value(0, Required = true, HelpText = "XML Schema file to use for the packing")]
            public string SchemaFile { get; set; }

            [Value(1, Default = "output", HelpText = "Source folder containing all the assets to put into the sbtf_pub.nwf file")]
            public string SourceFolder { get; set; }

            [Value(2, Default = "sbtf_pub.nwf", HelpText = "Path of the output file")]
            public string NwfFile { get; set; }
        }

        [Verb("verify", HelpText = "Checks if the file is a valid .nwf file that can be unpacked.")]
        class VerifyOptions
        {
            [Value(0, Required = true, HelpText = "The file that is being verified.")]
            public string FileToVerify { get; set; }
        }
        static int Main(string[] args)
        {
            // return RealMain(new string[] { "unpack", @"C:\Program Files (x86)\Steam\steamapps\common\Space Beast Terror Fright\sbtf_pub.nwf" });

            // return RealMain(new string[] { "schema", @"C:\Program Files (x86)\Steam\steamapps\common\Space Beast Terror Fright\sbtf_pub.nwf" });

            // return RealMain(new string[] { "repack", "schema.xml", "output", "sbtf_pub.nwf" });

            return RealMain(args);
        }

        static int RealMain(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<UnpackOptions, GenerateSchemaOptions, RepackOptions, VerifyOptions>(args)
                .MapResult(
                    (UnpackOptions opts) => Unpack(opts),
                    (GenerateSchemaOptions opts) => GenerateSchema(opts),
                    (RepackOptions opts) => Repack(opts),
                    (VerifyOptions opts) => Verify(opts),
                    (errs) => 1
                );
        }

        private static int Unpack(UnpackOptions opts)
        {
            Directory.CreateDirectory(opts.Output);

            using var nwfFile = File.OpenRead(opts.NwfFile);
            var package = Package.ReadPackageFromNwf(nwfFile);
            nwfFile.Seek(0, SeekOrigin.Begin);
            var fileCount = package.Files.Count;

            try
            {
                foreach (var (file, index) in package.Files.Select((f, i) => (f, i)))
                {
                    Console.WriteLine($"Reading file {file.FilePath}... ({index + 1}/{fileCount})");

                    if (file.Size > 52428800)
                    {
                        throw new ArgumentException("File size sanity check failed (size > 50 MiB)");
                    }

                    var buf = new byte[file.Size];

                    nwfFile.Seek(file.ComputedOffset, SeekOrigin.Begin);
                    nwfFile.Read(buf, 0, file.Size);

                    var outPath = Path.Join(opts.Output, file.FilePath.Replace('/', Path.DirectorySeparatorChar));
                    Console.WriteLine($"Writing file to {outPath}");

                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    using var outFile = File.Create(outPath);
                    outFile.Write(buf, 0, file.Size);
                }
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"Failed to unpack file: {e.Message}\n{e}");
                return 1;
            }

            Console.WriteLine("Done!");
            return 0;
        }

        private static int GenerateSchema(GenerateSchemaOptions opts)
        {
            using var nwfFile = File.OpenRead(opts.NwfFile);
            var package = Package.ReadPackageFromNwf(nwfFile);
            using var outFile = File.Create(opts.SchemaFile);
            package.WritePackageDefinitionToXml(outFile);

            return 0;
        }

        private static int Repack(RepackOptions opts)
        {
            using var schemaFile = File.OpenRead(opts.SchemaFile);
            var package = Package.ReadPackageDefinitionFromXml(schemaFile);

            // Make sure that all the files we want actually exist
            // Also figure out how long the header is
            // 12 bytes for the header, 20 + fileNameLength bytes for each file entry
            var currentOffset = 12;

            foreach (var file in package.Files)
            {
                var inPath = Path.Join(opts.SourceFolder, file.FilePath);
                if (!File.Exists(inPath))
                {
                    Console.WriteLine($"Repacking failed: The schema wants file \"{file.FilePath}\", " +
                        $"but I can't find it at {Path.GetFullPath(inPath)}");
                    return 1;
                }

                currentOffset += 20 + file.FilePath.Length;
            }

            using var outFile = File.Create(opts.NwfFile);
            using var writer = new BinaryWriter(outFile, Encoding.ASCII, true);

            // Magic number, unknown, file count
            writer.Write(0x6E776660);
            writer.Write(0);
            writer.Write(package.Files.Count);

            foreach (var file in package.Files)
            {
                var inPath = Path.Join(opts.SourceFolder, file.FilePath);
                using var inFile = File.OpenRead(inPath);
                writer.Write(file.FilePath.Length);
                writer.Write(Encoding.ASCII.GetBytes(file.FilePath));
                writer.Write(file.Unknown1);
                writer.Write(file.Unknown2);
                writer.Write(file.Unknown3);
                writer.Write(currentOffset);

                // Length is a long in C# but the format uses ints
                writer.Write((int)inFile.Length);
                currentOffset += (int)inFile.Length;
            }

            writer.Close();

            foreach (var file in package.Files)
            {
                var inPath = Path.Join(opts.SourceFolder, file.FilePath);
                using var inFile = File.OpenRead(inPath);
                inFile.CopyTo(outFile);
            }

            return 0;
        }

        private static int Verify(VerifyOptions opts)
        {
            using var file_to_verify = File.OpenRead(opts.FileToVerify);
            var is_success = Package.VerifyFile(file_to_verify);

            if (is_success)
            {
                Console.WriteLine("File successfully verified.");
                return 0;
            }
            else
            {
                Console.WriteLine("File verification failed. File must be a sbtf_pub.swf from update 60.");
                return 115;
            }
        }
    }
}
