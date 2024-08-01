using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace UnityLogToHtml
{
	internal class LogConverter
	{
		private const string ENTRY_LOG = "Log";
		private const string ENTRY_WARNING = "Warning";
		private const string ENTRY_ERROR = "Error";
		private const string ENTRY_EXCEPTION = "Exception";
		private const string CSS_CLASS_ENTRY_LOG = "entry-log";
		private const string CSS_CLASS_ENTRY_WARNING = "entry-warning";
		private const string CSS_CLASS_ENTRY_ERROR = "entry-error";

		private readonly StreamReader _inputReader;
		private readonly StreamWriter _outputWriter;

		private StringBuilder? _sbEntryStack;

		public LogConverter(StreamReader inputReader, StreamWriter outputWriter)
		{
			_inputReader = inputReader;
			_outputWriter = outputWriter;
		}

		public async Task Run()
		{
			PrintHeader();

			while (!_inputReader.EndOfStream)
			{
				ReadEntry();
			}

			PrintFooter();
		}

		private void ReadEntry()
		{
			string? line = _sbEntryStack != null ?
				_sbEntryStack.ToString() :
				_inputReader.ReadLine();

			_sbEntryStack = null;

			if (string.IsNullOrWhiteSpace(line))
			{
				return;
			}

			string? cssClass = ParseEntryType(line);

			if (string.IsNullOrEmpty(cssClass))
			{
				return;
			}

			StringBuilder sbEntryValue = new();

			while (!_inputReader.EndOfStream)
			{
				line = _inputReader.ReadLine();

				if (string.IsNullOrWhiteSpace(line))
				{
					break;
				}

				if (CheckCallStackLine(line))
				{
					_sbEntryStack ??= new StringBuilder();
				}

				if (string.IsNullOrWhiteSpace(line))
				{
					break;
				}

				string? entryClass = ParseEntryType(line);
				if (!string.IsNullOrEmpty(entryClass))
				{
					break;
				}

				if (_sbEntryStack != null)
				{
					_sbEntryStack.Append(PrepareString(line));
					_sbEntryStack.AppendLine("<br/>");
				}
				else
				{
					sbEntryValue.Append(PrepareString(line));
					sbEntryValue.AppendLine("<br/>");
				}
			}

			if (sbEntryValue.Length == 0)
			{
				return;
			}

			_outputWriter.WriteLine($"<div class=\"{cssClass}\">");
			_outputWriter.WriteLine("  <details>");
			_outputWriter.WriteLine("    <summary>");
			_outputWriter.WriteLine($"{sbEntryValue}");
			_outputWriter.WriteLine("    </summary>");
			if (_sbEntryStack != null)
			{
				_outputWriter.WriteLine("  <div class=\"call-stack\">");
				_outputWriter.WriteLine(_sbEntryStack);
				_outputWriter.WriteLine("  <br/>");
				_outputWriter.WriteLine("  </div>");
			}
			_outputWriter.WriteLine("  </details>");
			_outputWriter.WriteLine("</div>");
		}

		private static bool CheckCallStackLine(string line)
		{
			if (line.StartsWith("UnityEngine.Debug:Log") ||
				line.StartsWith("UnityEngine.Logger:Log"))
			{
				return true;
			}

			var regex = new Regex(@".+:\w+.*(\s+)?\(.*\)$");
			return regex.IsMatch(line);
		}

		private static string? ParseEntryType(string line)
		{
			if (line.StartsWith(ENTRY_LOG))
			{
				return CSS_CLASS_ENTRY_LOG;
			}
			else if (line.StartsWith(ENTRY_WARNING))
			{
				return CSS_CLASS_ENTRY_WARNING;
			}
			else if (line.StartsWith(ENTRY_ERROR))
			{
				return CSS_CLASS_ENTRY_ERROR;
			}
			else if (line.StartsWith(ENTRY_EXCEPTION))
			{
				return CSS_CLASS_ENTRY_ERROR;
			}
			else
			{
				return null;
			}
		}

		private static string PrepareString(string input)
		{
			string output = HttpUtility.HtmlEncode(input);
			return UnescapeTags(output);
		}

		// Unescapes a pair of html tags.
		// IMPORTAINT: If there is no pair for a tag, then keep it as it is
		// Examples:
		// input="&lt;i&gt;some text&lt;/i&gt; some other text"; output="<i>some text</i> some other text"
		// input="&lt;i&gt;some text no closing tag. some other text"; output="&lt;i&gt;some text no closing tag. some other text"
		//
		// Special behaviour for tag <color>
		// input="&lt;color=green&gt;some text&lt;/color&gt; some other text"; output="<font color="green">some text</font> some other text"
		private static string UnescapeTags(string input)
		{
			string[] splitted = input.Split("&gt;");
			foreach (string subsr in splitted)
			{
				string s = subsr;
				string lower = s.ToLower();

				int index = lower.IndexOf("&lt;");
				if (index < 0)
					continue;

				lower = lower.Substring(index);
				s = s.Substring(index);

				if (lower.StartsWith("&lt;color="))
				{
					lower = lower.Replace("&lt;color=", "<font color=") + ">";
					input = input.Replace(s + "&gt;", lower);
				}
				else if (lower.StartsWith("&lt;/color"))
				{
					lower = lower.Replace("&lt;/color", "</font") + ">";
					input = input.Replace(s + "&gt;", lower);
				}
			}

			var tagPattern = @"&lt;(\/?[^&gt;]+)&gt;";
			var matches = Regex.Matches(input, tagPattern);
			Stack<Tuple<string, int>> tagStack = new Stack<Tuple<string, int>>();

			foreach (Match match in matches)
			{
				var tag = match.Groups[1].Value;
				if (!tag.StartsWith("/"))
				{
					tagStack.Push(Tuple.Create(tag, match.Index));
				}
				else
				{
					if (tagStack.Count > 0)
					{
						var startTag = tagStack.Peek();
						if ($"/{startTag.Item1}" == tag)
						{
							tagStack.Pop();
							var unescapedOpenTag = UnescapeSingleTag(startTag.Item1);
							var unescapedCloseTag = UnescapeSingleTag(tag);
							input = input.Remove(startTag.Item2, match.Index + match.Length - startTag.Item2)
										 .Insert(startTag.Item2, unescapedOpenTag + input.Substring(startTag.Item2 + match.Length, match.Index - startTag.Item2 - match.Length) + unescapedCloseTag);
						}
					}
				}
			}
			return input;
		}

		private static string UnescapeSingleTag(string tag)
		{
			if (tag.StartsWith("color"))
			{
				var colorValue = tag.Split('=')[1].Trim();
				return tag.StartsWith("/") ? "</font>" : $"<font color=\"{colorValue}\">";
			}
			return $"<{tag}>";
		}
		
		private void PrintHeader()
		{
			_outputWriter.WriteLine("<html>");
			_outputWriter.WriteLine("  <head>");
			_outputWriter.WriteLine("    <style>");
			_outputWriter.WriteLine("      body {");
			_outputWriter.WriteLine("        font-family: consolas, courier, serif;");
			_outputWriter.WriteLine("        font-size: 90%;");
			_outputWriter.WriteLine("        background-color: #cccccc;");
			_outputWriter.WriteLine("      }");
			_outputWriter.WriteLine($"      .{CSS_CLASS_ENTRY_LOG} {{");
			_outputWriter.WriteLine("        background-color: #cccccc;");
			_outputWriter.WriteLine("        margin-bottom: 0.5em;");
			_outputWriter.WriteLine("      }");
			_outputWriter.WriteLine($"      .{CSS_CLASS_ENTRY_WARNING} {{");
			_outputWriter.WriteLine("        background-color: #ffffaa;");
			_outputWriter.WriteLine("        margin-bottom: 0.5em;");
			_outputWriter.WriteLine("      }");
			_outputWriter.WriteLine($"      .{CSS_CLASS_ENTRY_ERROR} {{");
			_outputWriter.WriteLine("        background-color: #ff8888;");
			_outputWriter.WriteLine("        margin-bottom: 0.5em;");
			_outputWriter.WriteLine("      }");
			_outputWriter.WriteLine("      .call-stack {");
			_outputWriter.WriteLine("        margin-left: 2em;");
			_outputWriter.WriteLine("        font-size: smaller;");
			_outputWriter.WriteLine("      }");
			_outputWriter.WriteLine("    </style>");
			_outputWriter.WriteLine("  </head>");
			_outputWriter.WriteLine("  <body>");
		}

		private void PrintFooter()
		{
			_outputWriter.WriteLine("  </body>");
			_outputWriter.WriteLine("</html>");
		}
	}
}
