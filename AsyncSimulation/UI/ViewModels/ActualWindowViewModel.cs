namespace AsyncSimulation.UI.ViewModels;

public class ActualWindowViewModel : ViewModelBase
{
    private const string Pending = "(pending)";

    private string _s1 = Pending, _s2 = Pending, _s3 = Pending, _s4 = Pending, _s5 = Pending, _s6 = Pending;
    private string _result = Pending;

    public string S1 { get => _s1; set => SetProperty(ref _s1, value); }
    public string S2 { get => _s2; set => SetProperty(ref _s2, value); }
    public string S3 { get => _s3; set => SetProperty(ref _s3, value); }
    public string S4 { get => _s4; set => SetProperty(ref _s4, value); }
    public string S5 { get => _s5; set => SetProperty(ref _s5, value); }
    public string S6 { get => _s6; set => SetProperty(ref _s6, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public void SetSlot(int index, string value)
    {
        switch (index)
        {
            case 1: S1 = value; break;
            case 2: S2 = value; break;
            case 3: S3 = value; break;
            case 4: S4 = value; break;
            case 5: S5 = value; break;
            case 6: S6 = value; break;
        }
    }

    public void SetFinal(string value) => Result = value;

    public void Reset() => S1 = S2 = S3 = S4 = S5 = S6 = Result = Pending;
}
