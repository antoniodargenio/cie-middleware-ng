using System;
using System.Linq;
using System.Windows.Forms;

namespace CIEID.Controls
{
    /// <summary>
    /// Logica del carosello delle CIE abbinate: mostra al massimo tre carte (precedente, attiva, successiva)
    /// usando una lista circolare, e gestisce i pallini di navigazione e i pulsanti di scorrimento.
    /// L'elemento attivo e' sempre Items[1].
    /// </summary>
    class CarouselControl
    {

        // Diametro (px) dei pallini di navigazione
        private const int DOT_SIZE = 10;

        // Argomenti dell'evento che abilita/mostra i pulsanti di scorrimento sinistro/destro
        public class ButtonsEventArgs : EventArgs
        {
            public bool IsRightButton { get; set; }
            public bool IsEnabled { get; set; }
            public bool IsVisible { get; set; }

            public ButtonsEventArgs(bool isRightButton, bool isEnabled) : this(isRightButton, isEnabled, true)
            { }

            public ButtonsEventArgs(bool isRightButton, bool isEnabled, bool isVisible)
            {
                this.IsRightButton = isRightButton;
                this.IsEnabled = isEnabled;
                this.IsVisible = isVisible;
            }
        }

        // Evento sollevato quando lo stato dei pulsanti di scorrimento deve cambiare
        private EventHandler<ButtonsEventArgs> onButtonsChanged;

        public event EventHandler<ButtonsEventArgs> ButtonsChanged
        {
            add
            {
                onButtonsChanged += value;
            }
            remove
            {
                onButtonsChanged -= value;
            }
        }

        // Elenco circolare delle CIE. In scrittura aggiunge elementi vuoti di riempimento
        // (con 1 o 2 carte) per avere sempre tre posizioni visualizzabili.
        public CircularViewList Items
        {
            get
            {
                return circularList;
            }
            set
            {
                if (value == null)
                    return;

                circularList = new CircularViewList();

                activeItemIndex = 1;

                if (value.Count == 1)
                {
                    circularList.Add(new CieModel());
                    circularList.Add(value.First());
                    circularList.Add(new CieModel());
                }
                else if (value.Count == 2)
                {
                    circularList.Add(new CieModel());
                    circularList.AddRange(value);

                    activeItemIndex = 0;
                }
                else
                {
                    circularList.AddRange(value);
                }

                UpdateButtons();
            }
        }

        // Numero di CIE reali (esclusi i segnaposto vuoti)
        public int CarouselItemsCount
        {
            get
            {
                return Items.Count(x => !x.IsEmpty);
            }
        }

        // CIE attualmente al centro del carosello
        public CieModel ActiveCieModel
        {
            get
            {
                return Items[1];
            }
        }

        // Stato interno: lista circolare, contenitori grafici e indice del pallino attivo
        private CircularViewList circularList;
        private FlowLayoutPanel dotsGroup;
        private TableLayoutPanel mainContainer;

        private int activeItemIndex { get; set; }
        private bool dotsCreated = false;

        // mainContainer contiene le tre schede; dotsGroup i pallini di navigazione
        public CarouselControl(TableLayoutPanel mainContainer, FlowLayoutPanel dotsGroup)
        {
            this.mainContainer = mainContainer;
            this.dotsGroup = dotsGroup;
        }

        // Carica le CIE dalla collezione e forza la ricreazione dei pallini
        public void LoadData(CieCollection cieCollection)
        {
            Items = new CircularViewList(cieCollection.MyDictionary.Values);
            dotsCreated = false;
        }

        // Con piu' di 2 carte entrambi i pulsanti sono attivi; con 2 solo quello utile;
        // con meno di 2 vengono nascosti
        private void UpdateButtons()
        {
            if (CarouselItemsCount > 2)
            {
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(false, true));
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(true, true));
            }
            else
            if (CarouselItemsCount == 2)
            {
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(false, circularList.LastOrDefault().IsEmpty));
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(true, circularList.FirstOrDefault().IsEmpty));
            }
            else
            {
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(false, false, false));
                onButtonsChanged?.Invoke(this, new ButtonsEventArgs(true, false, false));
            }
        }

        // Fa scorrere il carosello verso sinistra di 'steps' posizioni
        public void ShiftLeft(int steps = 1)
        {
            while (steps-- > 0)
            {
                circularList.ShiftLeft();

                activeItemIndex++;

                if (activeItemIndex >= CarouselItemsCount)
                    activeItemIndex = 0;
            }

            UpdateLayout();
            UpdateButtons();
            UpdateDots();
        }

        // Fa scorrere il carosello verso destra di 'steps' posizioni
        public void ShiftRight(int steps = 1)
        {
            while (steps-- > 0)
            {
                circularList.ShiftRight();

                activeItemIndex--;

                if (activeItemIndex < 0)
                    activeItemIndex = CarouselItemsCount - 1;
            }

            UpdateLayout();
            UpdateButtons();
            UpdateDots();
        }

        // Seleziona il pallino corrispondente alla carta attiva senza riattivare il gestore dell'evento
        private void UpdateDots()
        {
            var radioButton = (RadioButton)this.dotsGroup.Controls[activeItemIndex];

            radioButton.CheckedChanged -= dot_CheckedChanged;
            radioButton.Checked = true;
            radioButton.CheckedChanged += dot_CheckedChanged;
        }

        // Ricrea i pallini di navigazione (uno per ciascuna CIE reale)
        public void CreateDots()
        {
            this.dotsGroup.Controls.Clear();

            if (Items != null && CarouselItemsCount > 1)
            {
                for (int i = 0; i < CarouselItemsCount; i++)
                {
                    var dot = new CustomRadioButton
                    {
                        Name = String.Format("Dot#{0}", i),
                        Size = new System.Drawing.Size(DOT_SIZE, DOT_SIZE),
                        Anchor = (AnchorStyles.None),
                        Checked = activeItemIndex == i
                    };

                    dot.CheckedChanged += dot_CheckedChanged;

                    this.dotsGroup.Controls.Add(dot);
                }
            }
            else if(Items != null && CarouselItemsCount == 1)
            {
                var dot = new CustomRadioButton
                {
                    Name = "Dot1",
                    Size = new System.Drawing.Size(DOT_SIZE, DOT_SIZE),
                    Anchor = (AnchorStyles.None),
                    Checked = true
                };

                this.dotsGroup.Controls.Add(dot);
            }

            dotsCreated = true;
        }

        // Click su un pallino: scorre fino alla carta corrispondente
        private void dot_CheckedChanged(object sender, EventArgs e)
        {
            var radioButton = (RadioButton)sender;

            if (radioButton.Checked)
            {
                var index = this.dotsGroup.Controls.GetChildIndex(radioButton);
                var diff = Math.Abs(activeItemIndex - index);

                if (activeItemIndex < index)
                {
                    ShiftLeft(diff);
                }
                else if (activeItemIndex > index)
                {
                    ShiftRight(diff);
                }

                activeItemIndex = index;
            }
        }

        // Aggiorna le tre schede visibili con i dati delle prime tre voci della lista
        public void UpdateLayout()
        {
            if (circularList != null && circularList.Count > 0)
            {
                var index = 0;

                foreach (var item in circularList.Take(3))
                {
                    UpdateCarouselItem(index, item);

                    index++;
                }
            }

            if (!dotsCreated)
                CreateDots();
        }

        // Mostra numero di serie e intestatario nella scheda 'index', oppure la nasconde se vuota
        private void UpdateCarouselItem(int index, CieModel model)
        {
            var carouselItem = mainContainer.Controls.Find(String.Format("carouselItem{0}", index), false).FirstOrDefault();

            if (model.IsEmpty)
            {
                carouselItem.Hide();
            }
            else
            {
                var labelCardNumberValue = carouselItem.Controls.Find(String.Format("labelCardNumberValue{0}", index), false).FirstOrDefault();
                var labelOwnerValue = carouselItem.Controls.Find(String.Format("labelOwnerValue{0}", index), false).FirstOrDefault();

                labelCardNumberValue.Text = model.SerialNumber;
                labelOwnerValue.Text = model.Owner;

                carouselItem.Show();
            }
        }
    }
}