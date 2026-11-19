using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CIEID.Wpf.ViewModels;

namespace CIEID.Wpf.Views
{
    public partial class SignView : UserControl
    {
        private bool dragging;
        private Point grab;

        public SignView()
        {
            InitializeComponent();
            DataContextChanged += delegate { Hook(); };
            Overlay.SizeChanged += delegate { PlaceBox(); };
        }

        private SignViewModel VM { get { return DataContext as SignViewModel; } }

        private void Hook()
        {
            SignViewModel vm = VM;
            if (vm == null) return;
            vm.PropertyChanged += delegate (object s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "Pades" && CadesRadio != null) CadesRadio.IsChecked = !vm.Pades;
                if (e.PropertyName == "PageImage") Dispatcher.BeginInvoke(new Action(PlaceBox));
            };
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0 && VM != null) VM.SetFile(files[0]);
        }

        private void Cades_Checked(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.Pades = false;
        }

        private void PinBox_Changed(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.Pin = PinBox.Password;
        }

        private void PrevPage(object sender, RoutedEventArgs e) { if (VM != null && VM.Page > 1) VM.Page--; }
        private void NextPage(object sender, RoutedEventArgs e) { if (VM != null && VM.Page < VM.PageCount) VM.Page++; }

        private void PlaceBox()
        {
            SignViewModel vm = VM;
            double w = Overlay.ActualWidth, h = Overlay.ActualHeight;
            if (vm == null || w <= 0 || h <= 0) return;
            Box.Width = vm.BoxW * w;
            Box.Height = vm.BoxH * h;
            Canvas.SetLeft(Box, vm.BoxX * w);
            Canvas.SetTop(Box, vm.BoxY * h);
        }

        private void Box_Down(object sender, MouseButtonEventArgs e)
        {
            dragging = true;
            grab = e.GetPosition(Box);
            Box.CaptureMouse();
        }

        private void Box_Move(object sender, MouseEventArgs e)
        {
            if (!dragging || VM == null) return;
            Point p = e.GetPosition(Overlay);
            double w = Overlay.ActualWidth, h = Overlay.ActualHeight;
            double left = Math.Max(0, Math.Min(w - Box.Width, p.X - grab.X));
            double top = Math.Max(0, Math.Min(h - Box.Height, p.Y - grab.Y));
            Canvas.SetLeft(Box, left);
            Canvas.SetTop(Box, top);
            VM.BoxX = left / w;
            VM.BoxY = top / h;
        }

        private void Box_Up(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            Box.ReleaseMouseCapture();
        }
    }
}
