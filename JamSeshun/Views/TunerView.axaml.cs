using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views
{
    public partial class TunerView : UserControl
    {
        public TunerView()
        {
            this.InitializeComponent();
        }

        public TunerView(TunerViewModel viewModel) : this()
        {
            this.DataContext = viewModel;
        }
    }
}
