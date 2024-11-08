﻿using System.Text;
using YYHEggEgg.Logger;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Log.Initialize(new LoggerConfig(
            max_Output_Char_Count: 1024,
            use_Console_Wrapper: true,
            use_Working_Directory: true,
#if DEBUG
            global_Minimum_LogLevel: LogLevel.Verbose,
            console_Minimum_LogLevel: LogLevel.Debug,
#else
            global_Minimum_LogLevel: LogLevel.Information,
            console_Minimum_LogLevel: LogLevel.Warning,
#endif
            debug_LogWriter_AutoFlush: true,
            enable_Detailed_Time: false,
            enable_file_logging: true
            ));

        // 0. ConsoleWrapper input prompt test
        Console.WriteLine($"Waiting for 3s. It's a bug if input prompt don't show up now.");
        ConsoleWrapper.InputPrefix = "prompt: OK :) ";
        try { await ConsoleWrapper.ReadLineAsync(true, new CancellationTokenSource(3000).Token); } catch (TaskCanceledException) { }

        // 1. Shared log FileStream test
        BaseLogger separate_logger = new BaseLogger(new LoggerConfig(
            max_Output_Char_Count: 1024,
            use_Console_Wrapper: true,
            use_Working_Directory: true,
            global_Minimum_LogLevel: LogLevel.Verbose,
            console_Minimum_LogLevel: LogLevel.Information,
            debug_LogWriter_AutoFlush: true,
            enable_Detailed_Time: false,
            enable_file_logging: true
            ), new LogFileConfig
             {
                AutoFlushWriter = true,
                FileIdentifier = "Warning",
                MinimumLogLevel = LogLevel.Warning,
                MaximumLogLevel = LogLevel.Error,
                IsPipeSeparatedFile = true,
             }
        );
        separate_logger = new BaseLogger(separate_logger.CustomConfig, "Warning");
        separate_logger = new BaseLogger(separate_logger.CustomConfig, new LogFileConfig
            {
                AutoFlushWriter = true,
                FileIdentifier = "Warning",
                MinimumLogLevel = LogLevel.Warning,
                MaximumLogLevel = LogLevel.Error,
                IsPipeSeparatedFile = true,
                AllowAutoFallback = true,
            });

        // 2. Ctrl+C closing test
        ConsoleWrapper.ShutDownRequest += (_, _) => Environment.Exit(0);
        // int attempt_reading_wait_seconds = 100; // set to 100 when testing Ctrl+C
        int attempt_reading_wait_seconds = 1;

        // 2.5. Error Trace test
        try
        {
            throw new NotImplementedException("This is a test error in Logger-test.");
        }
        catch (Exception ex)
        {
            LogTrace.ErroTrace(ex, "ErrorTraceTest", "Error trace test raised an exception.");
        }

        // 3. Dbug test
#if DEBUG
        Log.Dbug("this is run on DEBUG!");
        Log.Verb("Verbose is output to the log file, not console!");
#elif RELEASE
        Log.Dbug("this should not be output in RELEASE!");
        Log.Verb("Verbose should not appear at all!", "TESTSender");
#endif

        Log.PushLog("Push a warning log!", LogLevel.Warning, "TSETSender");
        Log.PushLog("Push a verbose log!", LogLevel.Verbose, "TSESTender");

        // 4. Global default color set test
        Log.Info($"Waiting for 1s...");
        await Task.Delay(1000);
        Log.GlobalDefaultColor = ConsoleColor.White;
        Log.Warn($"Global logging console color changed to white.");

        // 5. LogTextWriter Encoding test
        StringReader chinese_string = new("你说得对，但是《原神》是一款由米哈游自主研发的开放世界冒险游戏");

        LogTextWriter logwriter = new();
        while (true)
        {
            var chint = chinese_string.Read();
            if (chint == -1) break;
            logwriter.Write((char)chint);
        }
        logwriter.WriteLine();

        // 6. Color test
        Log.Erro("<color=</color>" +
            "<color=Yellow>yelolow text</color>" +
            "<color=Yellow></color><-nothing text|" +
            "<color=Yellow><color=White><color=Blue><>></color></color></color>");
        Log.Info("<color=Blue>blue text</color>-<>>><<<color=Yellow>yelolow text</color>/<><color=FF>no color text</color>", "Should not output if Release");
        Log.Info($"start 1st reading attempt in {attempt_reading_wait_seconds}s...");
        await Task.Delay(attempt_reading_wait_seconds * 1000);
        ConsoleWrapper.InputPrefix = "WrapperCLI> ";
        string res = ConsoleWrapper.ReadLine();
        Log.Warn(res, "ReadLine");
        // 7. Auto Complete test
        Log.Info("Now auto complete enabled... (Press Tab/Shift+Tab)");
        bool waiting = true;
        // 8. Write & Read Combined test
        _ = Task.Run(async () =>
        {
            while (waiting)
            {
                Log.Warn("A message is passing by once per sec.", "Background_TestAutoComplete");
                await Task.Delay(1000);
            }
        });
        // 9. History reload test
        _ = Task.Run(async () =>
        {
            Log.Warn("The history is about to be replaced after 3s.", "Background_TestChangeHistory");
            await Task.Delay(3000);
            ConsoleWrapper.ChangeHistory(new List<string> { "history1", "historypushed" });
        });
        ConsoleWrapper.AutoCompleteHandler = new SimpleAutoCmpl();
        res = ConsoleWrapper.ReadLine();
        waiting = false;
        Log.Warn(res, "ReadLine_WithAutoCOmplete");
        //ConsoleWrapper.ReadLine();

        // 10. History usage test
        var _logchannel = Log.GetChannel("Historytest");
        _logchannel.LogWarn("Please type how many times you will test ConsoleWrapper input: ");
        var times = int.Parse(ConsoleWrapper.ReadLine());
        while (times-- > 0)
        {
            string text;
            if (times % 2 == 1)
            {
                _logchannel.LogWarn("This line of input will <color=Red>NOT</color> be recorded into history.");
                text = ConsoleWrapper.ReadLine(false);
            }
            else text = ConsoleWrapper.ReadLine();
            _logchannel.LogWarn(text);
        }

        // 11. High output amout test
        separate_logger.Warn(() =>
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 1000; i++) sb.AppendLine($"Batching message part {i}");
            return sb.ToString();
        });

        // ConsoleWrapper.InputPrefix = "Present for test prefix > ";
        // _ = ConsoleWrapper.ReadLineAsync();

        // Parallel.For(0, 1000, i =>
        // {
        //     Log.Warn(BatchingMessage);
        // });

        // ConsoleWrapper.ReadLine();

        // 12. Sudden terminate test
        Log.Warn($"The clearup succeed!");
        Environment.Exit(0);
    }

    class SimpleAutoCmpl : IAutoCompleteHandler
    {
        public SuggestionResult GetSuggestions(string text, int index)
        {
            var startIndex = text[0..index].LastIndexOf(' ') + 1;
            var endIndex = text[index..].IndexOf(' ');
            return new SuggestionResult
            {
                StartIndex = startIndex,
                EndIndex = endIndex,
                Suggestions = new List<string> { "autocmp1_01", "autocmp1_02", "autocmpl_03", "测试中文autocompl01", "测试中文autocmpl02" }
            };
        }
    }
}
