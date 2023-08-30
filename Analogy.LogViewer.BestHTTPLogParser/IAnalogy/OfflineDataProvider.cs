using Analogy.Interfaces;
using Analogy.Interfaces.DataTypes;

using Newtonsoft.Json.Linq;

using System.Windows.Forms;

namespace Analogy.LogViewer.BestHTTPLogParser.IAnalogy
{
    public class OfflineDataProvider : Template.OfflineDataProvider
    {
        public override Image? SmallImage { get; set; } = null;
        public override Image? LargeImage { get; set; } = null;
        public override string? OptionalTitle { get; set; } = "Best HTTP Log Parser";
        public override string FileOpenDialogFilters { get; set; } = "JSon files(*.json)|*.json|txt files (*.txt)|*.txt|All files (*.*)|*.*";
        public override IEnumerable<string> SupportFormats { get; set; } = new List<string> { "*.txt", "*.json", "*" };
        public override string? InitialFolderFullPath { get; set; } = Environment.CurrentDirectory;
        public override Guid Id { get; set; } = new Guid("8a235ab7-c918-46a8-8a08-2d93b1c1b313");

        public override IEnumerable<AnalogyLogMessagePropertyName> HideExistingColumns()
        {
            return new AnalogyLogMessagePropertyName[]
            {
                AnalogyLogMessagePropertyName.Class,
                AnalogyLogMessagePropertyName.User,
                AnalogyLogMessagePropertyName.Source,
                AnalogyLogMessagePropertyName.RawText,
                AnalogyLogMessagePropertyName.RawTextType,
                AnalogyLogMessagePropertyName.FileName,
                AnalogyLogMessagePropertyName.LineNumber,
                AnalogyLogMessagePropertyName.MachineName,
                AnalogyLogMessagePropertyName.ProcessId
            };
        }

        public override IEnumerable<string> HideAdditionalColumns()
        {
            return new string[] { "User", "Source" };
        }

        public override async Task<IEnumerable<IAnalogyLogMessage>> Process(string fileName, CancellationToken token, ILogMessageCreatedHandler messagesHandler)
        {
            var result = new List<IAnalogyLogMessage>();

            //var lines = await File.ReadAllLinesAsync(fileName, token);

            try
            {
                using var source = File.OpenText(fileName);

                int processedEntries = 0;
                int processedLines = 0;

                string? line = null;
                while ((line = await source.ReadLineAsync()) != null)
                {
                    line = line.Replace("<b><color=yellow>", "")
                           .Replace("</color></b>", "");
                    LogEntry? entry = null;
                    try
                    {
                        processedLines++;
                        entry = Newtonsoft.Json.JsonConvert.DeserializeObject<LogEntry>(line);
                    }
                    catch
                    { }

                    if (entry != null)
                    {
                        entry.FullTime = DateTime.FromBinary(entry.t);

                        var msg = new AnalogyLogMessage();

                        msg.Text = entry.msg;
                        msg.Level = entry.ToLogLevel();
                        msg.RawText = line;
                        msg.Date = entry.FullTime;
                        msg.ThreadId = entry.tid;
                        msg.Module = entry.div;

                        entry.AddContextsTo(msg);
                        result.Add(msg);

                        messagesHandler.ReportFileReadProgress(new AnalogyFileReadProgress(AnalogyFileReadProgressType.Incremental, 1, ++processedEntries, processedLines));
                    }
                }
            }
            catch (Exception e)
            {
                AnalogyLogMessage exceptionLogEntry = new AnalogyLogMessage($"Error occurred processing file {fileName}. Reason: {e.Message}",
                    AnalogyLogLevel.Critical, AnalogyLogClass.General, nameof(OfflineDataProvider), "None")
                {
                    Module = System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                    //LineNumber = e.StackTrace
                };

                string file = Path.GetFileName(fileName);

                messagesHandler.AppendMessage(exceptionLogEntry, fileName.Equals(file) ? fileName : $"{file} ({fileName})");
                result.Insert(0, exceptionLogEntry);
            }

            messagesHandler.AppendMessages(result, fileName);

            return result;
        }
    }

    public sealed class LogContext
    {
        public string? TypeName;
        public string? Hash;
    }

    public sealed class ExceptionInfo
    {
        public string? stack;
        public string? msg;
    }

    public sealed class LogEntry
    {
        public DateTime FullTime;
        public int tid;
        public string? div;
        public string? msg;
        public string? stack;
        public List<LogContext>? ctx;
        public List<ExceptionInfo>? ex;
        public long t;
        public string? ll;
        public int bh;

        public AnalogyLogLevel ToLogLevel() => this.ll switch
        {
            "All" => AnalogyLogLevel.Verbose,
            "Verbose" => AnalogyLogLevel.Verbose,
            "Information" => AnalogyLogLevel.Information,
            "Warning" => AnalogyLogLevel.Warning,
            "Error" => AnalogyLogLevel.Error,
            "Exception" => AnalogyLogLevel.Critical,
            _ => throw new NotImplementedException($"Unknown log level: {this.ll}")
        };

        public void AddContextsTo(AnalogyLogMessage msg)
        {
            if (ctx != null)
            {
                foreach (var item in ctx)
                {
                    if (item.TypeName != null && item.Hash != null)
                        msg.AddOrReplaceAdditionalProperty(item.TypeName, item.Hash);
                }
            }
        }
    }
}
