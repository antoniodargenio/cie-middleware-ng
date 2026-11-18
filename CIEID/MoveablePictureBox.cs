using System;
using System.Drawing;
using System.Windows.Forms;

namespace CIEID
{
    // Immagine trascinabile e ridimensionabile (maniglia nell'angolo in basso a destra,
    // proporzioni dell'immagine mantenute) all'interno del controllo padre.
    public class MoveablePictureBox : PictureBox
    {
        private const int GripSize = 14;
        private const int MinWidth = 40;

        private Point BoxLocation;
        private bool resizing = false;
        private double aspect = 3.0; // larghezza / altezza

        public MoveablePictureBox()
        {
            this.SetStyle(ControlStyles.ResizeRedraw, true);
        }

        private bool InGrip(Point p)
        {
            return p.X >= this.Width - GripSize && p.Y >= this.Height - GripSize;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            BoxLocation = e.Location;
            if (e.Button == MouseButtons.Left && InGrip(e.Location))
            {
                resizing = true;
                if (this.Height > 0)
                    aspect = (double)this.Width / this.Height;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            resizing = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && resizing && this.Parent != null)
            {
                int maxW = Math.Min(this.Parent.Width - this.Left, (int)((this.Parent.Height - this.Top) * aspect));
                int newW = Math.Max(MinWidth, Math.Min(e.X, maxW));
                this.Width = newW;
                this.Height = Math.Max(1, (int)(newW / aspect));
            }
            else if (e.Button == MouseButtons.Left)
            {
                int tempLeft = this.Left + (e.X - BoxLocation.X);
                int tempTop = this.Top + (e.Y - BoxLocation.Y);

                if (tempLeft < 0)
                    tempLeft = 0;

                if (tempTop < 0)
                    tempTop = 0;

                if (tempLeft > this.Parent.Width - this.Width)
                    tempLeft = this.Parent.Width - this.Width;

                if (tempTop > this.Parent.Height - this.Height)
                    tempTop = this.Parent.Height - this.Height;

                this.Left = tempLeft;
                this.Top = tempTop;
            }
            else
            {
                this.Cursor = InGrip(e.Location) ? Cursors.SizeNWSE : Cursors.SizeAll;
            }

            base.OnMouseMove(e);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            // maniglia di ridimensionamento
            Rectangle g = new Rectangle(this.Width - GripSize, this.Height - GripSize, GripSize - 1, GripSize - 1);
            using (Brush b = new SolidBrush(Color.FromArgb(200, Color.SeaGreen)))
                pe.Graphics.FillRectangle(b, g);
            pe.Graphics.DrawRectangle(Pens.White, g);
        }
    }
}
