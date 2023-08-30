using Analogy.Interfaces;
using Analogy.LogViewer.Template;

namespace Analogy.LogViewer.BestHTTPLogParser.IAnalogy
{
    public class PrimaryFactory : Analogy.LogViewer.Template.PrimaryFactory
    {
        internal static Guid Id { get; } = new Guid("92b3f9a7-d1ec-4197-b5fb-6e1d76011ad5");
        public override Guid FactoryId { get; set; } = Id;
        public override string Title { get; set; } = "Best HTTP Log Parser";
        public override IEnumerable<IAnalogyChangeLog> ChangeLog { get; set; } = ChangeLogList.GetChangeLog();
        public override IEnumerable<string> Contributors { get; set; } = new List<string> { "Tivadar György Nagy" };
        public override string About { get; set; } = "Best HTTP Log Parser";
        public override Image? SmallImage { get; set; } = null;
        public override Image? LargeImage { get; set; } = null;
    }

    public class DataProvidersFactory : LogViewer.Template.DataProvidersFactory
    {
        public override Guid FactoryId { get; set; } = PrimaryFactory.Id;
        public override string Title { get; set; } = "Best HTTP Log Parser";
        public override IEnumerable<IAnalogyDataProvider> DataProviders { get; set; } = new List<IAnalogyDataProvider> { new OfflineDataProvider() };
    }
}
