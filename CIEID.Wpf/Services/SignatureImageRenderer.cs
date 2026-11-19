using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CIEID.Wpf.Services
{
    public enum CaseMode { Title, Upper, Lower, AsTyped }

    /// <summary>Genera e gestisce l'immagine PNG della firma grafica di ogni carta.</summary>
    public static class SignatureImageRenderer
    {
        private const int W = 600, H = 200;
        public const string DefaultFont = "Allura";

        private static readonly string[] Candidates =
            { "Segoe Script", "Ink Free", "Lucida Handwriting", "Mistral", "Brush Script MT", "Freestyle Script", "Segoe Print" };

        /// <summary>Caratteri disponibili per la firma (Allura incluso nell'app + corsivi installati).</summary>
        public static IList<string> AvailableFonts()
        {
            List<string> l = new List<string> { DefaultFont };
            HashSet<string> sys = new HashSet<string>(Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);
            foreach (string c in Candidates) if (sys.Contains(c)) l.Add(c);
            return l;
        }

        /// <summary>Percorso dell'immagine di firma della carta (una per numero di serie).</summary>
        public static string PathFor(string serial)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIE-Middleware-NG");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, (string.IsNullOrEmpty(serial) ? "firma" : serial) + "_default.png");
        }

        /// <summary>Restituisce l'immagine di firma della carta, generandola dal nome se non esiste.</summary>
        public static string Ensure(string name, string serial)
        {
            string path = PathFor(serial);
            if (!File.Exists(path)) Save(ComposeText(name, DefaultFont, CaseMode.Title), path);
            return path;
        }

        /// <summary>Rigenera l'immagine predefinita (nome del titolare in corsivo).</summary>
        public static string Reset(string name, string serial)
        {
            string path = PathFor(serial);
            Save(ComposeText(name, DefaultFont, CaseMode.Title), path);
            return path;
        }

        /// <summary>Usa un'immagine scelta dall'utente (qualsiasi formato supportato), salvata come PNG.</summary>
        public static string Import(string file, string serial)
        {
            string path = PathFor(serial);
            Save(RemoveWhiteBackground(Load(file)), path);
            return path;
        }

        /// <summary>Rende trasparente lo sfondo chiaro di un'immagine opaca (le immagini già trasparenti restano invariate).</summary>
        public static BitmapSource RemoveWhiteBackground(BitmapSource src)
        {
            FormatConvertedBitmap conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            byte[] px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            for (int i = 0; i < px.Length; i += 4) if (px[i + 3] < 255) return conv;
            for (int i = 0; i < px.Length; i += 4)
            {
                int min = Math.Min(px[i], Math.Min(px[i + 1], px[i + 2]));
                if (min >= 245) px[i + 3] = 0;
                else if (min > 190) px[i + 3] = (byte)((245 - min) * 255 / 55);
            }
            BitmapSource res = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
            res.Freeze();
            return res;
        }

        public static void Delete(string serial)
        {
            try { File.Delete(PathFor(serial)); } catch (Exception) { }
        }

        public static BitmapSource Load(string path)
        {
            if (!File.Exists(path)) return null;
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        public static void Save(BitmapSource img, string path)
        {
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        private static string ApplyCase(string text, CaseMode mode)
        {
            text = text ?? "";
            switch (mode)
            {
                case CaseMode.Title: return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
                case CaseMode.Upper: return text.ToUpper();
                case CaseMode.Lower: return text.ToLower();
                default: return text;
            }
        }

        /// <summary>Compone l'immagine con il testo nel carattere scelto, centrato e adattato alle dimensioni.</summary>
        public static BitmapSource ComposeText(string text, string fontName, CaseMode mode)
        {
            text = ApplyCase(text, mode);
            FontFamily ff = fontName == DefaultFont || string.IsNullOrEmpty(fontName)
                ? new FontFamily(new Uri("pack://application:,,,/Assets/"), "./#Allura")
                : new FontFamily(fontName);
            Typeface tf = new Typeface(ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double size = 90;
            FormattedText ft;
            do
            {
                ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size, Brushes.Black, 1.0);
                size -= 4;
            } while ((ft.Width > W - 20 || ft.Height > H - 10) && size > 12);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
                dc.DrawText(ft, new Point((W - ft.Width) / 2, (H - ft.Height) / 2));
            return ToBitmap(dv);
        }

        /// <summary>Compone l'immagine a partire dal tratto disegnato a mano, scalato per riempire l'area.</summary>
        public static BitmapSource ComposeInk(StrokeCollection strokes)
        {
            Rect b = strokes.GetBounds();
            if (b.IsEmpty || b.Width <= 0 || b.Height <= 0) return null;
            double scale = Math.Min((W - 20) / b.Width, (H - 20) / b.Height);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform((W - b.Width * scale) / 2, (H - b.Height * scale) / 2));
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.PushTransform(new TranslateTransform(-b.X, -b.Y));
                strokes.Draw(dc);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            return ToBitmap(dv);
        }

        private static BitmapSource ToBitmap(DrawingVisual dv)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}
