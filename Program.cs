namespace ccwc;

using System;
using System.CommandLine;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;

/// <summary>
/// This Program is a very simple version of wc. It was created based on the Coding Challenges description.
/// For more information, see https://codingchallenges.fyi/challenges/challenge-wc.
/// </summary>
class Program
{
    /// <summary>
    /// Struct holding a given file stats.
    /// </summary>
    private struct FileStats
    {
        /// <summary>
        /// The file name requested.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// The number of Bytes in the file.
        /// </summary>
        public int ByteCount { get; set; }

        /// <summary>
        /// The number of lines in the file.
        /// </summary>
        public int LineCount { get; set; }

        /// <summary>
        /// The number of characters in the file.
        /// </summary>
        public int CharCount { get; set; }

        /// <summary>
        /// The number of words in the file.
        /// </summary>
        public int WordCount { get; set; }
    }


    /// <summary>
    /// A holder to the stream to be processed (file or standard input).
    /// </summary>
    private struct StreamReference
    {
        /// <summary>
        /// The name for reference.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The actual stream to read.
        /// </summary>
        public Stream Stream { get; set; }
    }


    /// <summary>
    /// The size of the buffer for reading bytes from a file.
    /// </summary>
    /// <remarks>
    /// We'll read the same buffer from disk as the original WC.
    /// </remarks>
    public const int BufferSize = 16 * 1024;

    /// <summary>
    /// Application's entry point.
    /// </summary>
    /// <param name="args">Arguments passed via commmand line.</param>
    /// <returns></returns>
    public static int Main(string[] args)
    {
        return BuildCommandDef().Invoke(args);
    }

    /// <summary>
    /// Builds the Command using the <see cref="System.CommandLine"/> API.
    /// </summary>
    /// <returns>A <see cref="System.CommandLine.RootCommand"/> with all available options.</returns>
    private static RootCommand BuildCommandDef()
    {
        var fileArgument = new Argument<FileInfo>(
            name: "file",
            description: "The file to process. If no file is specified, the standard input will be used."
        );

        fileArgument.SetDefaultValue(null); // Allow the user to provide standard input
        fileArgument.AddValidator(result => {
            if(result.GetValueForArgument(fileArgument) == null && !Console.IsInputRedirected)
            {
                result.ErrorMessage = "You must either provide a file or redirect the standard input for computation.";
            }
        });
        
        var bytesOption = new Option<bool>(
            name: "-c",
            description: "The number of bytes in each input file is written to the standard output.  This will cancel out any prior usage of the -m option.",
            getDefaultValue: () => false
        );

        var linesOption = new Option<bool>(
            name: "-l",
            description: "The number of lines in each input file is written to the standard output.",
            getDefaultValue: () => false
        );

        var wordsOption = new Option<bool>(
            name: "-w",
            description: "The number of words in each input file is written to the standard output.",
            getDefaultValue: () => false
        );

        var charOption = new Option<bool>(
            name: "-m",
            description: "The number of characters in each input file is written to the standard output.  If the current locale does not support multibyte characters, this is equivalent to the -c option.  This will cancel out any prior usage of the -c option.",
            getDefaultValue: () => false
        );

        var rootCommand = new RootCommand("ccwc – word, line, character, and byte count. A replica of wc written in C# per the Coding Challenge.");

        rootCommand.AddArgument(fileArgument);

        rootCommand.AddOption(bytesOption);
        rootCommand.AddOption(linesOption);
        rootCommand.AddOption(wordsOption);
        rootCommand.AddOption(charOption);

        rootCommand.SetHandler((bytesOptionValue, linesOptionValue, wordsOptionValue, charOptionValue, file) =>
        {
            var streamRef = CreateStreamReference(file);
            var fileStats = ComputeStats(streamRef, bytesOptionValue, linesOptionValue, wordsOptionValue, charOptionValue);

            PrintStats(fileStats, bytesOptionValue, linesOptionValue, wordsOptionValue, charOptionValue);
        }, bytesOption, linesOption, wordsOption, charOption, fileArgument);
        return rootCommand;
    }

    /// <summary>
    /// Creates a refrence to the stream that should be processed.
    /// </summary>
    /// <param name="file">The file provided as input by the user.</param>
    /// <returns></returns>
    private static StreamReference CreateStreamReference(FileInfo file)
    {
        if(file == null)
        {
            return new StreamReference { Name = "", Stream = Console.OpenStandardInput() };
        }
        return new StreamReference { Name = file.ToString(), Stream = file.OpenRead() };
    }

    /// <summary>
    /// Prints the final <see cref="stats"/> for the file.
    /// 
    /// This method follows the same convention as wc: line, word, byte, and file name.
    /// </summary>
    /// <param name="stats">The computed stats to be printed.</param>
    /// <param name="countBytes">Indicates whether or not to print the number of bytes.</param>
    /// <param name="countLines">Indicates whether or not to print the number of lines.</param>
    /// <param name="countWords">Indicates whether or not to print the number of words.</param>
    /// <param name="countChars">Indicates whether or not to print the number of Characters.</param>
    private static void PrintStats(FileStats stats, bool countBytes, bool countLines, bool countWords, bool countChars)
    {
        Console.Write("  ");

        if (countLines)
        {
            Console.Write("{0} ", stats.LineCount);
        }

        if (countWords)
        {
            Console.Write("{0} ", stats.WordCount);
        }

        if (countChars)
        {
            Console.Write("{0} ", stats.CharCount);
        }

        if (countBytes)
        {
            Console.Write("{0} ", stats.ByteCount);
        }

        Console.WriteLine(stats.File);
    }

    /// <summary>
    /// Compute the statistics for a given file.
    /// </summary>
    /// <remarks>
    /// For this implementation, I have chosen to follow a very similar approach from the original wc program.
    /// This means that I read the file in chuncks and perform all operations at byte level, instead of using a API that already gives the file content as a String.
    /// 
    /// The main reasons for that: (1) to better understand the inners of reading the file directly and dealing with decoding, 
    /// and (2) to make it more efficient, byte computing some of the stats while a read the chucks from the raw file.
    /// 
    /// Another approach would be to use the <see cref="System.IO.StreamReader"/> to get a string directly and use regular expressions to compute the # of words, for instance.
    /// </remarks>
    /// <param name="streamRef">A reference to the stream to be processed. This could be either a file or the standard input.</param>
    /// <param name="countBytes">Indicates whether or not to compute the number of bytes.</param>
    /// <param name="countLines">Indicates whether or not to compute the number of lines.</param>
    /// <param name="countWords">Indicates whether or not to compute the number of words.</param>
    /// <param name="countChars">Indicates whether or not to compute the number of Characters.</param>
    /// <returns></returns>
    private static FileStats ComputeStats(StreamReference streamRef, bool countBytes, bool countLines, bool countWords, bool countChars)
    {
        var needsDecoding = countChars | countWords | countLines;

        var buffer = new byte[BufferSize];
        var decoder = GetDecoder();
        var stats = new FileStats { File = streamRef.Name };

        using (Stream fs = streamRef.Stream)
        {
            var bytesRead = fs.Read(buffer, 0, BufferSize);
            var inWord = false;

            while (bytesRead > 0)
            {
                stats.ByteCount += bytesRead;

                if (needsDecoding)
                {
                    var decodedChars = new char[BufferSize];
                    var readChars = decoder.GetChars(buffer, 0, bytesRead, decodedChars, 0);

                    stats.CharCount += readChars;

                    for (int i = 0; i < readChars; i++)
                    {
                        switch (decodedChars[i])
                        {
                            case '\n':
                                stats.LineCount += 1;
                                goto case '\r';
                            case '\r':
                            case '\f':
                            case '\t':
                            case '\v':
                            case ' ':
                                inWord = false;
                                break;
                            default:
                                if (!inWord)
                                    stats.WordCount += 1;

                                inWord = true;
                                break;
                        }
                    }
                }

                bytesRead = fs.Read(buffer, 0, BufferSize);
            }
        }

        return stats;
    }

    /// <summary>
    /// Gets the decoder for converting the bytes from a file into a characters. For now, I only support utf-8 files.
    /// </summary>
    /// <returns>A stateful <see cref="System.Text.Decoder"/> for UTF-8 streams.</returns>
    private static Decoder GetDecoder()
    {
        return new UTF8Encoding().GetDecoder();
    }
}
