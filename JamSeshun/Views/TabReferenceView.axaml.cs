using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views
{
    public partial class TabReferenceView : UserControl
    {
        public TabReferenceView()
        {
            InitializeComponent();
        }

        public TabReferenceView(TabReferenceViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
