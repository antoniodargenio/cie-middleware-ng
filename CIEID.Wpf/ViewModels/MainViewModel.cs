using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CIEID.Wpf.ViewModels
{
    /// <summary>Base di ogni pagina: titolo, icona (glifo Segoe Fluent) e testo di aiuto contestuale.</summary>
    public abstract class PageViewModel : ObservableObject
    {
        public abstract string Title { get; }
        public abstract string Glyph { get; }
        public abstract string Help { get; }
    }

    public partial class MainViewModel : ObservableObject
    {
        public List<PageViewModel> Items { get; private set; }

        [ObservableProperty]
        private PageViewModel current;

        public MainViewModel()
        {
            Items = new List<PageViewModel>
            {
                new CardsViewModel(),
                new SignViewModel(),
                new VerifyViewModel(),
                new SettingsViewModel(),
                new LogViewModel()
            };
            Current = Items[0];
        }
    }
}
