using Analogy.Interfaces;
using Analogy.Interfaces.DataTypes;
using Analogy.LogViewer.Template.Properties;

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
            
            int processedEntries = 0;
            int processedLines = 0;

            string? line = null;
            DateTime start = DateTime.Now;

            try
            {
                using var source = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                RaiseProcessingStarted(new AnalogyStartedProcessingArgs(start, string.Empty));

                while ((line = await source.ReadLineAsync()) != null)
                {
                    line = line.Replace("<b><color=yellow>", "")
                           .Replace("</color></b>", "");

                    int jsonStartIdx = line.IndexOf("{");
                    if (jsonStartIdx > 0)
                        line = line.Remove(0, jsonStartIdx);

                    LogEntry? entry = null;
                    try
                    {
                        processedLines++;
                        entry = Newtonsoft.Json.JsonConvert.DeserializeObject<LogEntry>(line);
                    }
                    catch
                    { }

                    if (entry != null && entry.bh > 0)
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

                result.Insert(0, exceptionLogEntry);
            }
            finally
            {
                messagesHandler.AppendMessages(result, fileName);

                RaiseProcessingFinished(new AnalogyEndProcessingArgs(start, DateTime.Now));
            }
            return result;
        }

        protected override List<FileInfo> GetSupportedFilesInternal(DirectoryInfo dirInfo, bool recursive)
        {
            return new List<FileInfo>(0);
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
        public List<Dictionary<string, object>>? ctx;
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
            _ => throw new NotImplementedException($"Unknown log level: '{this.ll}'")
        };

        public void AddContextsTo(AnalogyLogMessage msg)
        {
            if (ctx != null)
                for (int i = 0; i < ctx.Count; i++) 
                    AddProperties(msg, i.ToString(), ctx[i]);
        }

        public void AddProperties(AnalogyLogMessage msg, string prefix, Dictionary<string, object> properties)
        {
            foreach (var prop in properties)
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Key : $"{prefix}.{prop.Key}";

                if (prop.Value is string str)
                    msg.AddOrReplaceAdditionalProperty(key, str);
                else if (prop.Value is Dictionary<string, object> subProp)
                    AddProperties(msg, key, subProp);
                else if (prop.Value is int intVal)
                    msg.AddOrReplaceAdditionalProperty(key, intVal.ToString());
                else if (prop.Value is long longVal)
                    msg.AddOrReplaceAdditionalProperty(key, longVal.ToString());
                else if (prop.Value is bool boolVal)
                    msg.AddOrReplaceAdditionalProperty(key, boolVal.ToString());
            }
        }
    }
}
