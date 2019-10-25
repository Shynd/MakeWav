using System;
using System.IO;
using System.Text;

namespace MakeWav
{
	/// <summary>
	/// This program basically takes a file you give it and prepends
	/// a wave file header to it which makes it a playable audio file.
	/// </summary>
	class Program
	{
        	static void Main(string[] args)
        	{						
			if (args.Length < 1)
				ExitWithReason("No file given.");

			if (!File.Exists(args[0]))
				ExitWithReason("File does not exist.");

			// load a file as bytes
			var fileBytes = File.ReadAllBytes(args[0]);

			Logger.Log(LogLevel.Info, "Creating WAV file header structure...");
			var wav = new Wav();
			wav.Header = new WAVHEADER
			{
				RiffID = "RIFF".ToBytes(),
				Size = (uint) 0, // set this after the header is created
				WavID = "WAVE".ToBytes(),
				FmtID = "fmt ".ToBytes(),
				FmtSize = 16,
				Format = 1, // PCM
				Channels = 2,
				SampleRate = 44_100, // 44_100
				Bit = 16,
				BlockSize = 0, // Will be set after.
				BytesPerSec = 0, // Will be set after.
				DataID = "data".ToBytes(),
				// Insert the "sound" data length.
				DataSize = (uint) fileBytes.Length,
			};

			// Calculate the bytes per second.
			Logger.Log(LogLevel.Info, "Calculating the bytes per second needed...");
			wav.Header.BytesPerSec = (wav.Header.SampleRate * wav.Header.Bit * wav.Header.Channels) / 8;
			// Change block size - (BitsPerSample * Channels) / 8.1 - 8 bit mono2 - 8 bit stereo/16 bit mono4 - 16 bit stereo 
			Logger.Log(LogLevel.Info, "Setting the block size...");
			wav.Header.BlockSize = (ushort) ((wav.Header.Bit * wav.Header.Channels) / 8);

			// Set the "Size" variable in the header (overall file-size - 8 bytes).
			Logger.Log(LogLevel.Info, "Calculating the file size...");
			wav.Header.Size = wav.Header.DataSize - 8;

			// Insert the "sound" data bytes.
			Logger.Log(LogLevel.Info, $"Writing the sound data to the file... ({fileBytes.Length} bytes)");
			wav.Data = fileBytes;

			// Print info about the file.
			wav.PrintHeader();

			// Write the wav to a MemoryStream
			var outputBytes = wav.Write();
			// Create the output filename
			var outputName = args[0].Split('.')[0];
			outputName += "_out.wav";
			// Write the bytes to the output file.
			Logger.Log(LogLevel.Info, $"Writing output file to '{outputName}'...");
			File.WriteAllBytes(outputName, outputBytes);
			Logger.Log(LogLevel.Normal, "Success!");
		}
		
		static void ExitWithReason(string reason)
		{
			Logger.Log(LogLevel.Error, reason);
			Environment.Exit(-1);
		}
	}
	
	public static class Extensions
	{
		public static string BytesToString(this byte[] args) => Encoding.ASCII.GetString(args);

		public static byte[] ToBytes(this uint args) => BitConverter.GetBytes(args);
		public static byte[] ToBytes(this ushort args) => BitConverter.GetBytes(args);
		public static byte[] ToBytes(this string str) => Encoding.ASCII.GetBytes(str);
	}
	
	public abstract class FileParser<TFormat> where TFormat : FileParser<TFormat>, new()
	{
		public abstract void Read(BinaryReader br);
		public abstract byte[] Write();
		
		public static TFormat Read(string fileName)
		{
			if (File.Exists(fileName))
			{
				using (var br = new BinaryReader(File.Open(fileName, FileMode.Open)))
				{
					var file = new TFormat();
					file.Read(br);
					return file;
				}
			}
			
			return null;
		}
		
		public static TFormat Write(string fileName)
		{
			if (File.Exists(fileName))
			{
				using (var bw = new BinaryWriter(File.Open(fileName, FileMode.Open)))
				{
					var file = new TFormat();
					file.Write();
					return file;
				}
			}
			
			return null;
		}
	}
	
	public class Wav : FileParser<Wav>
	{
		public WAVHEADER Header = new WAVHEADER();
		public byte[] Data { get; set; }
		
		public override void Read(BinaryReader br)
		{
			// Read header.
			Header.RiffID = br.ReadBytes(4);
			Header.Size = br.ReadUInt32();
			Header.WavID = br.ReadBytes(4);
			Header.FmtID = br.ReadBytes(4);
			Header.FmtSize = br.ReadUInt32();
			Header.Format = br.ReadUInt16();
			Header.Channels = br.ReadUInt16();
			Header.SampleRate = br.ReadUInt32();
			Header.BytesPerSec = br.ReadUInt32();
			Header.BlockSize = br.ReadUInt16();
			Header.Bit = br.ReadUInt16();
			Header.DataID = br.ReadBytes(4);
			Header.DataSize = br.ReadUInt32();
			
			// read data
			Data = br.ReadBytes((int) Header.DataSize);
		}
		
		public override byte[] Write()
		{
			var ms = new MemoryStream();
			ms.Write(Header.RiffID);
			ms.Write(Header.Size.ToBytes());
			ms.Write(Header.WavID);
			ms.Write(Header.FmtID);
			ms.Write(Header.FmtSize.ToBytes());
			ms.Write(Header.Format.ToBytes());
			ms.Write(Header.Channels.ToBytes());
			ms.Write(Header.SampleRate.ToBytes());
			ms.Write(Header.BytesPerSec.ToBytes());
			ms.Write(Header.BlockSize.ToBytes());
			ms.Write(Header.Bit.ToBytes());
			ms.Write(Header.DataID);
			ms.Write(Header.DataSize.ToBytes());
			
			// Write the "sound" data to the wave file.
			ms.Write(Data);
			
			return ms.ToArray();
		}
		
		// ---- Debug printing
		
		public void PrintHeader()
		{
			Console.WriteLine($"\n[Attribute]  [Value]");
			Console.WriteLine($"Magic        : {Header.RiffID.BytesToString()}");
			Console.WriteLine($"Size         : {Header.Size} bytes | {Header.Size/1024} Kb | {Header.Size/1024/1024} Mb");
			Console.WriteLine($"WavID        : {Header.WavID.BytesToString()}");
			Console.WriteLine($"FmtID        : {Header.FmtID.BytesToString()}");
			Console.WriteLine($"Size         : {Header.FmtSize} bytes | {Header.FmtSize/1024} Kb | {Header.FmtSize/1024/1024} Mb");
			Console.WriteLine($"Format       : {(Header.Format == 1 ? "PCM" : Header.Format.ToString())}");
			Console.WriteLine($"Channels     : {Header.Channels}");
			Console.WriteLine($"SampleRate   : {Header.SampleRate} KHz");
			Console.WriteLine($"BytesPerSec  : {Header.BytesPerSec}");
			Console.WriteLine($"BlockSize    : {Header.BlockSize}");
			Console.WriteLine($"BitsPerSample: {Header.Bit}");
			Console.WriteLine($"DataID       : {Header.DataID.BytesToString()}");
			Console.WriteLine($"DataSize     : {Header.DataSize} bytes | {Header.DataSize/1024} Kb | {Header.DataSize/1024/1024} Mb");
		}
	}
	
	public struct WAVHEADER
	{
		public byte[] RiffID;
		public uint Size;
		public byte[] WavID;
		public byte[] FmtID;
		public uint FmtSize;
		public ushort Format;
		public ushort Channels;
		public uint SampleRate;
		public uint BytesPerSec;
		public ushort BlockSize;
		public ushort Bit;
		public byte[] DataID;
		public uint DataSize;
	}
	
	public enum LogLevel
	{
		Normal,
		Info,
		Error,
		Exception
	}
	
	public static class Logger
	{		
		public static void Log(LogLevel level, string msg)
		{
			switch (level)
			{
				case LogLevel.Normal:
				Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine($"[OK] {msg}");
					Console.ResetColor();
					break;
				case LogLevel.Info:
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"[INFO] {msg}");
					Console.ResetColor();
					break;
				case LogLevel.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"[ERROR] {msg}");
					Console.ResetColor();
					break;
				case LogLevel.Exception:
					Console.ForegroundColor = ConsoleColor.DarkRed;
					Console.WriteLine($"[ERROR] {msg}");
					Console.ResetColor();
					break;
			}
		}
	}
}
