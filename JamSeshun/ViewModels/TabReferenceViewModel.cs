using JamSeshun.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.ViewModels
{
    public class TabReferenceViewModel : ViewModelBase
    {
        private readonly TabReference tabReference;

        public TabReferenceViewModel()
        {
            this.tabReference = new("Lorem Ipsum", "Dolar Set", 1, "Tab", 1, 0.9m, null);
        }

        public TabReferenceViewModel(TabReference tabReference)
        {
            this.tabReference = tabReference;
        }

        public string Artist => this.tabReference.Artist;

        public string Song => this.tabReference.Song;
    }
}
