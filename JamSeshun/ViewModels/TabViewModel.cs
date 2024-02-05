using JamSeshun.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.ViewModels
{
    internal class TabViewModel : ViewModelBase
    {
        private readonly Tab tab;

        public TabViewModel()
        {
        }

        public TabViewModel(Tab tab)
        {
            this.tab = tab;
        }
    }
}
