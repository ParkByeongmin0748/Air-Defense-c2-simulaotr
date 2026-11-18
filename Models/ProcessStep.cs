using CommunityToolkit.Mvvm.ComponentModel;

namespace AirDefenseC2.Models;

/// <summary>PROCESS 패널의 킬체인 단계 표시 항목</summary>
public partial class ProcessStep : ObservableObject
{
    public ProcessStep(int index, string name)
    {
        Index = index;
        Name = name;
    }

    public int Index { get; }
    public string Name { get; }
    public bool ShowArrow => Index > 1;

    [ObservableProperty] private StepState _state;
    [ObservableProperty] private string _detail = "-";
}
