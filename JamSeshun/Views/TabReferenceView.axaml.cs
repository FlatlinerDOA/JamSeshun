using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views
{
    public partial class TabReferenceView : UserControl
    {
        public TabReferenceView()
        {
            this.InitializeComponent();
        }

        public TabReferenceView(TabReferenceViewModel viewModel)
        {
            this.InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
