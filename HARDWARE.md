# Requisiti hardware: lettori di smart card e NFC

CIE Middleware NG parla con la Carta d'Identità Elettronica (CIE 3.0) tramite l'interfaccia **PC/SC** di Windows. Qualunque lettore che Windows espone come lettore di smart card PC/SC e che riesce a dialogare con la CIE può essere usato: il progetto non mantiene un elenco di modelli certificati.

## Requisiti del lettore

| Requisito | Specifica |
|---|---|
| Interfaccia verso la carta | **Contactless (NFC, 13,56 MHz)**: la CIE 3.0 si usa appoggiandola al lettore, senza contatti metallici |
| Standard radio | **ISO/IEC 14443** (la CIE usa il tipo B; i lettori con ISO 14443 A/B sono la norma) |
| Standard del lettore | Conforme a **PC/SC** (drivers CCID o driver del produttore) |
| Collegamento al PC | USB (tipico), oppure lettore NFC integrato nel computer, purché esposto a Windows come lettore PC/SC |
| Driver | Quelli inclusi in Windows per i lettori **CCID** ("Microsoft Usbccid Smartcard Reader") bastano per la maggior parte dei modelli; in caso contrario il driver del produttore |
| Servizio Windows | Servizio **Smart Card** (`SCardSvr`) in esecuzione |

Un lettore a sole smart card con contatti (ISO 7816, formato carta con chip) **non** legge la CIE 3.0, che non ha contatti. Alcuni lettori sono "dual interface" (contatti + contactless): vanno bene purché sia presente la parte contactless.

### Comportamento atteso dal lettore
- Presenta la CIE come carta PC/SC con un **ATR** sintetizzato dal lettore: il middleware la riconosce confrontando l'ATR con quelli noti, quindi un lettore che non fornisce un ATR corretto per le carte ISO 14443-B non funziona.
- Accetta il protocollo negoziato con la carta (il middleware usa `SCARD_PROTOCOL_Tx`, cioè T=0 o T=1) e la ripresa della connessione (`SCardReconnect`) con reset o spegnimento della carta.
- Consente la condivisione (`SCARD_SHARE_SHARED`) e le transazioni PC/SC.
- Non perde la connessione durante le operazioni lunghe (firma, abbinamento): con lettori molto economici o con firmware datato possono comparire errori di comunicazione, e in tal caso conviene provare un altro modello.

## Chip della CIE riconosciuti

Il middleware riconosce le CIE in base all'ATR: produttori **NXP**, **Gemalto**, **STMicroelectronics**, **Actalis** e **Bit4id** (varianti incluse; l'elenco è in `CSP/CSP/ATR.cpp`). Tutte sono CIE 3.0 contactless.

## Requisiti del computer

| Requisito | Specifica |
|---|---|
| Sistema operativo | Windows a 64 bit (Windows 10 o 11), 64 bit obbligatori per `CIEPKI-NG.dll` |
| Porta | USB libera per il lettore esterno (o NFC integrato) |
| Rete | Internet per marca temporale (server Actalis di default) e verifica della revoca dei certificati |

## Verifica rapida del lettore

Il lettore è visibile a Windows se compare tra i lettori di smart card:

```powershell
Get-PnpDevice -Class SmartCardReader | Format-Table FriendlyName, Status
Get-Service SCardSvr
```

Poi, con la CIE appoggiata al lettore:

```powershell
Get-Cie        # dal modulo PowerShell: mostra le CIE abbinate
Register-Cie   # abbina la CIE sul lettore (richiede il PIN di 8 cifre)
```

Se l'abbinamento risponde "CIE non presente sul lettore" la carta non è stata riconosciuta: controllare che sia appoggiata al centro dell'antenna, provare a spostarla leggermente e che non ci siano altre carte contactless vicine.

## Problemi tipici

| Sintomo | Causa probabile |
|---|---|
| Nessun lettore disponibile | Lettore non collegato, driver mancante o servizio Smart Card fermo |
| "CIE non presente sul lettore" | Carta mal posizionata, lettore senza interfaccia contactless, ATR non riconosciuto |
| Errori casuali durante la firma | Carta spostata durante l'operazione: tenerla ferma finché non termina |
| Più lettori collegati | Il middleware cerca la CIE su tutti i lettori e usa il primo che ne ha una valida |

## Discussioni e supporto

Per domande, esperienze con i lettori e segnalazioni usa le [Discussions](https://github.com/antoniodargenio/cie-middleware-ng/discussions) del progetto. Non scrivere mai PIN o PUK, nemmeno parziali.

| Categoria | Per cosa |
|---|---|
| [Hardware](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Lettori NFC e smart card: modelli che funzionano o danno problemi, driver, ATR non riconosciuti |
| [Installazione e aggiornamenti](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Installer, disinstallazione, convivenza con il middleware originale, aggiornamenti |
| [Firma e verifica](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Firma PAdES/CAdES, marca temporale, firma grafica, verifica dei file firmati |
| [Modulo PowerShell](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Cmdlet, script, automazione, errori `CIE_0x…` |
| [PIN, PUK e abbinamento](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Abbinamento della CIE, cambio e sblocco del PIN |
| [Compatibilità](https://github.com/antoniodargenio/cie-middleware-ng/discussions) | Versioni di Windows, antivirus, browser e applicazioni di terze parti |

Un problema del programma (non una domanda) va aperto come [Issue](https://github.com/antoniodargenio/cie-middleware-ng/issues).
