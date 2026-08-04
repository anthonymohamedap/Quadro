using CommunityToolkit.Mvvm.ComponentModel;

namespace QuadroApp.Model
{
    public partial class RegelPlanItem : ObservableObject
    {
        public int RegelId { get; init; }
        public string Label { get; init; } = "";

        // US-49 — rijkere weergave in het plan-paneel.
        public string Titel { get; init; } = "";
        public string Afmeting { get; init; } = "";
        public string LijstLabel { get; init; } = "";

        [ObservableProperty] private bool isSelected;
    }
}
