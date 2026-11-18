using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CIEID
{
    class PdfPreview
    {
        private Panel prPanel;
        private string filePath;
        private string tmpFolderName;
        private string signImagePath;
        private string mutoolPath;
        private int pageNumber;
        private int pdfNumPages;
        private PictureBox pbPdfPreview;
        private MoveablePictureBox signPicture;

        private const int MutoolTimeoutMs = 30000;

        // Esegue mutool passando gli argomenti come lista (nessun problema di quoting/injection),
        // con timeout, lettura asincrona dell'output e rilascio del processo.
        private string runMutool(params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = mutoolPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string a in args)
                psi.ArgumentList.Add(a);

            using (var proc = new Process { StartInfo = psi })
            {
                proc.Start();
                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();

                if (!proc.WaitForExit(MutoolTimeoutMs))
                {
                    try { proc.Kill(true); } catch (Exception) { }
                    throw new TimeoutException("mutool non ha risposto entro " + MutoolTimeoutMs + " ms");
                }
                proc.WaitForExit();
                stderr.Wait();
                return stdout.Result;
            }
        }

        private static readonly Regex pagesRegex = new Regex(@"Pages: (\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private int getPdfNumPages(string fileName)
        {
            string output = runMutool("info", fileName);
            Match m = pagesRegex.Match(output);
            if (!m.Success)
                return 0;

            int pdf_page_number = int.Parse(m.Groups[1].Value);
            Program.Logger.Debug("Numero di pagine del pdf: " + pdf_page_number);
            return pdf_page_number;
        }

        public int getPdfPages()
        {
            return this.pdfNumPages;
        }

        public string getSignImagePath()
        {
            return signImagePath;
        }

        public int getPdfPageNumber()
        {
            return this.pageNumber;
        }

        public PdfPreview(Panel previewPanel, string pdfPath, string imagePath)
        {
            this.prPanel = previewPanel;
            this.filePath = pdfPath;
            this.signImagePath = imagePath;
            this.tmpFolderName = Path.GetTempPath() + "IPZS";
            this.pageNumber = 1;

            if (!System.IO.Directory.Exists(tmpFolderName))
            {
                System.IO.Directory.CreateDirectory(tmpFolderName);
            }

            this.mutoolPath = Path.Combine(tmpFolderName, "mutool.exe");

            // Si riscrive l'eseguibile se manca o se differisce dalla risorsa incorporata
            // (evita di eseguire un mutool.exe sostituito da terzi nella cartella temporanea).
            byte[] mutoolBytes = CIEID.Properties.Resources.mutool;
            if (!File.Exists(mutoolPath) || !File.ReadAllBytes(mutoolPath).AsSpan().SequenceEqual(mutoolBytes))
                File.WriteAllBytes(mutoolPath, mutoolBytes);

            pdfNumPages = getPdfNumPages(filePath);


            Bitmap signImage;
            using (Image original = loadImageNoLock(signImagePath))
                signImage = new Bitmap(original, 90, 27);
            signImage.MakeTransparent();

            signPicture = new MoveablePictureBox();
            signPicture.Image = signImage;
            signPicture.BackColor = Color.Transparent;
            signPicture.Width = signImage.Width;
            signPicture.Height = signImage.Height;
            signPicture.BringToFront();
            signPicture.BorderStyle = BorderStyle.FixedSingle;

            pbPdfPreview = new PictureBox();

            pbPdfPreview.Controls.Add(signPicture);
            pbPdfPreview.Update();

            prPanel.Controls.Add(pbPdfPreview);
            freeTempFolder();
            showPreview();
        }

        public void pdfPreviewRemoveObjects()
        {
            this.signPicture.Hide();
            this.pbPdfPreview.Controls.Remove(signPicture);
            this.pbPdfPreview.Hide();
            this.prPanel.Controls.Remove(pbPdfPreview);
        }

        public Dictionary<string,float> getSignImageInfos()
        {
            Dictionary<string, float> signImageInfo = new Dictionary<string, float>();

            //X, Y, Width e Height signImage
            //page number

            float x = (float)signPicture.Left / (float)pbPdfPreview.Width;
            float y = (float)(signPicture.Bottom) / (float)pbPdfPreview.Height;
            float w = ((float)signPicture.Width / (float)pbPdfPreview.Width);
            float h = ((float)signPicture.Height / (float)pbPdfPreview.Height);

            signImageInfo["x"] = x;
            signImageInfo["y"] = y;
            signImageInfo["w"] = w;
            signImageInfo["h"] = h;
            signImageInfo["pageNumber"] = pageNumber - 1;

            return signImageInfo;
        }

        public void pageUp()
        {
            if (pageNumber - 1 >= 1)
            {
                pageNumber -= 1;
            }

            showPreview();
        }

        public void pageDown()
        {
            if (this.pdfNumPages >= pageNumber + 1)
            {
                pageNumber += 1;
            }

            showPreview();
        }

        public void freeTempFolder()
        {
            foreach (string file in Directory.GetFiles(tmpFolderName, "*.png"))
            {
                try { File.Delete(file); }
                catch (IOException ex) { Program.Logger.Error("freeTempFolder - impossibile eliminare " + file + ": " + ex.Message); }
            }
        }

        // Carica un'immagine in memoria senza mantenere il file bloccato.
        private static Image loadImageNoLock(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var tmp = Image.FromStream(fs))
                return new Bitmap(tmp);
        }

        public void showPreview()
        {
            string img_file_name = Path.Combine(tmpFolderName, string.Format("page{0}.png", pageNumber));

            if (!File.Exists(img_file_name))
            {
                runMutool("draw", "-o", Path.Combine(tmpFolderName, "page%d.png"), filePath, pageNumber.ToString());
            }

            bool landscape;
            Bitmap croppedImage;
            using (Image img = loadImageNoLock(img_file_name))
            {
                landscape = img.Width > img.Height;
                if (landscape)
                {
                    pbPdfPreview.Width = pbPdfPreview.Parent.Width - 10;
                    pbPdfPreview.Height = pbPdfPreview.Parent.Height;
                    pbPdfPreview.Left = 5;
                    pbPdfPreview.Top = 0;
                }
                else
                {
                    pbPdfPreview.Width = (pbPdfPreview.Parent.Width) / 2;
                    pbPdfPreview.Height = pbPdfPreview.Parent.Height;
                    pbPdfPreview.Left = (pbPdfPreview.Parent.Width) / 4;
                    pbPdfPreview.Top = 0;
                }

                croppedImage = new Bitmap(img, pbPdfPreview.Width, pbPdfPreview.Height);
            }

            var old = pbPdfPreview.Image;
            pbPdfPreview.Image = croppedImage;
            old?.Dispose();
            pbPdfPreview.SendToBack();
        }

    }
}
