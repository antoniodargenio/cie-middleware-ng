using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CIEID.Core
{
    /// <summary>
    /// Insieme delle CIE abbinate, indicizzate per PAN. Viene serializzato/deserializzato in JSON
    /// (Newtonsoft.Json) per essere salvato nelle impostazioni dell'applicazione.
    /// </summary>
    public class CieCollection
    {
        // PAN della CIE -> dati della CIE
        private Dictionary<string, CieModel> mydictionary;


        public CieCollection()
        {
            mydictionary = new Dictionary<string, CieModel>();
        }

        // Costruisce la collezione a partire dal JSON salvato (stringa vuota = nessuna CIE)
        public CieCollection(string collectionString)
        {
            if(collectionString.Equals(""))
            {
                mydictionary = new Dictionary<string, CieModel>();
            }
            else
            {
                mydictionary = JsonConvert.DeserializeObject<Dictionary<string, CieModel>>(collectionString);
            }
        }

        public Dictionary<string, CieModel> MyDictionary
        {
            get { return mydictionary; }
            set { mydictionary = value; }
        }

        // Aggiunge (o aggiorna) una CIE
        public void addCie(String pan, CieModel cieModel)
        {
            if (mydictionary != null)
                mydictionary[pan] = cieModel; // idempotente: sovrascrive se la CIE e' gia' presente
        }

        // Rimuove la CIE con il PAN indicato
        public void removeCie(String pan)
        {
            if (mydictionary != null)
                mydictionary.Remove(pan);
        }

        // Rimuove tutte le CIE abbinate
        public void removeAllCie()
        {
            if (mydictionary != null)
                mydictionary.Clear();
        }
    }
}
