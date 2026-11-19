using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CIEID
{
    /// <summary>
    /// Finestra informativa sulla sicurezza del PIN (Windows chiede il PIN della CIE).
    /// Si chiude da sola dopo un conto alla rovescia di 5 secondi oppure con il pulsante OK.
    /// </summary>
    public partial class PINNotice : Form
    {
        // Secondi residui prima della chiusura automatica
        private int timerCountdown = 5;

        public PINNotice()
        {
            InitializeComponent();
            // Mostra la versione dell'applicazione nel titolo
            Text += " – v" + typeof(Program).Assembly.GetName().Version;
            buttonOK.Text = "OK (" + timerCountdown + ")";
            countdownTimer.Start();
        }

        // Gestore residuo del designer, non utilizzato
        private void timer1_Tick(object sender, EventArgs e)
        {

        }

        // Ferma il conto alla rovescia e nasconde la finestra
        private void buttonOK_Click(object sender, EventArgs e)
        {
            countdownTimer.Stop();
            Hide();
                        
        }

        // Ogni secondo aggiorna il conto alla rovescia; a zero chiude la finestra
        private void timer_Tick(object sender, EventArgs e)
        {
            timerCountdown--;
            if (timerCountdown < 0)
            {
                buttonOK_Click(sender, e);
            }
            else
            {
                buttonOK.Text = "OK (" + timerCountdown + ")";
            }
            
        }
    }
}
