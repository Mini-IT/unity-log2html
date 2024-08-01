
namespace UnityLogToHtml
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				PrintHelp();
				return;
			}

			string intputFilePath = args[0];
			string outputFilePath = $"{intputFilePath}.html";

			if (!File.Exists(intputFilePath))
			{
				PrintHelp();
				return;
			}

			if (File.Exists(outputFilePath))
			{
				File.Delete(outputFilePath);
			}

			using var inputFileStream = File.OpenText(intputFilePath);
			using var outputFileStream = File.OpenWrite(outputFilePath);
			using var outputFileWriter = new StreamWriter(outputFileStream);

			var coverter = new LogConverter(inputFileStream, outputFileWriter);
			coverter.Run().Wait();
		}

		private static void PrintHelp()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("UnityLogToHtml [path-to-log-file]");
		}
	}
}