using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIEID.Core
{
    /// <summary>Dati di una CIE abbinata all'applicazione.</summary>
    public class CieModel
    {
        // Numero seriale della carta
        public String SerialNumber { get; set; }
        // Intestatario (nome e cognome)
        public String Owner { get; set; }
        // PAN: identificativo univoco della CIE usato come chiave di ricerca
        public String Pan { get; set; }
        // true se per questa CIE e' stata personalizzata la firma grafica
        public bool isCustomSign { get; set; }

        // Vero se il modello non contiene alcun dato
        public bool IsEmpty
        {
            get
            {
                return String.IsNullOrEmpty(SerialNumber) &&
                    String.IsNullOrEmpty(Owner) &&
                    String.IsNullOrEmpty(Pan);
            }
        }

        public CieModel()
        { }

        public CieModel(String serialNumber, String owner)
        {
            this.SerialNumber = serialNumber;
            this.Owner = owner;
            this.isCustomSign = false;
        }

        public CieModel(String serialNumber, String owner, String pan)
        {
            this.SerialNumber = serialNumber;
            this.Owner = owner;
            this.Pan = pan;
            this.isCustomSign = false;
        }
    }
}
