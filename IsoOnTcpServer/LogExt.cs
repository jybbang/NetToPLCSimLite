using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IsoOnTcp;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

namespace NetToPLCSimLite
{
    public static class LogExt
    {
        public static ILog log;

        static LogExt()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly());

            var pattern = new log4net.Layout.PatternLayout("[ %date ] [ %-5level ] %message %exception%newline");
            pattern.ActivateOptions();

            var consoleAppender = new log4net.Appender.ManagedColoredConsoleAppender
            {
                Name = "ConsoleAppender",
                Layout = pattern,
            };
            consoleAppender.AddMapping(new ManagedColoredConsoleAppender.LevelColors() { ForeColor = ConsoleColor.White, BackColor = ConsoleColor.Red, Level = log4net.Core.Level.Fatal });
            consoleAppender.AddMapping(new ManagedColoredConsoleAppender.LevelColors() { ForeColor = ConsoleColor.Red, Level = log4net.Core.Level.Error });
            consoleAppender.AddMapping(new ManagedColoredConsoleAppender.LevelColors() { ForeColor = ConsoleColor.Yellow, Level = log4net.Core.Level.Warn });
            consoleAppender.AddMapping(new ManagedColoredConsoleAppender.LevelColors() { ForeColor = ConsoleColor.White, Level = log4net.Core.Level.Info });
            consoleAppender.AddMapping(new ManagedColoredConsoleAppender.LevelColors() { ForeColor = ConsoleColor.Gray, Level = log4net.Core.Level.Debug });
            consoleAppender.ActivateOptions();

            var logFileAppender = new log4net.Appender.RollingFileAppender
            {
                Name = "LogFileAppender",
                Layout = pattern,
                File = @"logs/log.txt",
                AppendToFile = true,
                RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Date,
                DatePattern = "_yyyy-MM-dd",
                MaximumFileSize = "10MB",
                PreserveLogFileNameExtension = true,
            };
            logFileAppender.ActivateOptions();

            hierarchy.Root.AddAppender(consoleAppender);
            hierarchy.Root.AddAppender(logFileAppender);
#if DEBUG
            hierarchy.Root.Level = log4net.Core.Level.Debug;
#else
            hierarchy.Root.Level = log4net.Core.Level.Info;
#endif
            hierarchy.Configured = true;

            log = LogManager.GetLogger(Assembly.GetEntryAssembly(), CONST.LOGGER_NAME);
        }

        public static void SwitchLevel()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly());

            if (hierarchy.Root.Level == log4net.Core.Level.Debug)
            {
                hierarchy.Root.Level = log4net.Core.Level.Info;
                log.Info($"Log level changed.");
            }
            else if (hierarchy.Root.Level == log4net.Core.Level.Info)
            {
                hierarchy.Root.Level = log4net.Core.Level.Warn;
                log.Warn($"Log level changed.");
            }
            else if (hierarchy.Root.Level == log4net.Core.Level.Warn)
            {
                hierarchy.Root.Level = log4net.Core.Level.Debug;
                log.Debug($"Log level changed.");
            }
        }
    }
}
