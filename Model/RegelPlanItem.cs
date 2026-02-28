using CommunityToolkit.Mvvm.ComponentModel;

namespace QuadroApp.Model
{
    public partial class RegelPlanItem : ObservableObject
    {
        public int RegelId { get; init; }
        public string Label { get; init; } = "";

        [ObservableProperty] private bool isSelected;
    }
}
