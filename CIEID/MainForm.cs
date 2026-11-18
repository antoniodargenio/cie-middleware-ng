using CIEID.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CIEID
{
    public partial class mainForm : Form
    {
        public const int CKR_OK = 0x00000000;
        public const int CKR_CANCEL = 0x00000001;
        public const int CKR_TOKEN_NOT_PRESENT = 0x000000E0;
        public const int CKR_TOKEN_NOT_RECOGNIZED = 0x000000E1;
        public const int CKR_DEVICE_ERROR = 0x00000030;
        public const int CKR_GENERAL_ERROR = 0x00000005;
        public const int CKR_PIN_INCORRECT = 0x000000A0;
        public const int CKR_PIN_INVALID = 0x000000A1;
        public const int CKR_PIN_LEN_RANGE = 0x000000A2;
        public const int CARD_ALREADY_ENABLED = 0x000000F0;
        public const int CARD_PAN_MISMATCH = 0x000000F1;
        public const int CARD_TSA_ERROR = 0x000000F2;
        public const UInt32 INVALID_FILE_TYPE = 0x84000005;

        /* CKR_PIN_EXPIRED and CKR_PIN_LOCKED are new for v2.0 */
        public const int CKR_PIN_EXPIRED = 0x000000A3;
        public const int CKR_PIN_LOCKED = 0x000000A4;

        public const int ENROLLED = 1;
        public const int NOT_ENROLLED = 0;

        private PdfPreview pdfPreview = null;
        private enum OperationSelectedState
        {
            NO_OP = 0,
            PADES_SIGNATURE = 1,
            CADES_SIGNATURE = 2,
            VERIFY = 3,
        }

        private int signFontSize = 120;

        private OperationSelectedState signOp = OperationSelectedState.NO_OP;

        delegate long ProgressCallback(int progress, string message);
        delegate long CompletedCallback(string pan, string name, string ef_seriale);
        delegate long SignCompletedCallback(int retValue);

        [DllImport("CIEPKI-NG.dll")]
        static extern int VerificaCIEAbilitata(string pan);

        [DllImport("CIEPKI-NG.dll")]
        static extern int DisabilitaCIE(string pan);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int isCIEEnrolled(StringBuilder pan);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int AbbinaCIE(string szPAN, string szPIN, int[] attempts, ProgressCallback progressCallBack, CompletedCallback completedCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int ChangePIN(string szPIN, string szNewPIN, int[] attempts, ProgressCallback progressCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int UnlockPIN(string szPUK, string szNewPIN, int[] attempts, ProgressCallback progressCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int firmaConCIE(string inFilePath, string type, string pin, string pan, int page, float x, float y, float w, float h, string imagePathFile, string outFilePath, ProgressCallback progressCallBack, SignCompletedCallback signCompletedCallBack, string tsaUrl, string tsaUsername, string tsaPassword);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int estraiP7m(string inFilePath, string outFilePath);

        private bool shouldSignWithoutCIEPairing = false;

        private String PANForOneShotSigning = String.Empty;

        private CieCollection cieColl;

        internal CieCollection CieColl { get => cieColl; set => cieColl = value; }

        private CarouselControl carouselControl;

        private Logger Logger;

        public mainForm(string arg)
        {
            Logger = Program.Logger;
            Logger.Info("Inizializzo form principale");

            InitializeComponent();
            Text += " – v" + typeof(Program).Assembly.GetName().Version;
            unofficialDisclaimerLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pairingDisclaimerLabel = new Label();
            pairingDisclaimerLabel.Name = "pairingDisclaimerLabel";
            pairingDisclaimerLabel.Text = unofficialDisclaimerLabel.Text;
            pairingDisclaimerLabel.Font = unofficialDisclaimerLabel.Font;
            pairingDisclaimerLabel.ForeColor = unofficialDisclaimerLabel.ForeColor;
            pairingDisclaimerLabel.TextAlign = unofficialDisclaimerLabel.TextAlign;
            pairingDisclaimerLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pairingDisclaimerLabel.SetBounds(unofficialDisclaimerLabel.Left, 490, unofficialDisclaimerLabel.Width, unofficialDisclaimerLabel.Height);
            CIEPairingTabPage.Controls.Add(pairingDisclaimerLabel);
            CIEPickerTabPage.Resize += (s, e) => FitDisclaimerLabel();
            CIEPairingTabPage.Resize += (s, e) => FitDisclaimerLabel();
            this.DpiChanged += (s, e) => FitDisclaimerLabel();
            this.Shown += (s, e) => FitDisclaimerLabel();

            // Focus automatico sulla prima casella del PIN di abbinamento: all'avvio la scheda e' gia'
            // selezionata (SelectedIndexChanged non scatta), quindi si usa la visibilita' della scheda.
            CIEPairingTabPage.VisibleChanged += (s, e) => FocusPairingFirstPin();
            this.Shown += (s, e) => FocusPairingFirstPin();
            this.Activated += (s, e) => FocusPairingFirstPin();

            // Pagina Aiuto: precisa che il supporto non e' fornito dall'ente emittente ma dalla community.
            Label helpSupportLabel = new Label();
            helpSupportLabel.Name = "helpSupportLabel";
            helpSupportLabel.Text = "Questa versione non è ufficialmente supportata dall'IPZS. " +
                "Essendo un fork pubblico, riceve supporto dagli sviluppatori su GitHub. " +
                "Le informazioni qui sotto si riferiscono al servizio CIE ufficiale.";
            helpSupportLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Bold);
            helpSupportLabel.ForeColor = Color.Red;
            helpSupportLabel.TextAlign = ContentAlignment.MiddleCenter;
            helpSupportLabel.SetBounds(9, 2, tabPageHelpWebBrowser.Width - 18, 40);
            helpSupportLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tabPageHelpWebBrowser.Controls.Remove(pictureBox11);
            tabPageHelpWebBrowser.Controls.Remove(pictureBox13);
            webBrowserHelp.SetBounds(0, 44, webBrowserHelp.Width, tabPageHelpWebBrowser.Height - 44);
            webBrowserHelp.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabPageHelpWebBrowser.Controls.Add(helpSupportLabel);
            // Scheda Informazioni: via i loghi istituzionali, avviso sul trattamento dei dati del fork.
            tabPageInformationWebBrowser.Controls.Remove(pictureBox8);
            tabPageInformationWebBrowser.Controls.Remove(pictureBox9);
            tabPageInformationWebBrowser.Controls.Remove(pictureBox10);
            Label infoPrivacyLabel = new Label();
            infoPrivacyLabel.Name = "infoPrivacyLabel";
            infoPrivacyLabel.Text = "Questo fork effettua lo stesso trattamento dei dati del software originale, " +
                "ma l'IPZS non è più il responsabile del trattamento dei dati. " +
                "Per le informazioni sul trattamento dei dati personali vale quanto indicato di seguito per il software originale.";
            infoPrivacyLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Bold);
            infoPrivacyLabel.ForeColor = Color.Red;
            infoPrivacyLabel.TextAlign = ContentAlignment.MiddleCenter;
            infoPrivacyLabel.SetBounds(9, 2, tabPageInformationWebBrowser.Width - 18, 52);
            infoPrivacyLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            webBrowserInfo.SetBounds(0, 56, webBrowserInfo.Width, tabPageInformationWebBrowser.Height - 56);
            webBrowserInfo.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabPageInformationWebBrowser.Controls.Add(infoPrivacyLabel);
            enableTimestampCheckBox.Checked = Properties.Settings.Default.timestampEnabled;
            enableTimestampCheckBox.CheckedChanged += (s, e) =>
            {
                Properties.Settings.Default.timestampEnabled = enableTimestampCheckBox.Checked;
                Properties.Settings.Default.Save();
            };

            if ("unlock".Equals(arg))
            {
                SelectUnlockPIN();
            }
            else if ("changepin".Equals(arg))
            {
                SelectChangePIN();
            }
            else
            {
                SelectHome();
            }
        }

        void TextBox_Paint(object sender, PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics, ((Control)sender).DisplayRectangle, Color.LightGray, ButtonBorderStyle.Solid);
        }

        long PairingProgress(int progress, string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                progressBar.Value = progress;
                labelProgressMessage.Text = message;
            });

            return 0;
        }

        long SignProgress(int progress, string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                signProgressBar.Value = progress;
            });

            return 0;
        }

        private string getSignImagePath(string efSeriale)
        {
            Logger.Info("getSignImagePath() - Inizia funzione");
            string appdataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.Format("{0}\\CIE-Middleware-NG\\{1}_default.png", appdataPath, efSeriale);
        }

        long SignCompleted(int retValue)
        {
            Logger.Info("SignCompleted() - Inizia funzione");
            this.Invoke((MethodInvoker)delegate
            {
                if (retValue != 0)
                {
                    documentSuccessfullySignedLabel.Text = (retValue == 30)
                        ? "Marca temporale non ottenuta: verifica la connessione o disattiva la marca temporale"
                        : "Si è verificato un errore";
                    Logger.Debug(documentSuccessfullySignedLabel.Text);
                    digitalSignatureCompletedPictureBox.Image = Properties.Resources.cross;
                    digitalSignatureCompletedPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                }
                else
                {
                    documentSuccessfullySignedLabel.Text = "File firmato con successo";
                    Logger.Debug(documentSuccessfullySignedLabel.Text);
                    digitalSignatureCompletedPictureBox.Image = Properties.Resources.check;
                    digitalSignatureCompletedPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                }

                signProgressBar.Hide();
                typePINLabel.Hide();
                placeCIEOnReaderLabel.Hide();
                cancelSigningOperationPINPanelButton.Hide();
                signButton.Hide();
                documentSuccessfullySignedLabel.Show();
                documentSuccessfullySignedLabel.Update();
                digitalSignatureCompletedPictureBox.Show();
                digitalSignatureCompletedPictureBox.Update();
                closeButton.Show();
                closeButton.Update();
            });

            return 0;
        }

        long PairingCompleted(string pan, string name, string efSeriale)
        {
            Logger.Info("PairingCompleted() - Inizia funzione");

            // Un'eccezione managed non deve attraversare il confine nativo (il chiamante la vedrebbe come errore generico).
            try
            {
                string defaultSignImagePath = getSignImagePath(efSeriale);
                CieColl.addCie(pan, new CieModel(efSeriale, name, pan));

                if (shouldSignWithoutCIEPairing)
                    PANForOneShotSigning = pan;

                Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
                Properties.Settings.Default.Save();

                Logger.Debug(string.Format("Immagine firma salvata in: {0}", defaultSignImagePath));

                TextInfo nameInfo = new CultureInfo("it-IT", false).TextInfo;
                DrawText(nameInfo.ToTitleCase(name.ToLower()), Color.Black, defaultSignImagePath);

                Logger.Debug("CIE abbinate dopo aggiunta: " + Properties.Settings.Default.cieList);

                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("PairingCompleted() - Errore: " + ex);
                return -1;
            }
        }

        long ChangePINProgress(int progress, string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                progressBarChangePIN.Value = progress;
                progressLabelChangePIN.Text = message;
            });

            return 0;
        }

        long UnlockPINProgress(int progress, string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                progressBarUnlockPIN.Value = progress;
                progressLabelUnlockPIN.Text = message;
            });

            return 0;
        }

        private void PINDigit_TextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            int tag = Int16.Parse((String)textBox.Tag);

            if (e.KeyChar >= '0' && e.KeyChar <= '9')
            {
                if (tag < 8)
                {
                    Control nextTextBox = FindControlByTag(this.Controls, "" + (tag + 1));
                    nextTextBox.Focus();
                }
            }

            else if (e.KeyChar == 8) // backspace
            {
                if (tag > 1)
                {
                    Control previousTextBox = FindControlByTag(this.Controls, "" + (tag - 1));
                    previousTextBox.Focus();
                }
            }

            else if (e.KeyChar == 13) // enter
            {
                if (tag == 8)
                    PairButton_Click(sender, e);
            }

            else
                e.Handled = true;

            if (textBox.Text != String.Empty)
                textBox.Text = String.Empty;

        }

        private void TextBoxSignPin_KeyPress(object sender, KeyPressEventArgs e)
        {
            byte numPINDigits = (byte)((shouldSignWithoutCIEPairing) ? 4 : 0);

            TextBox textBox = (TextBox)sender;
            int tag = Int16.Parse((String)textBox.Tag);

            if (e.KeyChar >= '0' && e.KeyChar <= '9')
            {
                if (tag < 12 + numPINDigits)
                {
                    Control nextTextBox = FindControlByTag(this.Controls, "" + (tag + 1));
                    nextTextBox.Focus();
                }
                else
                {
                    signButton.Enabled = true;
                }
            }

            else if (e.KeyChar == 8) // Backspace
            {
                if (tag > 9)
                {
                    Control previousTextBox = FindControlByTag(this.Controls, "" + (tag - 1));
                    previousTextBox.Focus();
                }
            }

            else if (e.KeyChar == 13) // Enter
            {
                if (tag == 12 + numPINDigits)
                {
                    SignButton_Click(sender, e);
                }
            }

            else
                e.Handled = true;

            if (textBox.Text != String.Empty)
                textBox.Text = String.Empty;
        }

        private Control FindControlByTag(Control.ControlCollection controls, object tag)
        {
            Control control = null;
            foreach (Control c in controls)
            {
                if (tag.Equals(c.Tag))
                {
                    control = c;
                }
                else
                {
                    if (c.HasChildren)
                        control = FindControlByTag(c.Controls, tag); //Recursively check all children controls as well; ie groupboxes or tabpages
                }

                if (control != null)
                    return control;
            }

            return null;
        }


        private void ConfigureHomeButtons(CieCollection cieColl)
        {
            Logger.Info("configureHomeButtons() - Inizia funzione");

            if (cieColl.MyDictionary.Count > 1)
            {
                int size_x = tableLayoutPanel1.Size.Width;
                int size_y = tableLayoutPanel1.Size.Height;
                int remaining_space = (size_x - (3 * (deleteCIEButton.Width))) / 4;

                removeAllButton.Location = new Point(remaining_space, tableLayoutPanel1.Location.Y + size_y + 10);
                deleteCIEButton.Location = new Point(remaining_space + removeAllButton.Width + removeAllButton.Location.X, removeAllButton.Location.Y);
                addButton.Location = new Point(remaining_space + removeAllButton.Width + deleteCIEButton.Location.X, removeAllButton.Location.Y);
                removeAllButton.Visible = true;
            }
            else
            {
                int size_x = tableLayoutPanel1.Size.Width;
                int size_y = tableLayoutPanel1.Size.Height;
                int remaining_space = (size_x - (2 * (deleteCIEButton.Width))) / 3;

                removeAllButton.Visible = false;
                deleteCIEButton.Location = new Point(remaining_space, tableLayoutPanel1.Location.Y + size_y + 10);
                addButton.Location = new Point(2 * remaining_space + deleteCIEButton.Width, deleteCIEButton.Location.Y);
            }

            if (CieColl.MyDictionary.Count >= 1)
            {
                int size_x = CIEPairingTabPage.Size.Width;
                int size_y = CIEPairingTabPage.Size.Height;
                int remaining_space = (size_x - 2 * ((pairButton.Width))) / 3;

                FitDisclaimerLabel(pairingDisclaimerLabel);
                int bottomLimit = pairingDisclaimerLabel != null ? pairingDisclaimerLabel.Top : size_y;
                int height = bottomLimit - pairButton.Height - 10;

                cancelButton.Location = new Point(remaining_space, height);
                pairButton.Location = new Point(2 * remaining_space + cancelButton.Width, cancelButton.Location.Y);
                cancelButton.Visible = true;
            }
            else
            {
                int size_x = CIEPairingTabPage.Size.Width;
                int size_y = CIEPairingTabPage.Size.Height;
                int remaining_space = (size_x - ((pairButton.Width))) / 2;
                FitDisclaimerLabel(pairingDisclaimerLabel);
                int bottomLimit = pairingDisclaimerLabel != null ? pairingDisclaimerLabel.Top : size_y;
                int height = bottomLimit - pairButton.Height - 10;

                pairButton.Location = new Point(remaining_space, height);
                cancelButton.Visible = false;
            }
        }

        private void MenuHomeButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuHomeButton_Click() - Inizia funzione");
            SelectHome();
        }

        private void ChangeHomeObjects()
        {
            Logger.Info("ChangeHomeObjects() - Inizia funzione");

            appNameCIEPickerHeaderTextLabel.Text = "Firma Elettronica";
            CIESuccessfullyPairedLabel.Text = "Seleziona la CIE da usare";

            deleteCIEButton.Visible = false;
            removeAllButton.Visible = false;
            addButton.Visible = false;
            selectSigningCIEButton.Visible = true;
            signWithCIEWithoutPairingButton.Visible = true;

            mainTabControl.SelectedIndex = 1;
        }

        private void CreateImages(CieCollection CieColl)
        {
            Logger.Info("createImages() - Inizia funzione");


            /*
            string defaultSignImagePath = getSignImagePath(efSeriale);
            CieColl.addCie(pan, new CieModel(efSeriale, name, pan));

            Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
            Properties.Settings.Default.Save();

            Logger.Debug(string.Format("Immagine firma salvata in: {0}", defaultSignImagePath));

            TextInfo nameInfo = new CultureInfo("it-IT", false).TextInfo;
            DrawText(nameInfo.ToTitleCase(name.ToLower()), Color.Black, defaultSignImagePath);
            */
        }

        private void SelectHome()
        {
            Logger.Info("SelectHome() - Inizia funzione");

            deleteCIEButton.Visible = true;
            removeAllButton.Visible = true;
            addButton.Visible = true;
            selectSigningCIEButton.Visible = false;
            signWithCIEWithoutPairingButton.Visible = false;

            appNameCIEPickerHeaderTextLabel.Text = "CIE ID";
            CIESuccessfullyPairedLabel.Text = "Carta d'Identità Elettronica abbinata correttamente";

            CieColl = new CieCollection(Properties.Settings.Default.cieList);

            if (!Properties.Settings.Default.cardHolder.Equals(""))
            {
                CieColl.addCie(Properties.Settings.Default.serialNumber, new CieModel(Properties.Settings.Default.efSeriale, Properties.Settings.Default.cardHolder, Properties.Settings.Default.serialNumber));

                Properties.Settings.Default.serialNumber = "";
                Properties.Settings.Default.cardHolder = "";
                Properties.Settings.Default.efSeriale = "";
                Properties.Settings.Default.Save();
            }

            ConfigureHomeButtons(cieColl);

            Logger.Debug("Lista CIE abbinate: " + Properties.Settings.Default.cieList);

            if (CieColl.MyDictionary.Count == 0)
            {
                mainTabControl.SelectedIndex = 0;
            }
            else
            {
                if (carouselControl == null)
                {
                    carouselControl = new CarouselControl(tableLayoutPanelCarousel, dotsGroup);
                    carouselControl.ButtonsChanged += CarouselControl_ButtonsChanged;
                }

                carouselControl.LoadData(CieColl);
                carouselControl.UpdateLayout();

                mainTabControl.SelectedIndex = 1;
            }

            menuHomeButton.BackColor = Color.LightGray;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;
        }

        private PrivateFontCollection LoadCustomFont()
        {
            Logger.Info("loadCustomFont() - Inizia funzione");
            //Create your private font collection object.
            PrivateFontCollection pfc = new PrivateFontCollection();

            //Select your font from the resources.
            //My font here is "Digireu.ttf"
            int fontLength = Properties.Resources.Allura_Regular.Length;

            // create a buffer to read in to
            byte[] fontdata = Properties.Resources.Allura_Regular;

            // create an unsafe memory block for the font data
            System.IntPtr data = Marshal.AllocCoTaskMem(fontLength);

            // copy the bytes to the unsafe memory block
            Marshal.Copy(fontdata, 0, data, fontLength);

            // pass the font to the font collection
            pfc.AddMemoryFont(data, fontLength);

            return pfc;
        }

        private void DrawText(String text, Color textColor, String path)
        {
            PrivateFontCollection pfc = LoadCustomFont();

            Font font = new Font(pfc.Families[0], signFontSize);

            //first, create a dummy bitmap just to get a graphics object
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);
            //measure the string to see how big the image needs to be
            SizeF textSize = drawing.MeasureString(text, font);

            //set the stringformat flags to rtl
            StringFormat sf = new StringFormat();
            //uncomment the next line for right to left languages
            //sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            sf.Trimming = StringTrimming.Word;
            //free up the dummy image and old graphics object
            img.Dispose();
            drawing.Dispose();

            //create a new image of the right size
            img = new Bitmap((int)textSize.Width, (int)textSize.Height);

            drawing = Graphics.FromImage(img);
            //Adjust for high quality
            drawing.CompositingQuality = CompositingQuality.HighQuality;
            drawing.InterpolationMode = InterpolationMode.HighQualityBilinear;
            drawing.PixelOffsetMode = PixelOffsetMode.HighQuality;
            drawing.SmoothingMode = SmoothingMode.HighQuality;
            drawing.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            //paint the background
            drawing.Clear(Color.White);

            //create a brush for the text
            Brush textBrush = new SolidBrush(textColor);

            drawing.DrawString(text, font, textBrush, new RectangleF(0, 0, textSize.Width, textSize.Height), sf);
            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();
            img.Save(path, ImageFormat.Png);
            img.Dispose();
        }

        private void SelectPairingProgress()
        {
            mainTabControl.SelectedIndex = 2;
            progressBar.Value = 0;
            progressBar.Maximum = 100;
        }

        private void PairButton_Click(object sender, EventArgs e)
        {
            Logger.Info("PairButton_Click() - Inizia funzione");
            string pin = "";

            int i;
            for (i = 1; i < 9; i++)
            {
                TextBox txtField = (TextBox)FindControlByTag(Controls, "" + i);

                pin += txtField.Text;
            }

            if (pin.Length != 8)
            {
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            char c = pin[0];

            i = 1;
            for (i = 1; i < pin.Length && (c >= '0' && c <= '9'); i++)
            {
                c = pin[i];
            }

            if (i < pin.Length || !(c >= '0' && c <= '9'))
            {
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            for (i = 1; i < 9; i++)
            {
                TextBox txtField = (TextBox)FindControlByTag(Controls, "" + i);
                txtField.Text = "";
            }

            ((Control)sender).Enabled = false;

            mainTabControl.SelectedIndex = 2;

            ThreadStart processTaskThread = delegate { CIEPairing(sender, pin, false); };

            new Thread(processTaskThread).Start();
        }

        private int CIEPairing(object sender, string pin, bool oneShotSignature)
        {
            int ret = -1;

            Logger.Info("CIEPairing() - Inizia funzione");
            try
            {
                int[] attempts = new int[1];

                ret = AbbinaCIE(null, pin, attempts, new ProgressCallback(PairingProgress), new CompletedCallback(PairingCompleted));

                this.Invoke((MethodInvoker)delegate
                {
                    ((Control)sender).Enabled = true;
                    switch (ret)
                    {
                        case CKR_TOKEN_NOT_RECOGNIZED:
                            Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_RECOGNIZED");
                            MessageBox.Show("CIE non presente sul lettore", "Abilitazione CIE", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            SelectHome();
                            break;

                        case CKR_TOKEN_NOT_PRESENT:
                            Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_PRESENT");
                            MessageBox.Show("CIE non presente sul lettore", "Abilitazione CIE", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            SelectHome();
                            break;

                        case CKR_PIN_INCORRECT:
                            Logger.Debug("Il PIN digitato è errato. - CKR_PIN_INCORRECT");
                            MessageBox.Show(String.Format("Il PIN digitato è errato. Rimangono {0} tentativi", attempts[0]), "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            SelectHome();
                            break;

                        case CKR_PIN_LOCKED:
                            Logger.Debug("Carta bloccata - CKR_PIN_LOCKED");
                            MessageBox.Show("Munisciti del codice PUK e utilizza la funzione di sblocco carta per abilitarla", "Carta bloccata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            SelectHome();
                            break;

                        case CKR_GENERAL_ERROR:
                            Logger.Debug("Errore inaspettato durante la comunicazione con la smart card - CKR_GENERAL_ERROR");
                            MessageBox.Show("Errore inaspettato durante la comunicazione con la smart card", "Errore inaspettato", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            SelectHome();
                            break;
                     
                        case CKR_OK:
                            Logger.Debug("L'abilitazione della CIE è avvenuta con successo - CKR_OK");
                            if (!oneShotSignature)
                            {
                                MessageBox.Show("L'abilitazione della CIE è avvenuta con successo", "CIE abilitata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                SelectHome();
                            }
                            break;

                        case CARD_ALREADY_ENABLED:
                            Logger.Debug("Carta già abilitata - CARD_ALREADY_ENABLED");
                            MessageBox.Show("Carta già abilitata", "Carta già abilitata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            SelectHome();
                            break;
                    }
                });

            }
            catch (Exception ex)
            {
                Logger.Error("[ERROR] Abbinamento CIE - eccezione: " + ex);
            }

            return ret;
        }

        private void RemoveExistingGraphicSignature(String serialNumber)
        {
            if (File.Exists(getSignImagePath(serialNumber)))
            {
                File.Delete(getSignImagePath(serialNumber));
            }
        }

        private void RemoveCIEFromCollection(string PAN)
        {
            int ret = DisabilitaCIE(PAN);

            switch (ret)
            {
                case CKR_OK:
                    Logger.Debug("CIE disabilitata con successo");

                    try
                    {
                        var model = CieColl.MyDictionary[PAN];
                        RemoveExistingGraphicSignature(model.SerialNumber);
                    }

                    catch (Exception ex)
                    {
                        Logger.Error("RemoveCIEFromCollection() - Failed to remove graphic signature image for CIE PAN: " + PAN + " - " + ex.Message);
                    }


                    CieColl.removeCie(PAN);
                    Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
                    Properties.Settings.Default.Save();

                    if (!shouldSignWithoutCIEPairing)
                    { 
                        MessageBox.Show("CIE disabilitata con successo", "CIE disabilitata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SelectHome();
                    }

                    break;

                case CKR_TOKEN_NOT_PRESENT:
                    Logger.Debug("CIE non presente sul lettore");
                    MessageBox.Show("CIE non presente sul lettore", "Disabilitazione CIE", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;

                default:
                    Logger.Debug("Impossibile disabilitare la CIE");
                    MessageBox.Show("Impossibile disabilitare la CIE", "CIE non disabilitata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
            }
        }

        private void DeleteCIEButton_Click(object sender, EventArgs e)
        {
            Logger.Info("DeleteCIEButton_Click() - Inizia funzione");
            var model = carouselControl.ActiveCieModel;

            if (MessageBox.Show(
                        String.Format("Stai rimuovendo la Carta di Identità di {0} dal sistema, per utilizzarla nuovamente dovrai ripetere l'abbinamento.", model.Owner),
                        "Disabilita CIE", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                return;

            RemoveCIEFromCollection(model.Pan);
        }

        private void ChangePINButton_Click(object sender, EventArgs e)
        {
            Logger.Info("ChangePINButton_Click() - Inizia funzione");
            string pin = textBoxPIN.Text;
            string newpin = textBoxNewPIN.Text;
            string newpin2 = textBoxNewPIN2.Text;

            int i;

            if (pin.Length != 8)
            {
                Logger.Debug("Il PIN attuale deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (newpin.Length != 8)
            {
                Logger.Debug("Il nuovo PIN deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }


            char c = pin[0];

            for (i = 1; i < pin.Length && (c >= '0' && c <= '9'); i++)
            {
                c = pin[i];
            }

            if (i < pin.Length || !(c >= '0' && c <= '9'))
            {
                Logger.Debug("Il PIN attuale deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];

            for (i = 1; i < newpin.Length && (c >= '0' && c <= '9'); i++)
            {
                c = newpin[i];
            }

            if (i < newpin.Length || !(c >= '0' && c <= '9'))
            {
                Logger.Debug("Il nuovo PIN deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (!newpin.Equals(newpin2))
            {
                Logger.Debug("I PIN non corrispondono");
                MessageBox.Show("I PIN non corrispondono", "PIN non corrispondenti", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            char lastchar = c;

            for (i = 1; i < newpin.Length && c == lastchar; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre uguali");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre uguali", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            lastchar = (char)((int)c - 1);

            for (i = 1; i < newpin.Length && c == lastchar + 1; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar + 1)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre consecutive");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre consecutive", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            lastchar = (char)(c + 1);

            for (i = 1; i < newpin.Length && c == lastchar - 1; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar - 1)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre consecutive");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre consecutive", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            textBoxPIN.Text = "";
            textBoxNewPIN2.Text = "";
            textBoxNewPIN.Text = "";

            ((Control)sender).Enabled = false;

            mainTabControl.SelectedIndex = 4;

            ThreadStart processTaskThread = delegate { ChangePIN(sender, pin, newpin); };

            new Thread(processTaskThread).Start();
        }

        private void ChangePIN(object sender, string pin, string newpin)
        {
            Logger.Info("ChangePIN() - Inizia funzione");
            int[] attempts = new int[1];

            int ret = ChangePIN(pin, newpin, attempts, ChangePINProgress);

            this.Invoke((MethodInvoker)delegate
            {
                ((Control)sender).Enabled = true;
                switch (ret)
                {
                case CKR_TOKEN_NOT_RECOGNIZED:
                    Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_RECOGNIZED");
                    MessageBox.Show("CIE non presente sul lettore", "Cambio PIN", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_TOKEN_NOT_PRESENT:
                    Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_PRESENT");
                    MessageBox.Show("CIE non presente sul lettore", "Cambio PIN", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_PIN_INCORRECT:
                    Logger.Debug("Il PIN digitato è errato - CKR_PIN_INCORRECT");
                    MessageBox.Show(String.Format("Il PIN digitato è errato. Rimangono {0} tentativi", attempts[0]), "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_PIN_LOCKED:
                    Logger.Debug("Carta bloccata - CKR_PIN_LOCKED");
                    MessageBox.Show("Munisciti del codice PUK e utilizza la funzione di sblocco carta per abilitarla", "Carta bloccata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_GENERAL_ERROR:
                    Logger.Debug("Errore inaspettato durante la comunicazione con la smart card - CKR_GENERAL_ERROR");
                    MessageBox.Show("Errore inaspettato durante la comunicazione con la smart card", "Errore inaspettato", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_OK:
                    Logger.Debug("Il PIN è stato modificato con successo - CKR_OK");
                    MessageBox.Show("Il PIN è stato modificato con successo", "Operazione completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SelectHome();
                    new PINNotice().ShowDialog();
                    break;
                }
            });
        }

        private void UnlockPIN(object sender, string puk, string newpin)
        {
            Logger.Info("UnlockPIN() - Inizia funzione");
            int[] attempts = new int[1];

            long ret = UnlockPIN(puk, newpin, attempts, UnlockPINProgress);

            this.Invoke((MethodInvoker)delegate
            {
                ((Control)sender).Enabled = true;

                switch (ret)
                {
                case CKR_TOKEN_NOT_RECOGNIZED:
                    Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_RECOGNIZED");
                    MessageBox.Show("CIE non presente sul lettore", "Sblocca CIE", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_TOKEN_NOT_PRESENT:
                    Logger.Debug("CIE non presente sul lettore - CKR_TOKEN_NOT_PRESENT");
                    MessageBox.Show("CIE non presente sul lettore", "Sblocca CIE", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_PIN_INCORRECT:
                    Logger.Debug("Il PUK digitato è errato. - CKR_PIN_INCORRECT");
                    MessageBox.Show(String.Format("Il PUK digitato è errato. Rimangono {0} tentativi", attempts[0]), "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_PIN_LOCKED:
                    Logger.Debug("PUK bloccato. - CKR_PIN_LOCKED");
                    MessageBox.Show("PUK bloccato. La tua CIE deve essere sostituita", "Carta bloccata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;

                case CKR_GENERAL_ERROR:
                    Logger.Debug("Errore inaspettato durante la comunicazione con la smart card - CKR_GENERAL_ERROR");
                    MessageBox.Show("Errore inaspettato durante la comunicazione con la smart card", "Errore inaspettato", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;
                case CKR_DEVICE_ERROR:
                    Logger.Debug("Errore inaspettato durante la comunicazione con la smart card - CKR_DEVICE_ERROR");
                    MessageBox.Show("Errore inaspettato durante la comunicazione con la smart card", "Errore inaspettato", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    SelectHome();
                    break;
                case CKR_OK:
                    Logger.Debug("La CIE è stata sbloccata con successo - CKR_OK");
                    MessageBox.Show("La CIE è stata sbloccata con successo", "Operazione completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SelectHome();
                    new PINNotice().ShowDialog();
                    break;
                }
            });
        }

        private void MenuChangePINButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuChangePINButton_Click() - Inizia funzione");
            SelectChangePIN();
        }

        private void SelectChangePIN()
        {
            Logger.Info("SelectChangePIN() - Inizia funzione");
            mainTabControl.SelectedIndex = 3;

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.LightGray;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;
        }

        private void MenuUnlockPINButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuUnlockPINButton_Click() - Inizia funzione");
            SelectUnlockPIN();
        }

        private void SelectUnlockPIN()
        {
            Logger.Info("SelectUnlockPIN() - Inizia funzione");
            mainTabControl.SelectedIndex = 5;

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.LightGray;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;
        }

        private void UnlockPINButton_Click(object sender, EventArgs e)
        {
            Logger.Info("UnlockPINButton_Click() - Inizia funzione");
            string puk = textBoxPUK.Text;
            string newpin = textBoxUnlockPIN.Text;
            string newpin2 = textBoxUnlockPIN2.Text;

            int i;

            if (puk.Length != 8)
            {
                Logger.Debug("Il PUK deve essere composto da 8 numeri");
                MessageBox.Show("Il PUK deve essere composto da 8 numeri", "PUK non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (newpin.Length != 8)
            {
                Logger.Debug("Il PIN deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }


            char c = puk[0];

            i = 1;
            for (i = 1; i < puk.Length && (c >= '0' && c <= '9'); i++)
            {
                c = puk[i];
            }

            if (i < puk.Length || !(c >= '0' && c <= '9'))
            {
                Logger.Debug("Il PUK deve essere composto da 8 numeri");
                MessageBox.Show("Il PUK deve essere composto da 8 numeri", "PUK non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];

            i = 1;
            for (i = 1; i < newpin.Length && (c >= '0' && c <= '9'); i++)
            {
                c = newpin[i];
            }

            if (i < newpin.Length || !(c >= '0' && c <= '9'))
            {
                Logger.Debug("Il PIN deve essere composto da 8 numeri");
                MessageBox.Show("Il PIN deve essere composto da 8 numeri", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (!newpin.Equals(newpin2))
            {
                Logger.Debug("I PIN non corrispondono");
                MessageBox.Show("I PIN non corrispondono", "PIN non corrispondenti", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            char lastchar = c;

            for (i = 1; i < newpin.Length && c == lastchar; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre uguali");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre uguali", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            lastchar = (char)((int)c - 1);

            for (i = 1; i < newpin.Length && c == lastchar + 1; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar + 1)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre consecutive");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre consecutive", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            c = newpin[0];
            lastchar = (char)(c + 1);

            for (i = 1; i < newpin.Length && c == lastchar - 1; i++)
            {
                lastchar = c;
                c = newpin[i];
            }

            if (c == lastchar - 1)
            {
                Logger.Debug("Il nuovo PIN non deve essere composto da cifre consecutive");
                MessageBox.Show("Il nuovo PIN non deve essere composto da cifre consecutive", "PIN non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            textBoxPUK.Text = "";
            textBoxUnlockPIN.Text = "";
            textBoxUnlockPIN2.Text = "";

            ((Control)sender).Enabled = false;

            mainTabControl.SelectedIndex = 6;

            ThreadStart processTaskThread = delegate { UnlockPIN(sender, puk, newpin); };

            new Thread(processTaskThread).Start();
        }

        private void MenuTutorialButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuTutorialButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 7;

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.LightGray;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;
            webBrowserTutorial.Navigate("https://idserver.servizicie.interno.gov.it/idp/tutorial_win.jsp");
        }

        private void MenuHelpButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuHelpButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 8;

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.LightGray;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;

            webBrowserHelp.Navigate("https://idserver.servizicie.interno.gov.it/idp/aiuto.jsp");
        }

        private void MenuInformationButton_Click(object sender, EventArgs e)
        {
            Logger.Info("MenuInformationButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 9;

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.LightGray;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;

            webBrowserInfo.Navigate("https://idserver.servizicie.interno.gov.it/idp/privacy.jsp");
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Logger.Info("MainForm_FormClosed() - Inizia funzione");
            Application.Exit();
        }

        private void PINTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) // enter
            {
                ChangePINButton_Click(sender, e);
            }
        }

        private void PUKTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) // enter
            {
                UnlockPINButton_Click(sender, e);
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            Logger.Info("AddButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 0;
        }

        private void RemoveAllButton_Click(object sender, EventArgs e)
        {
            Logger.Info("RemoveAllButton_Click() - Inizia funzione");
            if (MessageBox.Show(String.Format("Rimuovere tutte le Carte di Identità attualmente abbinate?"),
                                "Disabilita tutte le CIE", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                return;

            String[] arrayCIE = new String[CieColl.MyDictionary.Count];

            for (int i = 0; i < arrayCIE.Count(); i++)
            {
                arrayCIE[i] = CieColl.MyDictionary.ElementAt(i).Key;
            }

            for (int i = 0; i < arrayCIE.Count(); i++)
            {

                int ret = DisabilitaCIE(arrayCIE[i]);

                if (ret != CKR_OK)
                {
                    var cieModel = CieColl.MyDictionary.ElementAt(i).Value;
                    MessageBox.Show("Impossibile disabilitare la CIE numero " + cieModel.SerialNumber, "CIE non disabilitata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
                    Properties.Settings.Default.Save();
                    SelectHome();
                    return;
                }

                CieColl.removeCie(arrayCIE[i]);
                Logger.Debug("CIE con chiave " + arrayCIE[i] + " disabilitata");
            }

            Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
            Properties.Settings.Default.Save();

            Logger.Debug("Cie Rimanenti: " + Properties.Settings.Default.cieList);

            MessageBox.Show("CIE disabilitate con successo", "CIE disabilitate", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SelectHome();
        }

        private void LeftButton_Click(object sender, EventArgs e)
        {
            Logger.Info("LeftButton_Click() - Inizia funzione");
            carouselControl.ShiftRight();
        }

        private void RightButton_Click(object sender, EventArgs e)
        {
            Logger.Info("RightButton_Click() - Inizia funzione");
            carouselControl.ShiftLeft();
        }

        private void CarouselControl_ButtonsChanged(object sender, Controls.CarouselControl.ButtonsEventArgs e)
        {
            Logger.Info("CarouselControl_ButtonsChanged() - Inizia funzione");
            if (e.IsRightButton)
            {
                rightButton.Enabled = e.IsEnabled;
                ToggleButtonVisibility(rightButton, e.IsVisible);
            }
            else
            {
                leftButton.Enabled = e.IsEnabled;
                ToggleButtonVisibility(leftButton, e.IsVisible);
            }
        }

        private void ToggleButtonVisibility(Button button, bool show)
        {
            Logger.Info("toggleButtonVisibility() - Inizia funzione");
            if (show)
            {
                button.Show();
            }
            else
            {
                button.Hide();
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CancelButton_Click() - Inizia funzione");
            SelectHome();
        }

        private void TabPageUnlockPIN_Click(object sender, EventArgs e)
        {
            Logger.Info("TabPageUnlockPIN_Click() - Inizia funzione");
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            Logger.Info("panel1_Paint() - Inizia funzione");
            Panel panel = (Panel)sender;
            float width = (float)5.0;
            Pen pen = new Pen(SystemColors.ControlDark, width);
            pen.DashStyle = DashStyle.Dash;
            e.Graphics.DrawLine(pen, 0, 0, 0, panel.Height - 0);
            e.Graphics.DrawLine(pen, 0, 0, panel.Width - 0, 0);
            e.Graphics.DrawLine(pen, panel.Width - 1, panel.Height - 1, 0, panel.Height - 1);
            e.Graphics.DrawLine(pen, panel.Width - 1, panel.Height - 1, panel.Width - 1, 0);
        }

        private void MenuDigitalSignatureButton_Click(object sender, EventArgs e)
        {
            Logger.Info("menuDigitalSignatureButton_Click() - Inizia funzione");
            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.LightGray;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.Transparent;

            signOp = OperationSelectedState.NO_OP;
            digitalSignatureLabel.Text = "Firma Elettronica";
            
            if (CieColl.MyDictionary.Count > 0)
            {
                ChangeHomeObjects();
                Logger.Debug("ChangeHomeObjects called");
            }

            else
            {
                SignWithCIEWithoutPairingButton_Click(null, null);
                Logger.Debug("SignCIEW/OPairing called");
            }
        }

        private void MenuVerifyButton_Click(object sender, EventArgs e)
        {
            Logger.Info("menuVerifyButton_Click() - Inizia funzione");
            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.LightGray;
            menuSettingsButton.BackColor = Color.Transparent;

            signOp = OperationSelectedState.VERIFY;
            digitalSignatureLabel.Text = "Verifica firma";
            DisplayFileSelectionTab();
        }

        private void CustomizeGraphicSignatureLabel_Click(object sender, EventArgs e)
        {
            Logger.Info("CustomizeGraphicSignatureLabel_Click() - Inizia funzione");

            var model = carouselControl.ActiveCieModel;

            string signImagePath = getSignImagePath(model.SerialNumber);

            if (graphicDigitalSignaturePanel.Controls.Count > 0 && graphicDigitalSignaturePanel.Controls[0] != null)
            {
                graphicDigitalSignaturePanel.Controls[0].Dispose();
            }

            if (!System.IO.File.Exists(signImagePath))
            {
                TextInfo nameInfo = new CultureInfo("it-IT", false).TextInfo;
                string name = model.Owner;
                DrawText(nameInfo.ToTitleCase(name.ToLower()), Color.Black, signImagePath);
            }

            PictureBox signPicture = new PictureBox();
            signPicture.BackColor = Color.Transparent;
            signPicture.Width = graphicDigitalSignaturePanel.Width;
            signPicture.Height = graphicDigitalSignaturePanel.Height;

            Image image;
            using (Stream stream = File.OpenRead(signImagePath))
            {
                image = Image.FromStream(stream);
            }

            Bitmap signImage = new Bitmap(image, signPicture.Width, signPicture.Height);
            signImage.MakeTransparent();
            signPicture.Image = (Image)signImage.Clone();

            signPicture.Update();
            graphicDigitalSignaturePanel.Controls.Add(signPicture);


            if (model.isCustomSign)
            {
                graphicSignatureCustomizationOverviewLabel.Text = "Una tua firma grafica personalizzata è già stata caricata. Vuoi aggiornarla?";
                graphicSignatureCustomizationOverviewLabel.Update();

                generateCustomGraphicSignatureButton.Enabled = true;

            }
            else
            {
                graphicSignatureCustomizationOverviewLabel.Text = "Abbiamo creato per te una firma grafica, ma se preferisci puoi personalizzarla. " +
                                                "Questo passaggio non è indispensabile, ma ti consentirà di dare un tocco personale ai documenti firmati.";

                graphicSignatureCustomizationOverviewLabel.Update();

                generateCustomGraphicSignatureButton.Enabled = false;
            }

            mainTabControl.SelectedIndex = 15;
        }

        private void CustomizeGraphicSignatureLabel_MouseEnter(object sender, EventArgs e)
        {
            customizeGraphicSignatureLabel.Font = new Font(customizeGraphicSignatureLabel.Font, FontStyle.Underline);
        }

        private void CustomizeGraphicSignatureLabel_MouseLeave(object sender, EventArgs e)
        {
            customizeGraphicSignatureLabel.Font = new Font(customizeGraphicSignatureLabel.Font, FontStyle.Regular);
        }

        void PanelChooseDoc_dragEnter(object sender, DragEventArgs e)
        {
            Logger.Info("panelChooseDoc_dragEnter() - Inizia funzione");
            Logger.Debug("Panel_DragEnter");
            panelChooseDoc.BackColor = Color.LightGray;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        void PanelChooseDoc_dragLeave(object sender, EventArgs e)
        {
            Logger.Info("panelChooseDoc_dragLeave() - Inizia funzione");
            Logger.Debug("Panel_DragLeave");
            panelChooseDoc.BackColor = Color.Transparent;
        }

        private void GoToSelectSignOp(string file_name)
        {
            Logger.Info("goToSelectSignOp() - Inizia funzione");
            labelFileNamePathInOperationChooser.Text = file_name;

            if (signOp == OperationSelectedState.VERIFY)
            {
                pnVerificaOp_MouseClick(null, null);
            }
            else
            {
                SignOperationOptionPanel_MouseClick(null, null);
            }
        }

        void PanelChooseDoc_dragDrop(object sender, DragEventArgs e)
        {
            Logger.Info("PanelChooseDoc_dragDrop() - Inizia funzione");

            panelChooseDoc.BackColor = Color.Transparent;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            Logger.Debug($"File drop: {files[0]}");
            GoToSelectSignOp(files[0]);
        }

        private void SelectDocument_Click(object sender, EventArgs e)
        {
            Logger.Info("SelectDocument_Click() - Inizia funzione");
            OpenFileDialog openFile = new OpenFileDialog();

            //deleteTmpFiles();
            //page_index = 1;

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                string file_name = openFile.FileName;
                Logger.Debug($"Selected file: {file_name}");

                GoToSelectSignOp(file_name);
            }
        }

        private void DisplaySigningOperationTab()
        {
            Logger.Info("displaySigningOperationTab() - Inizia funzione");
            labelFileNamePathInSigningFormatChooser.Text = labelFileNamePathInOperationChooser.Text;

            signOp = OperationSelectedState.NO_OP;
            proceedWithSignatureButton.Enabled = false;

            PAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
            PAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;

            CAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
            CAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;

            CAdESP7MPictureBox.Image = CIEID.Properties.Resources.p7m_2x_gray;
            pbPades.Image = CIEID.Properties.Resources.pdf_2x_gray;

            enableGraphicSignatureCheckBox.Checked = false;

            if (labelFileNamePathInSigningFormatChooser.Text.EndsWith(".pdf") && !shouldSignWithoutCIEPairing)
            {
                enableGraphicSignatureCheckBox.Enabled = true;
                enableGraphicSignatureCheckBox.Visible = true;
            }
            else
            {
                enableGraphicSignatureCheckBox.Enabled = false;
                enableGraphicSignatureCheckBox.Visible = false;
            }

            mainTabControl.SelectedIndex = 12;
        }

        private void SignOperationOptionPanel_MouseClick(object sender, EventArgs e)
        {
            Logger.Info("signOperationOptionPanel_MouseClick() - Inizia funzione");
            DisplaySigningOperationTab();
        }

        private void SignOperationOptionPanel_MouseEnter(object sender, EventArgs e)
        {
            signOptionChooserLabel.ForeColor = Color.SeaGreen;
        }

        private void SignOperationOptionPanel_MouseLeave(object sender, EventArgs e)
        {
            signOptionChooserLabel.ForeColor = SystemColors.ControlDarkDark;
        }

        private void pnVerificaOp_MouseClick(object sender, EventArgs e)
        {
            Logger.Info("pnVerificaOp_MouseClick() - Inizia funzione");

            fileNamePathVerifyLabel.Text = labelFileNamePathInOperationChooser.Text;
            SignerInfo sInfo = new SignerInfo(fileNamePathVerifyLabel.Text, pnSignerInfo);
            int n_sott = sInfo.verify();

            if (n_sott == 0)
            {
                MessageBox.Show("Il file selezionato non contiene firme", "Verifica completata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                mainTabControl.SelectedIndex = 10;
            }

            else if ((UInt32)n_sott == INVALID_FILE_TYPE)
            {
                MessageBox.Show("Il file selezionato non è un file valido. È" +
                    " possibile verificare solo file con estensione .p7m o .pdf", "Errore nella verifica", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mainTabControl.SelectedIndex = 10;
            }

            else if (n_sott < 0)
            {
                MessageBox.Show("Errore nella verifica del file", "Errore nella verifica", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mainTabControl.SelectedIndex = 10;
            }
            else
            {
                signersCounterLabel.Text = string.Format("Numero di sottoscrittori: {0}", n_sott);
                signersCounterLabel.Update();

                verifyPanel.Visible = true;

                if (Path.GetExtension(fileNamePathVerifyLabel.Text) == ".p7m")
                {
                    extractP7MButton.Enabled = true;
                }
                else
                {
                    extractP7MButton.Enabled = false;
                }

                mainTabControl.SelectedIndex = 16;
            }
        }

        private void pnVerificaOp_MouseEnter(object sender, EventArgs e)
        {
            verifyOptionChooserLabel.ForeColor = Color.SeaGreen;
        }

        private void pnVerificaOp_MouseLeave(object sender, EventArgs e)
        {
            verifyOptionChooserLabel.ForeColor = SystemColors.ControlDarkDark;
        }

        private void CancelOperationButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CancelOperationButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 10;
        }

        private void EnableGraphicSignatureCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Logger.Info("EnableGraphicSignatureCheckBox_CheckedChanged() - Inizia funzione");
            if (labelFileNamePathInSigningFormatChooser.Text.EndsWith(".pdf"))
            {
                if (enableGraphicSignatureCheckBox.Checked == true)
                {
                    enableGraphicSignatureCheckBox.ForeColor = Color.SeaGreen;
                }
                else
                {
                    enableGraphicSignatureCheckBox.ForeColor = SystemColors.GrayText;
                }
            }
        }

        private void CancelSigningOperationButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CancelSigningOperationButton_Click() - Inizia funzione");

            signOp = OperationSelectedState.NO_OP;
            proceedWithSignatureButton.Enabled = false;

            PAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
            PAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;

            CAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
            CAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;

            CAdESP7MPictureBox.Image = CIEID.Properties.Resources.p7m_2x_gray;
            pbPades.Image = CIEID.Properties.Resources.pdf_2x_gray;

            enableGraphicSignatureCheckBox.Checked = false;

            mainTabControl.SelectedIndex = 11;
        }

        private void PanelChoosePades_MouseEnter(object sender, EventArgs e)
        {
            panelChoosePades.BorderStyle = BorderStyle.FixedSingle;
        }

        private void PanelChoosePades_MouseLeave(object sender, EventArgs e)
        {
            panelChoosePades.BorderStyle = BorderStyle.None;
        }

        private void PanelChooseCades_MouseEnter(object sender, EventArgs e)
        {
            panelChooseCades.BorderStyle = BorderStyle.FixedSingle;
        }

        private void PanelChooseCades_MouseLeave(object sender, EventArgs e)
        {
            panelChooseCades.BorderStyle = BorderStyle.None;
        }

        private void PanelChoosePades_MouseClick(object sender, EventArgs e)
        {
            Logger.Info("panelChoosePades_MouseClick() - Inizia funzione");
            if (labelFileNamePathInSigningFormatChooser.Text.EndsWith(".pdf"))
            {
                PAdESSignatureLabel.ForeColor = Color.Red;
                PAdESFormatDescriptionLabel.ForeColor = Color.Black;

                CAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
                CAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;

                CAdESP7MPictureBox.Image = CIEID.Properties.Resources.p7m_2x_gray;
                pbPades.Image = CIEID.Properties.Resources.pdf_2x;

                signOp = OperationSelectedState.PADES_SIGNATURE;
                proceedWithSignatureButton.Enabled = true;
            }
        }

        private void PanelChooseCades_MouseClick(object sender, EventArgs e)
        {
            Logger.Info("panelChooseCades_MouseClick() - Inizia funzione");
            CAdESSignatureLabel.ForeColor = Color.SeaGreen;
            CAdESFormatDescriptionLabel.ForeColor = Color.Black;

            PAdESSignatureLabel.ForeColor = SystemColors.ControlDarkDark;
            PAdESFormatDescriptionLabel.ForeColor = SystemColors.ControlDarkDark;
            enableGraphicSignatureCheckBox.Checked = false;

            CAdESP7MPictureBox.Image = CIEID.Properties.Resources.p7m_2x;
            pbPades.Image = CIEID.Properties.Resources.pdf_2x_gray;

            signOp = OperationSelectedState.CADES_SIGNATURE;
            proceedWithSignatureButton.Enabled = true;
        }

        private void ProceedWithSignatureButton_Click(object sender, EventArgs e)
        {
            Logger.Info("proceedWithSignatureButton_Click() - Inizia funzione");

            if ((enableGraphicSignatureCheckBox.Checked == true) && (signOp == OperationSelectedState.PADES_SIGNATURE))
            {
                var model = carouselControl.ActiveCieModel;
                string signImagePath = getSignImagePath(model.SerialNumber);

                if (!System.IO.File.Exists(signImagePath))
                {
                    TextInfo nameInfo = new CultureInfo("it-IT", false).TextInfo;
                    string name = model.Owner;
                    DrawText(nameInfo.ToTitleCase(name.ToLower()), Color.Black, signImagePath);
                }

                labelFileNamePathInDragSignatureBox.Text = labelFileNamePathInSigningFormatChooser.Text;

                upButton.Enabled = true;
                downButton.Enabled = true;

                if (pdfPreview != null)
                {
                    pdfPreview.pdfPreviewRemoveObjects();
                }

                EnterPreviewFullscreen();
                pdfPreview = new PdfPreview(panePreview, labelFileNamePathInDragSignatureBox.Text, signImagePath);
                if (pdfPreview.getPdfPages() <= 1)
                {
                    upButton.Enabled = false;
                    downButton.Enabled = false;
                }

                mainTabControl.SelectedIndex = 13;
            }
            else
            {
                labelFileNamePathSigningOperation.Text = labelFileNamePathInSigningFormatChooser.Text;
                mainTabControl.SelectedIndex = 14;
            }

            ChangeSignPINObjects();
        }

        private void UpButton_Click(object sender, EventArgs e)
        {
            Logger.Info("upButton_Click() - Inizia funzione");
            pdfPreview.pageUp();
        }

        private void DownButton_Click(object sender, EventArgs e)
        {
            Logger.Info("downButton_Click() - Inizia funzione");
            pdfPreview.pageDown();
        }

        // Avviso 'versione non ufficiale': altezza calcolata sul testo reale (DPI/font alti)
        // e label ancorata al bordo inferiore, cosi' il testo non viene mai tagliato.
        private Label pairingDisclaimerLabel;

        private void FitDisclaimerLabel()
        {
            FitDisclaimerLabel(unofficialDisclaimerLabel);
            FitDisclaimerLabel(pairingDisclaimerLabel);
        }

        private void FitDisclaimerLabel(Label label)
        {
            if (label == null || label.Parent == null)
                return;
            Control parent = label.Parent;
            int width = parent.ClientSize.Width - label.Left * 2;
            if (width <= 0)
                return;
            Size measured = TextRenderer.MeasureText(label.Text, label.Font,
                new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int height = measured.Height + 6;
            label.SetBounds(label.Left, parent.ClientSize.Height - height - 2, width, height);
        }

        private System.Windows.Forms.Timer placeCardBlinkTimer;
        private int placeCardBlinkTicks;

        // Fa lampeggiare "Appoggia la carta sul lettore" per qualche secondo (carta non trovata durante la firma)
        private void StartPlaceCardBlink()
        {
            StopPlaceCardBlink();
            placeCardBlinkTicks = 0;
            placeCIEOnReaderLabel.Show();
            placeCardBlinkTimer = new System.Windows.Forms.Timer();
            placeCardBlinkTimer.Interval = 400;
            placeCardBlinkTimer.Tick += (s, e) =>
            {
                placeCardBlinkTicks++;
                if (placeCardBlinkTicks >= 16)
                {
                    StopPlaceCardBlink();
                    return;
                }
                placeCIEOnReaderLabel.Visible = !placeCIEOnReaderLabel.Visible;
            };
            placeCardBlinkTimer.Start();
        }

        private void StopPlaceCardBlink()
        {
            if (placeCardBlinkTimer != null)
            {
                placeCardBlinkTimer.Stop();
                placeCardBlinkTimer.Dispose();
                placeCardBlinkTimer = null;
                placeCIEOnReaderLabel.Visible = true;
            }
        }

        private void ChangeSignPINObjects()
        {
            Logger.Info("ChangeSignPINObjects() - Inizia funzione");

            Logger.Debug("ShouldSignWithoutPairing: " + shouldSignWithoutCIEPairing);

            byte tagIndexStart = 9;
            byte tagIndexEnd = 17;
            byte spacingDiffPINLabelAndInput = 77;

            byte numDigitsPIN = (byte)((shouldSignWithoutCIEPairing) ? 8 : 4);
            typePINLabel.Text = "Inserire le " + ((numDigitsPIN == 4) ? "ultime 4" : "8") + " cifre del PIN";
            typePINLabel.TextAlign = ContentAlignment.MiddleLeft;
            typePINLabel.Show();

            for (int i = tagIndexStart; i < tagIndexEnd; i++)
            {
                TextBox txtField = (TextBox)FindControlByTag(Controls, "" + i);
                txtField.Hide();
                txtField.Text = "";

                txtField.Location = new Point(typePINLabel.Location.X + ((shouldSignWithoutCIEPairing) ? 8 : spacingDiffPINLabelAndInput) + 24 * (i - tagIndexStart), txtField.Location.Y);

                if(i < tagIndexStart + numDigitsPIN)
                    txtField.Show();
            }

            cancelSigningOperationPINPanelButton.Show();
            cancelSigningOperationPINPanelButton.Enabled = true;
            signButton.Show();
            signButton.Enabled = false;
            closeButton.Hide();

            placeCIEOnReaderLabel.Show();
            digitalSignatureCompletedPictureBox.Hide();
            documentSuccessfullySignedLabel.Hide();
            signProgressBar.Hide();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CloseButton_Click() - Inizia funzione");

            ChangeSignPINObjects();

            MenuDigitalSignatureButton_Click(null, null);
        }

        private void GoToSignPIN(string fileToSignPath)
        {
            Logger.Info("GoToSignPIN() - Inizia funzione");
            labelFileNamePathSigningOperation.Text = fileToSignPath;
            mainTabControl.SelectedIndex = 14;
        }

        private void ProceedSignPreviewButton_Click(object sender, EventArgs e)
        {
            Logger.Info("ProceedSignPreviewButton_Click() - Inizia funzione");
            GoToSignPIN(labelFileNamePathInDragSignatureBox.Text);
        }

        private void SignButton_Click(object sender, EventArgs e)
        {
            Logger.Info("SignButton_Click() - Inizia funzione");
            StopPlaceCardBlink();
            string PIN = "";
            byte numDigitsPIN = (byte)((shouldSignWithoutCIEPairing) ? 8 : 4);

            for (int i = 9; i < 9 + numDigitsPIN; i++)
            {
                TextBox txtField = (TextBox)FindControlByTag(Controls, "" + i);

                PIN += txtField.Text;
            }

            if (PIN.Length != numDigitsPIN || System.Text.RegularExpressions.Regex.IsMatch(PIN, "[^0-9]"))
            {
                MessageBox.Show("Inserire le " + ((numDigitsPIN == 4) ? "ultime 4": "8") + " cifre del PIN", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            string fileName = "";

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            if (signOp == OperationSelectedState.PADES_SIGNATURE)
            {
                fileName = "signed-" + Path.GetFileNameWithoutExtension(labelFileNamePathSigningOperation.Text);
                saveFileDialog1.Filter = "File (*.pdf) | *.pdf";
            }
            else
            {
                fileName = "signed-" + Path.GetFileName(labelFileNamePathSigningOperation.Text);
                fileName = fileName.Replace(".p7m", "");
                saveFileDialog1.Filter = "File (*.p7m) | *.p7m";
            }

            saveFileDialog1.FileName = fileName;
            string sfdname = saveFileDialog1.FileName;
            string pathToSaveFile = "";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                pathToSaveFile = Path.GetFullPath(saveFileDialog1.FileName);
                Logger.Debug(string.Format("Path save file: {0}", pathToSaveFile));
            }
            else
                return;

            for (int i = 9; i < 9 + numDigitsPIN; i++)
            {
                TextBox txtField = (TextBox)FindControlByTag(Controls, "" + i);
                txtField.Hide();
            }

            cancelSigningOperationPINPanelButton.Enabled = false;
            signButton.Enabled = false;

            signProgressBar.Value = 0;
            signProgressBar.Show();
            typePINLabel.TextAlign = ContentAlignment.MiddleCenter;
            typePINLabel.Text = "Firma in corso...";
            typePINLabel.Update();

            //PairCIE and Sign

            ((Control)sender).Enabled = false;
            ThreadStart processTaskThread = delegate
            {
                CieModel model = null;

                if (shouldSignWithoutCIEPairing)
                {
                    int tmpPairRes = CIEPairing(sender, PIN, true);

                    if (tmpPairRes == CARD_ALREADY_ENABLED)
                    {
                        MessageBox.Show("La CIE risulta essere già stata associata precedentemente, per cui l'operazione di firma è stata annullata. Ripetere il procedimento, selezionando la CIE dal selettore presente in 'Firma Elettronica'.", "CIE già associata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        shouldSignWithoutCIEPairing = false;
                        return;
                    }

                    else if (tmpPairRes != CKR_OK)
                    {
                        MessageBox.Show("Si è verificato un errore durante la lettura dei dati della CIE.", "Errore durante la firma", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        shouldSignWithoutCIEPairing = false;
                        return;
                    }

                    PIN = PIN.Substring(PIN.Length - 4);
                }

                else
                    model = carouselControl.ActiveCieModel;

                // Marca temporale: letta qui (thread UI) perche' la firma gira su un altro thread
                string tsaUrl = null;
                string savedTsaUser = null;
                string savedTsaPass = null;
                if (enableTimestampCheckBox.Checked)
                {
                    string customUrl = Properties.Settings.Default.timestampCustomUrl;
                    if (!string.IsNullOrEmpty(customUrl))
                    {
                        tsaUrl = customUrl;
                        TryGetSavedTsaCredentials(out savedTsaUser, out savedTsaPass);
                    }
                    else
                    {
                        tsaUrl = Properties.Settings.Default.timestampUrl;
                    }
                }

                // Parametri letti una volta sola (thread UI) e riusati negli eventuali nuovi tentativi
                string inputPath = labelFileNamePathSigningOperation.Text;
                bool withGraphic = enableGraphicSignatureCheckBox.Checked && (signOp == OperationSelectedState.PADES_SIGNATURE);
                Dictionary<string, float> graphicInfo = withGraphic ? pdfPreview.getSignImageInfos() : null;
                string graphicImagePath = withGraphic ? pdfPreview.getSignImagePath() : null;

                // Credenziali della TSA: solo se scelte nel dialogo di fallback (server personalizzato)
                string tsaUser = savedTsaUser;
                string tsaPass = savedTsaPass;

                Func<string, int> doSign = delegate (string url)
                {
                    string pan = (shouldSignWithoutCIEPairing) ? PANForOneShotSigning : model.Pan;
                    if (withGraphic)
                    {
                        Logger.Debug("Pades con grafica");
                        return firmaConCIE(inputPath, "pdf", PIN, pan, (int)graphicInfo["pageNumber"], graphicInfo["x"], graphicInfo["y"], graphicInfo["w"], graphicInfo["h"],
                                          graphicImagePath, pathToSaveFile, new ProgressCallback(SignProgress), new SignCompletedCallback(SignCompleted), url, tsaUser, tsaPass);
                    }
                    else if (signOp == OperationSelectedState.PADES_SIGNATURE)
                    {
                        Logger.Debug("Pades senza grafica");
                        return firmaConCIE(inputPath, "pdf", PIN, pan, 0, 0.0f, 0.0f, 0.0f, 0.0f, null, pathToSaveFile, new ProgressCallback(SignProgress), new SignCompletedCallback(SignCompleted), url, tsaUser, tsaPass);
                    }
                    else if (signOp == OperationSelectedState.CADES_SIGNATURE)
                    {
                        Logger.Debug("Cades");
                        return firmaConCIE(inputPath, "p7m", PIN, pan, 0, 0.0f, 0.0f, 0.0f, 0.0f, null, pathToSaveFile, new ProgressCallback(SignProgress), new SignCompletedCallback(SignCompleted), url, tsaUser, tsaPass);
                    }
                    return 0;
                };

                int ret = doSign(tsaUrl);
                Logger.Info("Firma: firmaConCIE ha restituito 0x" + ret.ToString("X"));

                // Marca temporale non ottenuta: si chiede all'utente come procedere (al massimo 3 tentativi)
                for (int attempt = 0; ret == CARD_TSA_ERROR && attempt < 3; attempt++)
                {
                    string failedUrl = tsaUrl;
                    bool proceed = false;
                    string chosen = null;
                    string chosenUser = null;
                    string chosenPass = null;
                    this.Invoke((MethodInvoker)delegate
                    {
                        proceed = AskTimestampFallback(failedUrl, out chosen, out chosenUser, out chosenPass);
                    });

                    if (!proceed)
                        break;

                    tsaUrl = chosen;
                    tsaUser = chosenUser;
                    tsaPass = chosenPass;
                    Logger.Info("Marca temporale non ottenuta, nuovo tentativo con: " + (tsaUrl ?? "(nessuna marca)"));
                    ret = doSign(tsaUrl);
                }

                // Le credenziali non servono piu': non restano in memoria piu del necessario
                tsaUser = null;
                tsaPass = null;

                this.Invoke((MethodInvoker)delegate
                {
                    ((Control)sender).Enabled = true;
                    switch (ret)
                    {
                        case CKR_TOKEN_NOT_RECOGNIZED:
                            Logger.Info("Firma: CIE non presente sul lettore");
                            ChangeSignPINObjects();
                            StartPlaceCardBlink();
                            break;

                        case CKR_TOKEN_NOT_PRESENT:
                            Logger.Info("Firma: CIE non presente sul lettore");
                            ChangeSignPINObjects();
                            StartPlaceCardBlink();
                            break;

                        case CKR_PIN_INCORRECT:
                            MessageBox.Show(String.Format("Il PIN digitato è errato."), "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            ChangeSignPINObjects();
                            break;

                        case CKR_PIN_LEN_RANGE:
                            MessageBox.Show("Il PIN deve essere composto da cifre numeriche (le ultime 4 cifre).", "PIN non corretto", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            ChangeSignPINObjects();
                            break;

                        case CKR_PIN_LOCKED:
                            MessageBox.Show("Munisciti del codice PUK e utilizza la funzione di sblocco carta per abilitarla", "Carta bloccata", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            ChangeSignPINObjects();
                            break;

                        case CARD_PAN_MISMATCH:
                            MessageBox.Show("CIE selezionata diversa da quella presente sul lettore", "CIE non corrispondente", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            ChangeSignPINObjects();
                            break;

                        case CARD_TSA_ERROR:
                            MessageBox.Show("La marca temporale non è stata ottenuta e il documento non è stato firmato.", "Marca temporale non disponibile", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            ChangeSignPINObjects();
                            break;
                        default:
                            if (ret != 0)
                            {
                                Logger.Error("Firma: codice di ritorno non gestito 0x" + ret.ToString("X"));
                                ChangeSignPINObjects();
                                StartPlaceCardBlink();
                            }
                            break;
                    }

                    if (shouldSignWithoutCIEPairing)
                        RemoveCIEFromCollection(PANForOneShotSigning);

                    PANForOneShotSigning = String.Empty;
                    shouldSignWithoutCIEPairing = false;
                });
            };

            new Thread(processTaskThread).Start();
        }

        // Dialogo mostrato quando il server di marca temporale non risponde.
        // Restituisce false se l'utente annulla; chosenUrl = null significa firma senza marca (solo ora del PC).
        private bool AskTimestampFallback(string failedUrl, out string chosenUrl, out string chosenUser, out string chosenPass)
        {
            const string digicertUrl = "http://timestamp.digicert.com";
            chosenUrl = null;
            chosenUser = null;
            chosenPass = null;

            using (Form dlg = new Form())
            {
                dlg.Text = "Marca temporale non disponibile";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(480, 340);

                Label info = new Label();
                info.AutoSize = false;
                info.SetBounds(16, 12, 448, 52);
                info.Text = "La marca temporale non è stata ottenuta (server non raggiungibile, certificato non valido, credenziali rifiutate o URL non valido)" +
                            (string.IsNullOrEmpty(failedUrl) ? "." : " (" + failedUrl + ").") +
                            "\nCome vuoi procedere?";
                dlg.Controls.Add(info);

                RadioButton rbNone = new RadioButton();
                rbNone.Text = "Firma senza marca temporale";
                rbNone.SetBounds(24, 72, 430, 24);
                rbNone.Checked = true;
                dlg.Controls.Add(rbNone);

                RadioButton rbDigicert = new RadioButton();
                rbDigicert.Text = "Usa DigiCert (" + digicertUrl + ")";
                rbDigicert.SetBounds(24, 100, 430, 24);
                dlg.Controls.Add(rbDigicert);

                RadioButton rbLocal = new RadioButton();
                rbLocal.Text = "Solo ora del PC (nessuna marca, orario locale)";
                rbLocal.SetBounds(24, 128, 430, 24);
                dlg.Controls.Add(rbLocal);

                RadioButton rbCustom = new RadioButton();
                rbCustom.Text = "Server personalizzato (URL):";
                rbCustom.SetBounds(24, 156, 430, 24);
                dlg.Controls.Add(rbCustom);

                TextBox txtUrl = new TextBox();
                txtUrl.SetBounds(44, 184, 410, 24);
                txtUrl.Enabled = false;
                dlg.Controls.Add(txtUrl);

                Label lblUser = new Label();
                lblUser.Text = "Utente (facoltativo):";
                lblUser.SetBounds(44, 216, 130, 20);
                dlg.Controls.Add(lblUser);

                TextBox txtTsaUser = new TextBox();
                txtTsaUser.SetBounds(180, 213, 274, 24);
                txtTsaUser.Enabled = false;
                dlg.Controls.Add(txtTsaUser);

                Label lblPass = new Label();
                lblPass.Text = "Password:";
                lblPass.SetBounds(44, 246, 130, 20);
                dlg.Controls.Add(lblPass);

                TextBox txtTsaPass = new TextBox();
                txtTsaPass.SetBounds(180, 243, 274, 24);
                txtTsaPass.UseSystemPasswordChar = true;
                txtTsaPass.Enabled = false;
                dlg.Controls.Add(txtTsaPass);

                rbCustom.CheckedChanged += delegate
                {
                    txtUrl.Enabled = rbCustom.Checked;
                    txtTsaUser.Enabled = rbCustom.Checked;
                    txtTsaPass.Enabled = rbCustom.Checked;
                    if (rbCustom.Checked) txtUrl.Focus();
                };

                Button ok = new Button();
                ok.Text = "Procedi";
                ok.SetBounds(298, 290, 80, 30);
                dlg.Controls.Add(ok);

                Button cancel = new Button();
                cancel.Text = "Annulla";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(384, 290, 80, 30);
                dlg.Controls.Add(cancel);

                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                string result = null;
                string resultUser = null;
                string resultPass = null;
                ok.Click += delegate
                {
                    if (rbDigicert.Checked)
                    {
                        result = digicertUrl;
                    }
                    else if (rbCustom.Checked)
                    {
                        string u = txtUrl.Text.Trim();
                        Uri parsed;
                        if (u.Length == 0 || u.Length >= 256 ||
                            !Uri.TryCreate(u, UriKind.Absolute, out parsed) ||
                            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                        {
                            MessageBox.Show("Inserire un URL valido che inizi con http:// o https:// (massimo 255 caratteri).", "URL non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }
                        if (txtTsaUser.Text.Length >= 256 || txtTsaPass.Text.Length >= 256)
                        {
                            MessageBox.Show("Utente e password possono avere al massimo 255 caratteri.", "Credenziali non valide", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }
                        result = u;
                        if (txtTsaUser.Text.Length > 0)
                        {
                            resultUser = txtTsaUser.Text;
                            resultPass = txtTsaPass.Text;
                        }
                    }
                    else
                    {
                        result = null;
                    }
                    dlg.DialogResult = DialogResult.OK;
                };

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return false;

                chosenUrl = result;
                chosenUser = resultUser;
                chosenPass = resultPass;
                return true;
            }
        }

        private void CancelCustomizationButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CancelCustomizationButton_Click() - Inizia funzione");

            var model = carouselControl.ActiveCieModel;

            if (model.isCustomSign)
            {
                customizeGraphicSignatureLabel.Text = "Aggiorna";
                labelGraphicSignatureDescriptionInfoBox.Text = "Firma personalizzata correttamente";
            }

            mainTabControl.SelectedIndex = 10;
        }

        private void SelectFileForGraphicSignatureCustomizationButton_Click(object sender, EventArgs e)
        {
            Logger.Info("SelectFileForGraphicSignatureCustomizationButton_Click() - Inizia funzione");
            OpenFileDialog openFile = new OpenFileDialog();

            openFile.Filter = "File (*.png) | *.png";

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                string file_name = openFile.FileName;
                Logger.Debug(string.Format("PNG Selected file: {0}", file_name));

                var model = carouselControl.ActiveCieModel;

                File.Copy(file_name, getSignImagePath(model.SerialNumber), true);
                CieColl.MyDictionary[model.Pan].isCustomSign = true;
                Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
                Properties.Settings.Default.Save();

                Image image;
                using (Stream stream = File.OpenRead(getSignImagePath(model.SerialNumber)))
                {
                    image = Image.FromStream(stream);
                }

                PictureBox signPicture = (PictureBox)graphicDigitalSignaturePanel.Controls[0];
                Bitmap signImage = new Bitmap(image, signPicture.Width, signPicture.Height);
                signImage.MakeTransparent();
                signPicture.Image = signImage;
                signPicture.Update();

                graphicSignatureCustomizationOverviewLabel.Text = "Una tua firma grafica personalizzata è già stata caricata. Vuoi aggiornarla?";
                graphicSignatureCustomizationOverviewLabel.Update();

                generateCustomGraphicSignatureButton.Enabled = true;
            }
        }

        private void CloseVerifyButton_Click(object sender, EventArgs e)
        {
            Logger.Info("CloseVerifyButton_Click() - Inizia funzione");
            mainTabControl.SelectedIndex = 10;
        }

        private void DisplayFileSelectionTab()
        {
            Logger.Info("DisplayFileSelectionTab() - Inizia funzione");

            if (signOp == OperationSelectedState.VERIFY)
            {
                pictureBox15.Visible = false;
                customizeGraphicSignatureLabel.Visible = false;
                labelGraphicSignatureDescriptionInfoBox.Visible = false;
                labelDragAndDropDocumentInformation.Text = "Trascina i tuoi documenti qui per verificare una firma elettronica esistente";
            }

            else
            {
                labelDragAndDropDocumentInformation.Text = "Trascina i tuoi documenti qui dentro per firmarli";

                if (shouldSignWithoutCIEPairing)
                {
                    pictureBox15.Visible = false;
                    customizeGraphicSignatureLabel.Visible = false;
                    labelGraphicSignatureDescriptionInfoBox.Visible = false;
                }

                else
                {
                    var model = carouselControl.ActiveCieModel;

                    pictureBox15.Visible = true;

                    customizeGraphicSignatureLabel.Visible = true;
                    labelGraphicSignatureDescriptionInfoBox.Visible = true;

                    if (model.isCustomSign)
                    {
                        customizeGraphicSignatureLabel.Text = "Aggiorna";
                        labelGraphicSignatureDescriptionInfoBox.Text = "Firma personalizzata correttamente";
                    }

                    else
                    {
                        customizeGraphicSignatureLabel.Text = "Personalizza";
                        labelGraphicSignatureDescriptionInfoBox.Text = "Abbiamo creato per te una firma grafica, ma se preferisci puoi personalizzarla. " +
                                       "Questo passaggio non è indispensabile, ma ti consentirà di dare un tocco personale ai documenti firmati.";
                    }
                }
            }

            mainTabControl.SelectedIndex = 10;
        }

        private void SelectSigningCIEButton_Click(object sender, EventArgs e)
        {
            Logger.Info("SelectSigningCIEButton_Click() - Inizia funzione");
            Logger.Debug("Disabling flag");
            shouldSignWithoutCIEPairing = false;
            DisplayFileSelectionTab();
        }

        private void GenerateCustomGraphicSignatureButton_Click(object sender, EventArgs e)
        {
            Logger.Info("GenerateCustomGraphicSignatureButton_Click() - Inizia funzione");

            var model = carouselControl.ActiveCieModel;

            string defaultSignImagePath = getSignImagePath(model.SerialNumber);

            Logger.Debug(string.Format("Immagine firma salvata in: {0}", defaultSignImagePath));

            TextInfo nameInfo = new CultureInfo("it-IT", false).TextInfo;
            DrawText(nameInfo.ToTitleCase(model.Owner.ToLower()), Color.Black, defaultSignImagePath);

            Image image;
            using (Stream stream = File.OpenRead(getSignImagePath(model.SerialNumber)))
            {
                image = Image.FromStream(stream);
            }

            PictureBox signPicture = (PictureBox)graphicDigitalSignaturePanel.Controls[0];
            Bitmap signImage = new Bitmap(image, signPicture.Width, signPicture.Height);
            signImage.MakeTransparent();
            signPicture.Image = signImage;
            signPicture.Update();

            CieColl.MyDictionary[model.Pan].isCustomSign = false;
            Properties.Settings.Default.cieList = JsonConvert.SerializeObject(CieColl.MyDictionary);
            Properties.Settings.Default.Save();

            labelGraphicSignatureDescriptionInfoBox.Text = "Abbiamo creato per te una firma grafica, ma se preferisci puoi personalizzarla. " +
                           "Questo passaggio non è indispensabile, ma ti consentirà di dare un tocco personale ai documenti firmati.";

            customizeGraphicSignatureLabel.Text = "Personalizza";

            graphicSignatureCustomizationOverviewLabel.Text = labelGraphicSignatureDescriptionInfoBox.Text;
            graphicSignatureCustomizationOverviewLabel.Update();

            generateCustomGraphicSignatureButton.Enabled = false;
        }

        private void menuSettingsButton_Click(object sender, EventArgs e)
        {
            Logger.Info("menuSettingsButton_Click() - Inizia funzione");

            menuHomeButton.BackColor = Color.Transparent;
            menuChangePINButton.BackColor = Color.Transparent;
            menuUnlockPINButton.BackColor = Color.Transparent;
            menuTutorialButton.BackColor = Color.Transparent;
            menuInformationButton.BackColor = Color.Transparent;
            menuHelpButton.BackColor = Color.Transparent;
            menuDigitalSignatureButton.BackColor = Color.Transparent;
            menuVerifyButton.BackColor = Color.Transparent;
            menuSettingsButton.BackColor = Color.LightGray;

            // If btnEditSettings is enabled then the config pane is *not* in edit mode
            if (editSettingsButton.Enabled)
            {
                Logger.Debug("menuSettingsButton_Click() - Configurazione non in modifica, carico valori dalle preferenze");
                if (Properties.Settings.Default.proxyURL != "")
                {
                    Logger.Debug("menuSettingsButton_Click() - Impostazione proxy presente");
                    if (Properties.Settings.Default.credentials == "")
                    {
                        Logger.Debug("menuSettingsButton_Click() - Impostazioni credenziali presenti");
                        txtUsername.Text = "";
                        txtPassword.Text = "";
                    }
                    else
                    {
                        Logger.Debug("menuSettingsButton_Click() - Impostazioni credenziali non presenti");
                        string encryptedCredentials = Properties.Settings.Default.credentials;
                        ProxyInfoManager proxyInfoManager = new ProxyInfoManager();

                        string credentials = proxyInfoManager.getDecryptedCredentials(encryptedCredentials);

                        if (credentials.Substring(0, 5) == "cred=")
                        {
                            string[] infos = credentials.Substring(5).Split(':');
                            txtUsername.Text = infos[0];
                            txtPassword.Text = infos[1];
                        }

                    }
                    txtUrl.Text = Properties.Settings.Default.proxyURL;
                    txtPort.Text = Properties.Settings.Default.proxyPort.ToString();
                }

                Logger.Debug($"menuSettingsButton_Click() - radio button app: {Program.LogLevelApp}  lib: {Program.LogLevelLib}");
                rbLoggingAppNone.Checked = (Program.LogLevelApp == LogLevel.NONE);
                rbLoggingAppError.Checked = (Program.LogLevelApp == LogLevel.ERROR);
                rbLoggingAppInfo.Checked = (Program.LogLevelApp == LogLevel.INFO);
                rbLoggingAppDebug.Checked = (Program.LogLevelApp == LogLevel.DEBUG);

                rbLoggingLibNone.Checked = (Program.LogLevelLib == LogLevel.NONE);
                rbLoggingLibError.Checked = (Program.LogLevelLib == LogLevel.ERROR);
                rbLoggingLibInfo.Checked = (Program.LogLevelLib == LogLevel.INFO);
                rbLoggingLibDebug.Checked = (Program.LogLevelLib == LogLevel.DEBUG);

                showIntroductionScreens.Checked = Properties.Settings.Default.firstTime;

                LoadTsaSettings();
            }

            mainTabControl.SelectedIndex = 17;
        }

        private void LoadTsaSettings()
        {
            txtTsaUrl.Text = Properties.Settings.Default.timestampCustomUrl;
            txtTsaUsername.Text = "";
            txtTsaPassword.Text = "";
            string tsaUser, tsaPass;
            if (TryGetSavedTsaCredentials(out tsaUser, out tsaPass))
            {
                txtTsaUsername.Text = tsaUser;
                txtTsaPassword.Text = tsaPass;
            }
        }

        // Legge le credenziali TSA salvate (cifrate come quelle del proxy). Formato: cred=user:pass
        private bool TryGetSavedTsaCredentials(out string user, out string pass)
        {
            user = null;
            pass = null;
            string enc = Properties.Settings.Default.timestampCredentials;
            if (string.IsNullOrEmpty(enc))
                return false;
            try
            {
                string cred = new ProxyInfoManager().getDecryptedCredentials(enc);
                if (cred != null && cred.StartsWith("cred="))
                {
                    string[] infos = cred.Substring(5).Split(new char[] { ':' }, 2);
                    if (infos.Length == 2 && infos[0].Length > 0)
                    {
                        user = infos[0];
                        pass = infos[1];
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Credenziali TSA salvate non leggibili: " + ex.Message);
            }
            return false;
        }

        private void ShowPasswordCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Logger.Info("ShowPasswordCheckBox_CheckedChanged() - Inizia funzione");
            if (showPasswordCheckBox.Checked == true)
            {
                txtPassword.UseSystemPasswordChar = false;
            }
            else
            {
                txtPassword.UseSystemPasswordChar = true;
            }
        }

        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            Logger.Info("SaveSettingsButton_Click() - Inizia funzione");

            if ((String.IsNullOrEmpty(txtUsername.Text) && !String.IsNullOrEmpty(txtPassword.Text)) || (!String.IsNullOrEmpty(txtUsername.Text) && String.IsNullOrEmpty(txtPassword.Text)))
            {
                Logger.Debug("SaveSettingsButton_Click() - Campo username o password mancante");
                MessageBox.Show("Campo username o password mancante", "Credenziali proxy mancanti", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if ((String.IsNullOrEmpty(txtPort.Text) && !String.IsNullOrEmpty(txtUrl.Text)) || (!String.IsNullOrEmpty(txtPort.Text) && String.IsNullOrEmpty(txtUrl.Text)))
            {
                Logger.Debug("SaveSettingsButton_Click() - Indirizzo o porta del proxy mancante");
                MessageBox.Show("Indirizzo o porta del proxy mancante", "Informazioni proxy mancanti", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if ((String.IsNullOrEmpty(txtUsername.Text)))
            {
                Logger.Debug("SaveSettingsButton_Click() - Elimina credenziali proxy");
                Properties.Settings.Default.credentials = "";
            }
            else
            {
                Logger.Debug($"SaveSettingsButton_Click() - Conserva credenziali per username: '{txtUsername.Text}'");
                string credentials = String.Format("cred={0}:{1}", txtUsername.Text, txtPassword.Text);
                //Console.WriteLine("Credentials: {0}", credentials);

                ProxyInfoManager proxyInfoManager = new ProxyInfoManager();
                string encryptedCredentials = proxyInfoManager.getEncryptedCredentials(credentials);
                //Console.WriteLine("Credentials: {0}", credentials);
                Properties.Settings.Default.credentials = encryptedCredentials;
            }
            Logger.Debug($"SaveSettingsButton_Click() - Conserva credenziali per proxyURL: '{txtUrl.Text}'");
            Properties.Settings.Default.proxyURL = txtUrl.Text;
            if (String.IsNullOrEmpty(txtPort.Text))
            {
                Logger.Debug($"SaveSettingsButton_Click() - proxyPort: {0}");
                Properties.Settings.Default.proxyPort = 0;
            }
            else
            {
                Int32 value = Int32.Parse(txtPort.Text);
                Logger.Debug($"SaveSettingsButton_Click() - proxyPort: {value}");
                Properties.Settings.Default.proxyPort = value;
            }

            // Marca temporale personalizzata
            string tsaCustomUrl = (txtTsaUrl.Text ?? "").Trim();
            if (tsaCustomUrl.Length > 0)
            {
                Uri tsaUri;
                if (tsaCustomUrl.Length > 255 || !Uri.TryCreate(tsaCustomUrl, UriKind.Absolute, out tsaUri)
                    || (tsaUri.Scheme != Uri.UriSchemeHttp && tsaUri.Scheme != Uri.UriSchemeHttps))
                {
                    MessageBox.Show("L'indirizzo della TSA deve iniziare con http:// o https:// (max 255 caratteri)", "Indirizzo TSA non valido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }
            if (string.IsNullOrEmpty(txtTsaUsername.Text) != string.IsNullOrEmpty(txtTsaPassword.Text))
            {
                MessageBox.Show("Campo username o password della TSA mancante", "Credenziali TSA mancanti", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (txtTsaUsername.Text.Contains(":"))
            {
                MessageBox.Show("Lo username della TSA non può contenere il carattere ':'", "Credenziali TSA non valide", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            Properties.Settings.Default.timestampCustomUrl = tsaCustomUrl;
            if (tsaCustomUrl.Length == 0 || string.IsNullOrEmpty(txtTsaUsername.Text))
            {
                Properties.Settings.Default.timestampCredentials = "";
            }
            else
            {
                Properties.Settings.Default.timestampCredentials = new ProxyInfoManager().getEncryptedCredentials(
                    string.Format("cred={0}:{1}", txtTsaUsername.Text, txtTsaPassword.Text));
            }

            Logger.Debug("SaveSettingsButton_Click() - Registra configurazione di log");
            LogLevel logLevelApp = Logger.DefaultLogLevel;
            LogLevel logLevelLib = Logger.DefaultLogLevel;

            if (rbLoggingAppNone.Checked == true)
            {
                logLevelApp = LogLevel.NONE;
            }
            else if (rbLoggingAppError.Checked == true)
            {
                logLevelApp = LogLevel.ERROR;
            }
            else if (rbLoggingAppInfo.Checked == true)
            {
                logLevelApp = LogLevel.INFO;
            }
            else if (rbLoggingAppDebug.Checked == true)
            {
                logLevelApp = LogLevel.DEBUG;
            }

            if (rbLoggingLibNone.Checked == true)
            {
                logLevelLib = LogLevel.NONE;
            }
            else if (rbLoggingLibError.Checked == true)
            {
                logLevelLib = LogLevel.ERROR;
            }
            else if (rbLoggingLibInfo.Checked == true)
            {
                logLevelLib = LogLevel.INFO;
            }
            else if (rbLoggingLibDebug.Checked == true)
            {
                logLevelLib = LogLevel.DEBUG;
            }

            Program.SetLogConfig(logLevelApp, logLevelLib);

            Logger.Debug("SaveSettingsButton_Click() - Esci dalla modalità di modifica");
            txtUrl.Enabled = false;
            txtUsername.Enabled = false;
            txtPassword.Enabled = false;
            txtPort.Enabled = false;
            showPasswordCheckBox.Enabled = false;
            showPasswordCheckBox.Checked = false;

            txtTsaUrl.Enabled = false;
            txtTsaUsername.Enabled = false;
            txtTsaPassword.Enabled = false;
            showTsaPasswordCheckBox.Enabled = false;
            showTsaPasswordCheckBox.Checked = false;

            configLibraryLoggingGroupBox.Enabled = false;
            configApplicationLoggingGroupBox.Enabled = false;

            showIntroductionScreens.Enabled = false;

            saveSettingsButton.Enabled = false;
            editSettingsButton.Enabled = true;

            Logger.Debug("SaveSettingsButton_Click() - Salva configurazione");
            Program.SaveLogConfigToFile();
            Properties.Settings.Default.Save();
            Logger.Debug("SaveSettingsButton_Click() - Configurazione salvata");
        }

        private void EditSettingsButton_Click(object sender, EventArgs e)
        {
            Logger.Info("editSettingsButton_Click() - Inizia funzione");

            txtUrl.Enabled = true;
            txtUsername.Enabled = true;
            txtPassword.Enabled = true;
            txtPort.Enabled = true;
            showPasswordCheckBox.Enabled = true;
            showPasswordCheckBox.Checked = false;

            txtTsaUrl.Enabled = true;
            txtTsaUsername.Enabled = true;
            txtTsaPassword.Enabled = true;
            showTsaPasswordCheckBox.Enabled = true;
            showTsaPasswordCheckBox.Checked = false;

            configLibraryLoggingGroupBox.Enabled = true;
            configApplicationLoggingGroupBox.Enabled = true;

            showIntroductionScreens.Enabled = true;

            saveSettingsButton.Enabled = true;
            editSettingsButton.Enabled = false;
        }

        private void txtPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void ExtractP7MButton_Click(object sender, EventArgs e)
        {
            Logger.Info("ExtractP7MButton_Click() - Inizia funzione");
            string fileName = Path.GetFileNameWithoutExtension(fileNamePathVerifyLabel.Text);

            if (fileName.StartsWith("signed-"))
                fileName = fileName.Substring("signed-".Length);
            fileName = fileName.Replace("-signed", "");

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.FileName = fileName;
            string fileExt = Path.GetExtension(fileName);
            saveFileDialog1.Filter = string.Format("{0}{1}{2}{3}", "File(*", fileExt, ") | *", fileExt);

            string pathToSaveFile = "";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                pathToSaveFile = Path.GetFullPath(saveFileDialog1.FileName);

                Logger.Debug(string.Format("File extension: {0}", fileExt));
                long res = estraiP7m(fileNamePathVerifyLabel.Text, pathToSaveFile);
                Logger.Debug(string.Format("Res: {0}", (UInt32)res));

                if (res == 0)
                {
                    MessageBox.Show("File estratto correttamente", "Estrazione file completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Impossibile estrarre il file", "Estrazione file completata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                return;
            }

        }

        private void SignWithCIEWithoutPairingButton_Click(object sender, EventArgs e)
        {
            //Enable 8 PIN digits
            shouldSignWithoutCIEPairing = true;
            ChangeSignPINObjects();
            DisplayFileSelectionTab();
        }

        private void FocusPairingFirstPin()
        {
            if (!IsHandleCreated || mainTabControl.SelectedTab != CIEPairingTabPage)
                return;
            // Se l'utente sta gia' scrivendo in una delle caselle non si sposta il focus
            if (ActiveControl is TextBox active && active.Parent == CIEPairingTabPage && active != PINDigit_TextBox1)
                return;
            BeginInvoke((MethodInvoker)delegate
            {
                if (mainTabControl.SelectedTab == CIEPairingTabPage)
                    PINDigit_TextBox1.Focus();
            });
        }

        private void mainTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            Logger.Debug("mainTabControl_SelectedIndex: " + mainTabControl.SelectedIndex.ToString());

            if (mainTabControl.SelectedIndex == 0 || mainTabControl.SelectedIndex == 1)
                shouldSignWithoutCIEPairing = false;

            // Uscendo dalla scelta della posizione della firma grafica si torna alle dimensioni normali
            if (previewFullscreen && mainTabControl.SelectedIndex != 13)
                ExitPreviewFullscreen();

            // Focus automatico sulla prima casella del PIN
            Control firstPin = null;
            if (mainTabControl.SelectedTab == CIEPairingTabPage)
                firstPin = PINDigit_TextBox1;
            else if (mainTabControl.SelectedIndex == 14)
                firstPin = signPINDigit_TextBox1;
            if (firstPin != null)
                BeginInvoke((MethodInvoker)delegate { firstPin.Focus(); });
        }

        private bool previewFullscreen = false;
        private Size normalClientSize;

        // Porta l'applicazione a schermo pieno e dimensiona l'anteprima del PDF sullo spazio disponibile
        private void EnterPreviewFullscreen()
        {
            if (!previewFullscreen)
            {
                normalClientSize = this.ClientSize;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MaximizeBox = true;
                this.WindowState = FormWindowState.Maximized;
                previewFullscreen = true;
            }

            if (!previewLayoutHooked)
            {
                graphicDigitalSignatureTab.Resize += delegate { LayoutPreviewTab(); };
                previewLayoutHooked = true;
            }

            LayoutPreviewTab();
        }

        private bool previewLayoutHooked = false;

        // Disposizione della schermata di scelta della posizione della firma grafica,
        // calcolata sulle dimensioni correnti della scheda
        private void LayoutPreviewTab()
        {
            Size t = graphicDigitalSignatureTab.ClientSize;
            if (t.Width < 200 || t.Height < 200)
                return;

            labelDigitalSignaturePlaceGraphicSignatureHeaderText.Location = new Point((t.Width - labelDigitalSignaturePlaceGraphicSignatureHeaderText.Width) / 2, 10);
            labelDigitalSignatureDragSignatureBox.Location = new Point((t.Width - labelDigitalSignatureDragSignatureBox.Width) / 2, 52);

            proceedSignPreviewButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            proceedSignPreviewButton.Location = new Point((t.Width - proceedSignPreviewButton.Width) / 2, t.Height - proceedSignPreviewButton.Height - 12);

            panelPreviewGraphicSignature.Location = new Point(15, 82);
            panelPreviewGraphicSignature.Size = new Size(t.Width - 30, proceedSignPreviewButton.Top - 82 - 8);

            int pw = panelPreviewGraphicSignature.Width;
            int ph = panelPreviewGraphicSignature.Height;

            pictureBox22.Location = new Point(4, 2);
            pictureBox22.Size = new Size(36, 43);
            labelFileNamePathInDragSignatureBox.Location = new Point(48, 12);
            labelFileNamePathInDragSignatureBox.Size = new Size(Math.Max(50, pw - 56), 24);

            int top = 50;
            panePreview.Location = new Point(4, top);
            panePreview.Size = new Size(Math.Max(100, pw - 8 - 48), Math.Max(100, ph - top - 2));

            upButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            downButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            upButton.Location = new Point(panePreview.Right + 6, top + panePreview.Height / 2 - 45);
            downButton.Location = new Point(panePreview.Right + 6, top + panePreview.Height / 2 + 10);
        }

        private void ExitPreviewFullscreen()
        {
            previewFullscreen = false;
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.ClientSize = normalClientSize;
        }

        private void collectLogBtn_Click(object sender, EventArgs e)
        {
            bool logFound = false;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = "CIEID_" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            saveFileDialog.Filter = "Archivio log CIE ID | *.zip";

            if(saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (FileStream fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                        {
                            String[] files = Directory.GetFiles(@"C:\ProgramData\CIEPKI-NG");

                            foreach (string fileName in files)
                            {
                                if (Path.GetExtension(fileName).ToLower() == @".log")
                                {
                                    logFound = true;
                                    ZipArchiveEntry zipArchiveEntry = archive.CreateEntry(Path.GetFileName(fileName), CompressionLevel.Optimal);
                                    using (Stream zipStream = zipArchiveEntry.Open())
                                    {
                                        Byte[] logData = File.ReadAllBytes(fileName);
                                        zipStream.Write(logData, 0, logData.Length);
                                    }
                                }
                            }
                        }
                    }

                    if (logFound)
                    {
                        MessageBox.Show("La raccolta dei log di diagnostica è avvenuta con successo. Puoi adesso condividere con gli sviluppatori " +
                                        "l'archivio generato per un'analisi della problematica riscontrata.", "Raccolta completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Logger.Info("[INFO] CollectLogBtn_Click() - I file di log sono stati raccolti e zippati con successo.");
                    }

                    else
                    {
                        MessageBox.Show("Non sono presenti log. Effettua prima delle operazioni con l'applicativo, quindi ripeti " +
                                        "l'operazione di raccolta dei log.", "Raccolta log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Logger.Info("[INFO] CollectLogBtn_Click() - Non sono presenti file di log nella cartella del programma.");

                        if (File.Exists(saveFileDialog.FileName))
                            File.Delete(saveFileDialog.FileName);
                    }
                }

                catch (Exception ex)
                {
                    MessageBox.Show("Si è verificato un errore durante l'operazione:\n\n" + ex.Message, "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Error("[ERROR] CollectLogBtn_Click() - Si è verificato un errore durante l'operazione: " + ex.Message);
                }
            }
        }

        private void clearLogFolderBtn_Click(object sender, EventArgs e)
        {
            bool res = true;

            if(MessageBox.Show("Avanzando con l'operazione, verranno eliminati tutti i file di log generati dal software CIE ID. Confermi di voler procedere?",
                               "Eliminazione log", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
            {
                String[] files = Directory.GetFiles(@"C:\ProgramData\CIEPKI-NG");

                foreach (string fileName in files)
                {
                    try
                    {
                        if (Path.GetExtension(fileName).ToLower() == @".log")
                            File.Delete(fileName);
                    }

                    catch(Exception ex)
                    {
                        Logger.Error("[ERROR] ClearLogFolderBtn_Click() - Si è verificato un errore durante l'operazione: " + ex.Message);
                        res = false;
                    }
                }

                if (res)
                {
                    MessageBox.Show("L'eliminazione dei log è avvenuta con successo. Se hai riscontrato un'anomalia nel software " +
                                    "che intendi segnalare, puoi impostare il livello di logging su 'Debug', replicare l'operazione, " +
                                    "raccogliere i log con l'apposito pulsante e condividerli con lo sviluppatore.",
                                    "Eliminazione completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Logger.Info("[INFO] ClearLogFolderBtn_Click() - Log eliminati con successo.");
                }

                else
                {
                    MessageBox.Show("Si è verificato un errore durante la cancellazione dei log. È possibile che alcuni file siano aperti " +
                                    "ed in uso da terze parti, per cui non è stato possibile procedere con l'eliminazione.",
                                    "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void showIntroductionScreens_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.firstTime = showIntroductionScreens.Checked;
        }
    }
}
