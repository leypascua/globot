using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.CommandLine;

namespace Globot
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            // Register to handle Ctrl+C and SIGTERM signals
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Ctrl+C pressed. Initiating shutdown...");
                cts.Cancel();
                eventArgs.Cancel = true; // Prevent immediate termination
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                cts.Cancel();
            };

            var rootCommand = CreateCommand(cts.Token);
            return await rootCommand.InvokeAsync(args);
        }

        private static RootCommand CreateCommand(CancellationToken cancellationToken)
        {
            // globot "/the/source/path"  --include "*.parquet" --connection "connection-string" --container "container-name" --prefix "ThePrefix"
            var rootCommand = new RootCommand("globot: Upload globs to an Azure Blob Storage container");

            var sourcePathArg = new Argument<string> { Name = "sourcePath", HelpName = "Source path", Description = "/path/to/source/files" };
            rootCommand.AddArgument(sourcePathArg);

            var includeOpt = new Option<string>(
                name: "--include",
                description: "File type extensions to include, comma-separated."
            )
            { ArgumentHelpName = ".png,.jpg,.txt" };
            includeOpt.SetDefaultValue("*.*");
            rootCommand.AddOption(includeOpt);

            var connectionOpt = new Option<string>(
                name: "--connection",
                description: "The Azure Blob Storage connection string to use for uploading"
            )
            { ArgumentHelpName = "DefaultEndpointsProtocol=https;AccountName=storage-account;AccountKey=secret;EndpointSuffix=core.windows.net", IsRequired = true };
            rootCommand.AddOption(connectionOpt);

            var containerOpt = new Option<string>(name: "--container", description: "The Azure Blob Storage container name")
            { IsRequired = true };
            rootCommand.AddOption(containerOpt);

            var prefixOpt = new Option<string>(name: "--prefix", description: "The path (folder) prefix to use when uploading to blob storage.");
            rootCommand.AddOption(prefixOpt);

            rootCommand.SetHandler(async (context) =>
            {
                var options = new GlobotOptions
                { 
                    SourcePath = context.ParseResult.GetValueForArgument(sourcePathArg),
                    ConnectionString = context.ParseResult.GetValueForOption(connectionOpt)!,
                    ContainerName = context.ParseResult.GetValueForOption(containerOpt)!,
                
                    IncludedFileExtensions = ParseFileExtensions(context.ParseResult.GetValueForOption(includeOpt)!).ToArray(),
                };

                var task = new GlobotTaskContext(options, new ConsoleLogger<GlobotTaskContext>());

                await task.UploadAsync(cancellationToken);

                context.Console.WriteLine("Done.");
            });

            return rootCommand;
        }

        private static IEnumerable<string> ParseFileExtensions(string inputCsv)
        {
            var chunks = inputCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());

            foreach (var item in chunks)
            {
                yield return item.StartsWith('*') ?
                    item :
                    "*" + item;
            }
        }

        public class ConsoleLogger<T> : ILogger<T>
        {
            IDisposable? ILogger.BeginScope<TState>(TState state)
            {
                // Scoping is not implemented for simplicity.
                return new ConsoleLoggerDisposable();
            }

            bool ILogger.IsEnabled(LogLevel logLevel)
            {
                throw new NotImplementedException();
            }

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string formattedMessage = formatter != null ?
                    formatter(state, exception) :
                    string.Empty;

                if (!string.IsNullOrEmpty(formattedMessage))
                {
                    Console.WriteLine($"[{logLevel}] [{typeof(T).FullName}] {formattedMessage}");
                    if (exception != null)
                    {
                        Console.WriteLine(exception);
                    }
                }
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                // For simplicity, we log everything.
                return true;
            }

            private class ConsoleLoggerDisposable : IDisposable
            {
                public void Dispose()
                {
                    // No resources to dispose.
                }
            }
        }
    }
}
