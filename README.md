# CIE MIDDLEWARE NG (CSP-PKCS11 PER LA CIE 3.0) — build non ufficiale


## CASO D’USO

Il middleware CIE è una libreria software che implementa le interfacce crittografiche standard **PKCS#11** e **CSP**. Esso consente agli applicativi integranti di utilizzare il certificato di autenticazione e la relativa chiave privata memorizzati sul chip della CIE astraendo dalle modalità di comunicazione di basso livello. 

## ARCHITETTURA
La libreria è sviluppata in C++ su Visual Studio 2022 o successivo (toolset v145); l'applicazione grafica **CIE Middleware NG** (progetti **CIEID.Wpf** e **CIEID.Core**) è in C# su .NET 10 (WPF, MVVM); per compilare il modulo di installazione (progetto **Setup**) è inoltre necessario [NSIS](https://nsis.sourceforge.io/). Allo stato attuale è utilizzabile esclusivamente in ambiente Windows. Entrambe le interfacce sono esposte della stessa libreria (CIEPKI-NG.dll), che viene compilata dal progetto CSP. La libreria viene compilata sia in versione a 32 bit che a 64 bit.

L’interfaccia CSP è conforme alla versione 7 delle specifiche dei Minidriver pubblicate da Microsoft a [questo](http://download.microsoft.com/download/7/E/7/7E7662CF-CBEA-470B-A97E-CE7CE0D98DC2/sc-minidriver_specs_V7.docx) indirizzo.
L’interfaccia PKCS11 è conforme alla specifica [RSA 2.11](https://www.cryptsoft.com/pkcs11doc/v211/).

## CSP
Il Minidriver CIE gestisce la carta in modalità **Read-Only**, come previsto dalle specifiche §7.4, pertanto i comandi di creazione e cancellazione di oggetti non sono supportati. Si faccia riferimento alla specifica Microsoft per i dettagli su quali operazioni possono essere effettuate su una carta Read Only.
Il modulo CSP implementa anche uno store provider per i certificati, in modo tale da non richiedere l’operazione di propagazione dei certificati nello store di sistema.

## PKCS11
Allo stesso modo del CSP, anche il PKCS11 gestisce la carta in modalità **read-only**. Pertanto le operazioni di creazione, modifica e distruzione di qualsiasi oggetto restituiranno un errore.

## Setup
Il modulo di installazione del Middleware si compila tramite il progetto **Setup**, che richiede l'installazione di [NSIS](https://nsis.sourceforge.io/). Il setup installa sia la versione a 32 che a 64 bit, ed effettua la registrazione del CSP e dello Store provider. Il modulo PKCS11 non richiede registrazione, ma il nome del modulo (**CIEPKI-NG.dll**) deve essere noto alle applicazioni che lo utilizzano.

## Differenze rispetto alla versione originale

Questa è una build non ufficiale, derivata dal progetto [italia/cie-middleware](https://github.com/italia/cie-middleware) (ultimo commit upstream di partenza: `8dd385e`). La finestra riporta il titolo "CIE Middleware NG [build non ufficiale]" e usa una palette verde per distinguerla dall'originale. Il formato della cache delle CIE abbinate è invariato, quindi le CIE già registrate da altre versioni restano utilizzabili.

**Supporto.** Questa versione non è ufficialmente supportata dall'IPZS. Essendo un fork pubblico, riceve supporto dagli sviluppatori su GitHub (issue e pull request del repository del progetto).

### Build e piattaforma
- Toolset C++ v145 e Windows SDK 10.0.26100.0.
- `CIEID.Core`, `CIEID.Wpf` ed `EsempioCSP` migrati a progetti SDK-style per **.NET 10** (`net10.0-windows`); Newtonsoft.Json tramite `PackageReference`, `packages.config` rimosso.
- Rimossa la firma del codice dalla build (post-build di `CSP` e passi NSIS di `Setup`).

### Firma
- **Marca temporale RFC 3161** opzionale su PAdES e CAdES. TSA predefinita: Actalis (`http://timestamp.actalis.it`), sostituibile da Opzioni → "Marca temporale" con una TSA personalizzata ed eventuali credenziali (username/password, salvate cifrate) usate nella firma.
- **Fallback se la TSA non risponde**: l'applicazione chiede se firmare senza marca, usare DigiCert, usare solo l'ora del PC oppure indicare un URL personalizzato. Il nuovo codice di ritorno `CARD_TSA_ERROR` (`0xF2`) segnala il fallimento della marca.
- **Anteprima PDF** a schermo pieno durante la scelta della posizione della firma grafica, con nuova disposizione della schermata; al termine la finestra torna alle dimensioni normali.
- **Firma grafica ridimensionabile** con una maniglia in basso a destra, proporzionale alla pagina.
- Focus automatico sulla prima casella del PIN (firma e abbinamento).

### Verifica
- Nelle firme PAdES, che non hanno l'attributo *signing time*, l'ora di firma è quella della marca temporale. Senza marca viene mostrato "Firma senza marca temporale".

### Abbinamento CIE
- Gestione più tollerante delle CIE già abbinate e dei diversi lettori collegati al computer.
- Supporto a CIE già registrate da altre versioni.

### Sicurezza e robustezza
- Revisione generale del codice di abbinamento, firma, cache e comunicazione con i server di marca temporale, con maggiore robustezza nella gestione degli input, delle risorse e degli errori.
- Verifica dei certificati TLS e gestione più prudente delle credenziali eventualmente configurate (proxy, TSA).
- Log strutturato nell'applicazione grafica.

## Modulo PowerShell

Il modulo `CIE-Middleware-NG` (cartella `PowerShell/`) offre da riga di comando le stesse operazioni dell'applicazione; l'installer lo copia in `Program Files\WindowsPowerShell\Modules`, quindi è disponibile sia in Windows PowerShell 5.1 sia in PowerShell 7 (64 bit). Richiede `CIEPKI-NG.dll` installata e, per le operazioni sulla carta, un lettore con la CIE inserita.

| Comando | Funzione |
|---|---|
| `Get-Cie` | elenca le CIE abbinate (con quelle abbinate dall'applicazione) |
| `Register-Cie` | abbina la CIE sul lettore (PIN a 8 cifre) |
| `Unregister-Cie` | disabilita l'abbinamento di una CIE |
| `Set-CiePin` / `Unlock-CiePin` | cambio PIN / sblocco con PUK |
| `Add-CieSignature` | firma PAdES (PDF) o CAdES (.p7m), con marca temporale opzionale e firma grafica |
| `Test-CieSignature` | verifica le firme di un file |
| `Export-CieSignedContent` | estrae il documento originale da un .p7m |

Esempi:

```powershell
Get-Cie
Add-CieSignature .\contratto.pdf          # chiede il PIN (ultime 4 cifre) senza eco
Add-CieSignature .\atto.docx -NoTimestamp  # produce atto.docx.p7m
Test-CieSignature .\signed-contratto.pdf | Format-List
Export-CieSignedContent .\atto.docx.p7m
```

I PIN sono `SecureString` e, se omessi, vengono richiesti senza eco. Aiuto: `Get-Help Add-CieSignature -Full`.

### Specifiche del modulo

**Requisiti.** Windows a 64 bit, PowerShell 5.1 o 7 a 64 bit (il modulo lo verifica all'avvio di ogni comando), `CIEPKI-NG.dll` in `System32`. La verifica delle firme e l'estrazione dei .p7m non richiedono il lettore di smart card.

**Architettura.** Script module (`.psm1` + manifest `.psd1`): le funzioni native di `CIEPKI-NG.dll` (`AbbinaCIE`, `ChangePIN`, `UnlockPIN`, `firmaConCIE`, `VerificaCIEAbilitata`, `DisabilitaCIE`, `estraiP7m`, `verificaConCIE`, `getVerifyInfo`) sono richiamate via P/Invoke con un tipo C# compilato al caricamento (`Add-Type`). Le operazioni lente (abbinamento, cambio PIN, sblocco, firma) girano su un thread dedicato: le callback di avanzamento della DLL non possono eseguire script, quindi PowerShell legge lo stato dal thread e lo mostra con `Write-Progress` (i messaggi dettagliati con `-Verbose`). La versione del manifest segue `version.txt` (propagata dall'hook `pre-commit`).

**Elenco delle CIE.** La DLL non esporta una funzione per leggere il PAN della carta inserita, quindi il modulo tiene un proprio elenco in `%LOCALAPPDATA%\CIE-Middleware-NG\cie-powershell.json` (PAN, intestatario, seriale) e legge, in sola lettura, l'elenco dell'applicazione grafica dal suo `user.config`. `Get-Cie` unisce le due fonti e interroga la DLL per confermare che l'abbinamento sia attivo.

| Comando | Parametri principali | Note |
|---|---|---|
| `Get-Cie` | `-Pan` | Restituisce `Pan`, `Owner`, `SerialNumber`, `Abbinata`. |
| `Register-Cie` | `-Pin` | PIN di 8 cifre. Restituisce la CIE abbinata e la salva nell'elenco del modulo. |
| `Unregister-Cie` | `-Pan` (anche da pipeline) | Conferma richiesta (`-Confirm`/`-WhatIf`). Non rimuove la CIE dall'elenco dell'applicazione. |
| `Set-CiePin` | `-Pin`, `-NewPin` | Entrambi di 8 cifre; il nuovo PIN non può avere cifre uguali o consecutive (stesse regole dell'app). `-WhatIf` supportato. |
| `Unlock-CiePin` | `-Puk`, `-NewPin` | PUK di 8 cifre; stesse regole sul nuovo PIN. |
| `Add-CieSignature` | `-Path`, `-Pan`, `-Format Pades\|Cades`, `-OutFile`, `-Pin`, `-NoTimestamp`, `-TimestampUrl`, `-TimestampCredential`, `-SignatureImage`, `-Page`, `-X`, `-Y`, `-Width`, `-Height`, `-Force` | Vedi sotto. |
| `Test-CieSignature` | `-Path`, `-ProxyUrl`, `-ProxyPort`, `-ProxyCredential` | Un oggetto per firmatario: `Nome`, `CodiceFiscale`, `DataFirma`, `FirmaValida`, `CertificatoOk`, `Revoca`, `EnteEmittente`. |
| `Export-CieSignedContent` | `-Path`, `-OutFile`, `-Force` | Se manca `-OutFile` toglie `.p7m` e i marcatori `signed-`/`-signed`. |

**Firma (`Add-CieSignature`).**
- Formato: `Pades` per i `.pdf`, `Cades` per ogni altro file (si può forzare con `-Format`; PAdES su un non-PDF è un errore).
- CIE: `-Pan`, oppure l'unica CIE abbinata; se ce n'è più di una `-Pan` è obbligatorio.
- PIN: si possono indicare le sole ultime 4 cifre o il PIN intero (ne vengono usate le ultime 4).
- Uscita predefinita: `signed-<nome>.pdf` per PAdES, `<file>.p7m` per CAdES; un file esistente non viene sovrascritto senza `-Force`.
- Marca temporale: attiva di default (`http://timestamp.actalis.it`, come l'applicazione); `-NoTimestamp` la disattiva, `-TimestampUrl` indica un altro server, `-TimestampCredential` le credenziali.
- Firma grafica (solo PAdES, con `-SignatureImage`): `-Page` parte da 1; `-X`, `-Y`, `-Width`, `-Height` sono frazioni della pagina (0-1) con origine in alto a sinistra; `-Y` è il bordo inferiore dell'immagine. Valori predefiniti: 0.55, 0.95, 0.40, 0.10.
- Restituisce il `FileInfo` del file prodotto.

**Sicurezza.** PIN e PUK sono `SecureString`, richiesti senza eco se omessi, convertiti in testo solo per la chiamata alla DLL e azzerati subito dopo. Le credenziali di marca temporale e proxy sono `PSCredential`. Il modulo non scrive PIN o credenziali su disco né nei log.

**Errori.** Ogni errore è un `ErrorRecord` terminante con messaggio in italiano e `FullyQualifiedErrorId` `CIE_0x<codice>`, con il codice restituito dalla DLL (es. `0xA0` PIN errato con tentativi residui, `0xA4` carta bloccata, `0xE0` CIE assente, `0xF1` PAN diverso da quello indicato). I controlli sul formato dei PIN avvengono prima di toccare la carta, per non consumare tentativi.

**Installazione e disinstallazione.** L'installer NSIS copia `.psd1` e `.psm1` in `Program Files\WindowsPowerShell\Modules\CIE-Middleware-NG` e li rimuove con la disinstallazione; l'elenco `cie-powershell.json` resta con i dati utente.

## Sviluppo

### Struttura della soluzione
`CIE-Middleware-NG.sln` contiene:
- `CSP` — libreria nativa `CIEPKI-NG.dll` (minidriver CSP + modulo PKCS#11 + funzioni di abbinamento, firma e cambio/sblocco PIN usate dall'applicazione grafica);
- `CIEID.Core` — libreria .NET 10 senza interfaccia (P/Invoke verso la DLL, impostazioni, log, servizi);
- `CIEID.Wpf` — interfaccia grafica WPF/MVVM .NET 10 (eseguibile `CIE-Middleware-NG.exe`), interamente riscritta: non usa loghi, marchi né grafica istituzionale;
- `CIEID.Legacy` — la vecchia interfaccia WinForms, mantenuta funzionante sopra `CIEID.Core` (eseguibile `CIE-Middleware-NG-Legacy.exe`, non inclusa nell'installer); l'interfaccia principale resta quella WPF;
- `PowerShell` — modulo `CIE-Middleware-NG` (stesse operazioni dell'applicazione grafica da riga di comando, stessa DLL);
- `CacheLib`, `cie_sign_sdk` (terze parti, non modificare), `PKCS11`, `Setup` (installer NSIS), `EsempioCSP`, `EsempioPCSC`, `EsempioPKCS11`.

Il codice del fork (CIEID.Core, CIEID.Wpf, CSP, CacheLib) è commentato in italiano. Attenzione: il nome della DLL nei `DllImport` di `CIEID.Core/Native` deve coincidere con il file installato (`CIEPKI-NG.dll`), altrimenti l'abbinamento si blocca.

### Compilazione
```
MSBuild.exe CIE-Middleware-NG.sln -p:Configuration=Release -p:Platform=x64 -m -v:q -nologo
cd Setup
makensis.exe -V2 -DConfig=Release CIE-Middleware-NG.nsi
```
L'installer risultante è `Setup/Release/CIE-Middleware-NG.exe` (installazione silenziosa con `/S`).

### Coesistenza con l'originale
Il fork convive con l'installazione originale: file in `C:\Program Files\CIEPKI-NG`, DLL `CIEPKI-NG.dll` in System32, dati e cache in `C:\ProgramData\CIEPKI-NG`.

### Diagnostica e log
Il file `C:\ProgramData\CIEPKI-NG\config` accetta `LIB_LOG_LEVEL=n` (libreria) e `APP_LOG_LEVEL=n` (applicazione), con 0 = disattivato, 1 = debug, 2 = info, 3 = solo errori. I log sono `CIE-Middleware-NG_{data}.log` (app) e `CIEPKI-NG_{data}.log` (libreria); la scheda Impostazioni permette di raccoglierli o eliminarli.

### Versionamento
SemVer `MAJOR.MINOR.PATCH.BUILD`, sorgente unica `version.txt`. L'hook `.githooks/pre-commit` (attivare con `git config core.hooksPath .githooks`) incrementa BUILD a ogni commit senza mai azzerarlo e propaga la versione in `AssemblyInfo.cs`, nel file `.rc` e nello script NSIS; `./bump-version.sh major|minor|patch` modifica le prime tre cifre.

## Disclaimer

Questa è una versione **non ufficiale**, a scopo didattico e di studio open source, utilizzata a rischio dell'utente. Vedere [DISCLAIMER.md](DISCLAIMER.md).
