﻿using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger
{
    internal class LogFileStream
    {
        public static void HandlePastLogs(string dir)
        {
            Directory.CreateDirectory($"{dir}/logs");

            #region Handle Past Log
            if (File.Exists($"{dir}/logs/latest.log"))
            {
                FileInfo info = new($"{dir}/logs/latest.log");
                info.MoveTo($"{dir}/logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.log");
            }
            foreach (var filename in Directory.GetFiles($"{dir}/logs", "latest.*.log"))
            {
                FileInfo info = new(filename);
                info.MoveTo($"{dir}/logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.{info.Name.Substring("latest.".Length)}");
            }
            #endregion

            #region Compress Past Logs / Generated By ChatGPT
            // 获取目录中所有的日志文件名
            string[] fileNames = Directory.GetFiles(Path.Combine(dir, "logs"));

            List<(DateTime logTime, string filePath)> validFiles = new List<(DateTime, string)>();

            // 过滤掉不符合格式的文件名，收集所有有效的日期
            foreach (string fileName in fileNames)
            {
                string prefix = Path.GetFileName(fileName).Split('.')[0];
                if (DateTime.TryParseExact(prefix, "yyyy-MM-dd_HH-mm-ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                {
                    validFiles.Add((result, fileName));
                }
            }

            // 将日期按从新到旧的顺序排序
            validFiles.Sort((a, b) => -(a.logTime).CompareTo(b.logTime));

            // 将日志文件按日期分组
            var dateGroups = validFiles.GroupBy(d => d.logTime.Date);

            // 对于每一组日期相同的日志文件，进行压缩
            foreach (var dateGroup in dateGroups)
            {
                DateTime date = dateGroup.Key;
                if (date >= DateTime.Today) continue;

                // 构建要保存/添加压缩文件的名称
                string zipFileName = $"log.{date:yyyy-MM-dd}.zip";
                string zipFilePath = Path.Combine(dir, "logs", zipFileName);

                if (File.Exists(zipFilePath))
                {
                    #region 尝试向已有 zip 存档添加文件
                    try
                    {
                        using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update))
                        {
                            foreach (var pair in dateGroup)
                            {
                                string logFilePath = pair.filePath;
                                string logFileName = Path.GetFileName(logFilePath);

                                // 检查文件是否存在
                                if (File.Exists(logFilePath))
                                {
                                    // 添加日志文件到压缩存档
                                    ZipArchiveEntry zipEntry = archive.CreateEntry(logFileName, CompressionLevel.Fastest);
                                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open))
                                    using (Stream entryStream = zipEntry.Open())
                                    {
                                        fileStream.CopyTo(entryStream);
                                    }
                                    // 删除日志文件
                                    File.Delete(logFilePath);
                                }
                            }
                        }
                    }
                    #endregion
                    #region 创建新 zip 归档
                    catch (Exception /*ex*/)
                    {
                        // 如果存在 zip 文件，则尝试在文件名后添加数字序号
                        string newFilePath = Tools.AddNumberedSuffixToPath(zipFilePath);
                        try
                        {
                            using (ZipArchive archive = ZipFile.Open(newFilePath, ZipArchiveMode.Create))
                            {
                                foreach (var pair in dateGroup)
                                {
                                    string logFilePath = pair.filePath;
                                    string logFileName = Path.GetFileName(logFilePath);

                                    // 检查文件是否存在并且大小大于零以防出现空文件
                                    if (File.Exists(logFilePath))
                                    {
                                        // 添加日志文件到压缩存档
                                        ZipArchiveEntry zipEntry = archive.CreateEntry(logFileName, CompressionLevel.Fastest);
                                        using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open))
                                        using (Stream entryStream = zipEntry.Open())
                                        {
                                            fileStream.CopyTo(entryStream);
                                        }
                                        // 删除日志文件
                                        File.Delete(logFilePath);
                                    }
                                }
                            }
                        }
                        catch (Exception /*ex2*/)
                        {
                        }
                    }
                    #endregion
                }
                else
                {
                    #region 创建新 zip 归档
                    try
                    {
                        using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                        {
                            foreach (var pair in dateGroup)
                            {
                                string logFilePath = pair.filePath;
                                string logFileName = Path.GetFileName(logFilePath);

                                // 检查文件是否存在
                                if (File.Exists(logFilePath))
                                {
                                    // 添加日志文件到压缩存档
                                    ZipArchiveEntry zipEntry = archive.CreateEntry(logFileName, CompressionLevel.Fastest);
                                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open))
                                    using (Stream entryStream = zipEntry.Open())
                                    {
                                        fileStream.CopyTo(entryStream);
                                    }
                                    // 删除日志文件
                                    File.Delete(logFilePath);
                                }
                            }
                        }
                    }
                    catch (Exception /*ex2*/)
                    {
                    }
                    #endregion
                }
            }
            #endregion
        }

        public readonly string LogPath;
        public readonly string FileStreamName;
        private readonly StreamWriter logwriter;
        private readonly object write_lck = "3.8.50";
        public readonly LogLevel MinimumLogLevel, MaximumLogLevel;
        public readonly bool IsPipeSeparatedFormat;
        /// <summary>
        /// 对于 <paramref name="FileStreamName"/> == "global"，其被留用作
        /// latest.log 的标识符。用户代码尝试与其同名的示例应引发异常。
        /// </summary>
        public const string GlobalLog_Reserved = "global";

        /// <summary>
        /// 创建一个 <see cref="LogFileStream"/>，对其的操作唯一对应一个文件。
        /// </summary>
        /// <param name="fileStreamName">要创建的日志的文件标识符。<para/>
        /// <remarks>
        /// 创建的日志命名为 latest.<paramref name="fileStreamName"/>.log。不区分大小写。
        /// 特别地，对于 <paramref name="fileStreamName"/> == "global"，其被留用作
        /// latest.log 的标识符。用户代码尝试创建其示例应引发异常。
        /// </remarks>
        /// </param>
        public LogFileStream(string dir, LogFileConfig fileconf)
        {
            if (fileconf.FileIdentifier == null)
            {
                throw new ArgumentException("Please give a valid file identifier for the created log file.");
            }
            if (fileconf.MinimumLogLevel == null || fileconf.MaximumLogLevel == null)
            {
                throw new ArgumentNullException(
                    (fileconf.MinimumLogLevel == null ? $"{nameof(LogFileConfig)}.{nameof(LogFileConfig.MinimumLogLevel)}; " : "") +
                    (fileconf.MaximumLogLevel == null ? $"{nameof(LogFileConfig)}.{nameof(LogFileConfig.MaximumLogLevel)}; " : ""), 
                    "Valid log level borders are expected for the newly created log file.");
            }
            FileStreamName = fileconf.FileIdentifier;
            if (FileStreamName == GlobalLog_Reserved) LogPath = $"{dir}/logs/latest.log";
            else LogPath = $"{dir}/logs/latest.{FileStreamName}.log";
            logwriter = new(LogPath, true);
            logwriter.AutoFlush = fileconf.AutoFlushWriter;
            MinimumLogLevel = (LogLevel)fileconf.MinimumLogLevel;
            MaximumLogLevel = (LogLevel)fileconf.MaximumLogLevel;
            IsPipeSeparatedFormat = fileconf.IsPipeSeparatedFile;
        }

        public void WriteLine(string content, LogLevel level)
        {
            if (level < MinimumLogLevel || level > MaximumLogLevel) return;
            lock (write_lck)
            {
                logwriter.WriteLine(content);
            }
        }

        public void WriteLine(ColorLineResult content, LogLevel level)
        {
            if (level < MinimumLogLevel || level > MaximumLogLevel) return;
            lock (write_lck)
            {
                logwriter.WriteLine(content.TextWithoutColor);
            }
        }

        private static ConcurrentDictionary<string, LogFileStream> fileStreams = new();

        public static LogFileStream GetInitedInstance(string fileStreamName)
        {
            return fileStreams[fileStreamName.ToLower()];
        }
    }
}
