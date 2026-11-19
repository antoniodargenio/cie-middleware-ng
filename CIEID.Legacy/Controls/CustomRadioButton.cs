using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace CIEID.Controls
{
    /// <summary>Radio button disegnato a mano: cerchietto verde, pieno quando selezionato.</summary>
    class CustomRadioButton : RadioButton
    {
        // Colore del pallino quando e' selezionato
        private Color onColor = Color.FromArgb(0, 168, 107);
        // Colore del bordo
        private Color offColor = Color.FromArgb(0, 168, 107);

        public CustomRadioButton()
        {
            // Sfondo trasparente per integrarsi con il controllo contenitore
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        // Disegno personalizzato: bordo circolare e, se selezionato, riempimento
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            graphics.Clear(Parent.BackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var dotDiameter = ClientRectangle.Width - 2;
            var innerRect = new RectangleF(ClientRectangle.X, ClientRectangle.Y, dotDiameter, dotDiameter);

            var pen = new Pen(offColor, 1);
            graphics.DrawEllipse(pen, innerRect);

            if (this.Checked)
            {
                graphics.FillEllipse(new SolidBrush(onColor), innerRect);
            }
        }

    }
}
