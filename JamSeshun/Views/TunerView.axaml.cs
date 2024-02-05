using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views
{
    public partial class TunerView : UserControl
    {
        public TunerView()
        {
            InitializeComponent();
        }

        public TunerView(TunerViewModel viewModel) : this()
        {
            this.DataContext = viewModel;
        }
    }
}
