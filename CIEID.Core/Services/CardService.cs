using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CIEID.Core
{
    /// <summary>Esito di un'operazione sulla carta: codice nativo e tentativi residui del PIN.</summary>
    public class CardResult
    {
        public int Code;
        public int AttemptsLeft;
        public bool Ok { get { return Code == NativeCodes.CKR_OK; } }

        public string Describe()
        {
            switch (Code)
            {
                case NativeCodes.CKR_OK: return "Operazione completata.";
                case NativeCodes.CKR_TOKEN_NOT_PRESENT:
                case NativeCodes.CKR_TOKEN_NOT_RECOGNIZED: return "Nessuna carta rilevata: appoggia la CIE sul lettore e riprova.";
                case NativeCodes.CKR_PIN_INCORRECT: return "PIN errato. Tentativi rimasti: " + AttemptsLeft + ".";
                case NativeCodes.CKR_PIN_LOCKED: return "La carta è bloccata: usa il PUK per sbloccarla.";
                case NativeCodes.CKR_PIN_LEN_RANGE: return "Il PIN deve contenere solo cifre, nella lunghezza richiesta.";
                case NativeCodes.CARD_ALREADY_ENABLED: return "Questa carta è già abbinata.";
                case NativeCodes.CARD_PAN_MISMATCH: return "La carta sul lettore è diversa da quella selezionata.";
                case NativeCodes.CARD_TSA_ERROR: return "Marca temporale non ottenuta.";
                case NativeCodes.CKR_GENERAL_ERROR: return "Errore di comunicazione con la carta.";
                default: return "Operazione non riuscita (codice 0x" + Code.ToString("X") + ").";
            }
        }
    }

    /// <summary>Gestione delle carte abbinate: abbinamento, rimozione, cambio PIN, sblocco.</summary>
    public class CardService
    {
        private readonly ISettingsStore settings;
        private readonly Logger log;
        private CieCollection coll;

        public CardService(ISettingsStore settings, Logger log)
        {
            this.settings = settings;
            this.log = log;
            Reload();
        }

        public void Reload()
        {
            try { coll = new CieCollection(settings.CieList ?? ""); }
            catch (Exception ex) { log.Error("CieList non leggibile: " + ex.Message); coll = new CieCollection(); }
        }

        public CieModel[] Cards { get { return coll.MyDictionary.Values.ToArray(); } }

        private void Persist()
        {
            settings.CieList = JsonConvert.SerializeObject(coll.MyDictionary);
            settings.Save();
        }

        public Task<CardResult> PairAsync(string pin, Action<int, string> progress)
        {
            return Task.Run(delegate
            {
                int[] attempts = new int[1];
                // I delegate restano referenziati fino al termine della chiamata nativa
                ProgressCallback pcb = delegate (int p, string m)
                {
                    try { if (progress != null) progress(p, m); } catch (Exception) { }
                    return 0;
                };
                CompletedCallback ccb = delegate (string pan, string name, string ef)
                {
                    try
                    {
                        string nice = new CultureInfo("it-IT", false).TextInfo.ToTitleCase((name ?? "").ToLower());
                        coll.addCie(pan, new CieModel(ef, nice, pan));
                        Persist();
                        return 0;
                    }
                    catch (Exception ex) { log.Error("Abbinamento: " + ex); return -1; }
                };
                int ret = NativeMethods.AbbinaCIE(null, pin, attempts, pcb, ccb);
                GC.KeepAlive(pcb); GC.KeepAlive(ccb);
                log.Info("AbbinaCIE -> 0x" + ret.ToString("X"));
                return new CardResult { Code = ret, AttemptsLeft = attempts[0] };
            });
        }

        public Task<CardResult> RemoveAsync(string pan)
        {
            return Task.Run(delegate
            {
                int ret = NativeMethods.DisabilitaCIE(pan);
                if (ret == NativeCodes.CKR_OK)
                {
                    coll.removeCie(pan);
                    Persist();
                }
                return new CardResult { Code = ret };
            });
        }

        public Task<CardResult> ChangePinAsync(string pin, string newPin, Action<int, string> progress)
        {
            return Task.Run(delegate
            {
                int[] attempts = new int[1];
                ProgressCallback pcb = delegate (int p, string m)
                {
                    try { if (progress != null) progress(p, m); } catch (Exception) { }
                    return 0;
                };
                int ret = NativeMethods.ChangePIN(pin, newPin, attempts, pcb);
                GC.KeepAlive(pcb);
                return new CardResult { Code = ret, AttemptsLeft = attempts[0] };
            });
        }

        public Task<CardResult> UnlockPinAsync(string puk, string newPin, Action<int, string> progress)
        {
            return Task.Run(delegate
            {
                int[] attempts = new int[1];
                ProgressCallback pcb = delegate (int p, string m)
                {
                    try { if (progress != null) progress(p, m); } catch (Exception) { }
                    return 0;
                };
                int ret = NativeMethods.UnlockPIN(puk, newPin, attempts, pcb);
                GC.KeepAlive(pcb);
                return new CardResult { Code = ret, AttemptsLeft = attempts[0] };
            });
        }
    }
}
