using System;
using System.Windows.Forms;

namespace CIEID
{
    public partial class Intro : Form
    {
        public Intro()
        {
            InitializeComponent();
            AddDisclaimer(tabPage1);
            AddDisclaimer(tabPage2);
        }

        // Avviso 'versione non ufficiale' in fondo a ogni pagina della schermata iniziale.
        private static void AddDisclaimer(TabPage page)
        {
            Label label = new Label();
            label.Text = "AVVISO – VERSIONE NON UFFICIALE. Questo software è un derivato (fork) dell'applicazione originale, " +
                "con modifiche alla sicurezza e alle funzionalità, realizzato a scopo didattico e di studio open source. " +
                "Non è la versione ufficiale IPZS. L'utilizzo è a esclusivo rischio dell'utente; " +
                "lo sviluppatore declina ogni responsabilità.";
            label.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Bold);
            label.ForeColor = System.Drawing.Color.Red;
            label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            label.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            int width = page.ClientSize.Width - 40;
            System.Drawing.Size measured = TextRenderer.MeasureText(label.Text, label.Font,
                new System.Drawing.Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int height = measured.Height + 6;
            label.SetBounds(20, page.ClientSize.Height - height - 2, width, height);
            page.Controls.Add(label);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if(checkBoxNoMore.Checked)
            {
                Properties.Settings.Default.firstTime = false;
                Properties.Settings.Default.Save(); // Saves settings in application configuration file                
            }

            mainForm mainForm = new mainForm(null);
            mainForm.Show();

            Hide();
        }

        private void buttonContinue_Click(object sender, EventArgs e)
        {
            tabControl.SelectedIndex = 1;
            tabControl.Refresh();
        }
    }
}
