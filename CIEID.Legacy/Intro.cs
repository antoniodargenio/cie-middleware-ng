using System;
using System.Windows.Forms;

namespace CIEID
{
    /// <summary>Schermata iniziale (due pagine) mostrata al primo avvio, con avviso di versione non ufficiale.</summary>
    public partial class Intro : Form
    {
        public Intro()
        {
            InitializeComponent();
            // Versione nel titolo e avviso rosso in fondo a entrambe le pagine
            Text += " – v" + typeof(Program).Assembly.GetName().Version;
            AddDisclaimer(tabPage1);
            AddDisclaimer(tabPage2);
        }

        // Avviso 'versione non ufficiale' in fondo a ogni pagina della schermata iniziale.
        private static void AddDisclaimer(TabPage page)
        {
            Label label = new Label();
            label.Text = "AVVISO – VERSIONE NON UFFICIALE. Questo software è un derivato (fork) dell'applicazione originale, " +
                "con modifiche alla sicurezza e alle funzionalità, realizzato a scopo didattico e di studio open source. " +
                "L'utilizzo è a esclusivo rischio dell'utente; " +
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

        // Avvia l'applicazione; con la casella 'non mostrare piu'' salva la scelta nelle impostazioni
        private void buttonStart_Click(object sender, EventArgs e)
        {
            if(checkBoxNoMore.Checked)
            {
                Properties.Settings.Default.firstTime = false;
                Properties.Settings.Default.Save(); // Saves settings in application configuration file                
            }

            // Apre la finestra principale e nasconde la schermata iniziale
            mainForm mainForm = new mainForm(null);
            mainForm.Show();

            Hide();
        }

        // Passa dalla prima alla seconda pagina
        private void buttonContinue_Click(object sender, EventArgs e)
        {
            tabControl.SelectedIndex = 1;
            tabControl.Refresh();
        }
    }
}
