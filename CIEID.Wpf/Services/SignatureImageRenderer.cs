using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CIEID.Wpf.Services
{
    /// <summary>Genera l'immagine PNG della firma grafica (nome in corsivo, font Allura).</summary>
    public static class SignatureImageRenderer
    {
        public static string Render(string name, string serial)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIE-Middleware-NG");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, (string.IsNullOrEmpty(serial) ? "firma" : serial) + "_default.png");

            const int W = 600, H = 200;
            FontFamily ff = new FontFamily(new Uri("pack://application:,,,/Assets/"), "./#Allura");
            Typeface tf = new Typeface(ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double size = 80;
            FormattedText ft;
            do
            {
                ft = new FormattedText(name ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size, Brushes.Black, 1.0);
                size -= 4;
            } while (ft.Width > W - 20 && size > 16);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawText(ft, new Point((W - ft.Width) / 2, (H - ft.Height) / 2));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
            return path;
        }
    }
}
