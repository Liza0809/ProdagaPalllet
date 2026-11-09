using System.Windows;
using PdfSharp.Fonts;
using PalletApp.Services;

namespace pallet
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Разрешаем PdfSharp использовать системные шрифты Windows
            GlobalFontSettings.FontResolver = new WindowsFontResolver();
        }
    }
}