using System;
using System.Diagnostics;
using System.Reactive.Linq;
using ColorConsole;
using CommandLineParser.Arguments;
using System.Collections.Generic;
using log4net;
using System.Text;
using System.Configuration;
using log4net.Config;

namespace PerfCounterMonitor.Tool
{
    class Program
    {
        public static readonly ILog _logger = LogManager.GetLogger(typeof(Program));
        class ArgsOptions
        {
            [ValueArgument(typeof(string), 'p', "process", Description = "Name of the process to monitor", Optional = true)]
            public string ProcessName;
        }

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var pn = ConfigurationSettings.AppSettings["proccessName"];
            var console = new ConsoleWriter();
            var cmdLineParser = new CommandLineParser.CommandLineParser();
            ArgsOptions p = new ArgsOptions();
            cmdLineParser.ExtractArgumentAttributes(p);
            cmdLineParser.ParseCommandLine(args);

            if (string.IsNullOrEmpty(p.ProcessName))
            {
                p.ProcessName = pn;
            }

            var process = Process.GetProcessesByName(p.ProcessName);

            console.WriteLine($"Press any key to quit...", ConsoleColor.Yellow);

            if (process == null || process.Length == 0)
            {
                console.WriteLine($"ERROR: There is no process with name: {p.ProcessName}", ConsoleColor.Red);
                Console.ReadKey();
                return;
            } else if (process.Length > 1)
            {
                console.WriteLine($"ERROR: There are no processes with name: {p.ProcessName}", ConsoleColor.Red);
                Console.ReadKey();
            }

            var perfCounters = GetCounters(p.ProcessName);

            var subject = Observable.Interval(TimeSpan.FromMilliseconds(1000))
                .ObserveOn(System.Reactive.Concurrency.Scheduler.Default)
                .Subscribe(x =>
                {
                    var messageBuilder = new StringBuilder();
                    foreach(var counterKey in perfCounters.Keys)
                    {
                        var counter = perfCounters[counterKey];
                        var value = counterKey == "Working Set" ? counter.NextValue() / 1024 / 1024 : counter.NextValue();
                        messageBuilder.AppendFormat("{0}: {1};", counterKey, value);
                    }
                    _logger.Info(messageBuilder.ToString());

                }, () => console.WriteLine("Done", ConsoleColor.DarkCyan));

            
            Console.ReadKey();
            subject.Dispose();
        }

        private static Dictionary<string, PerformanceCounter> GetCounters(string processName)
        {
            var perfCounters = new Dictionary<string, PerformanceCounter>();
            if (PerformanceCounterCategory.Exists("Process"))
            {
                if (PerformanceCounterCategory.CounterExists("Working Set", "Process"))
                {
                    perfCounters.Add("Working Set", new PerformanceCounter("Process", "Working Set", processName, true));
                }

                if (PerformanceCounterCategory.CounterExists("% Processor Time", "Process"))
                {
                    perfCounters.Add("CPU", new PerformanceCounter("Process", "% Processor Time", processName, true));
                }
            }

            if (PerformanceCounterCategory.Exists("Memory"))
            {
                if (PerformanceCounterCategory.CounterExists("Available Mbytes", "Memory"))
                {
                    perfCounters.Add("Available Memory", new PerformanceCounter("Memory", "Available Mbytes", true));
                }
            }

            return perfCounters;
        }
    }


}
