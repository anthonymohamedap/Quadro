using System.Collections.ObjectModel;

namespace QuadroApp.Service.Import
{


    public sealed class ImportResult<T>
    {
        public ObservableCollection<T> Rows { get; } = new();
        public ObservableCollection<string> Issues { get; } = new();

        public bool HasErrors => Issues.Count > 0;
    }

}
