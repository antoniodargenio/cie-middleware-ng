# CIE MIDDLEWARE NG (CSP-PKCS11 PER LA CIE 3.0) — build non ufficiale


## CASO D’USO

Il middleware CIE è una libreria software che implementa le interfacce crittografiche standard **PKCS#11** e **CSP**. Esso consente agli applicativi integranti di utilizzare il certificato di autenticazione e la relativa chiave privata memorizzati sul chip della CIE astraendo dalle modalità di comunicazione di basso livello. 

## ARCHITETTURA
La libreria è sviluppata in C++ su Visual Studio 2022 o successivo (toolset v145); l'applicazione **CIEID** è in C# su .NET 10 (WinForms); per compilare il modulo di installazione (progetto **Setup**) è inoltre necessario [NSIS](https://nsis.sourceforge.io/). Allo stato attuale è utilizzabile esclusivamente in ambiente Windows. Entrambe le interfacce sono esposte della stessa libreria (CIEPKI-NG.dll), che viene compilata dal progetto CSP. La libreria viene compilata sia in versione a 32 bit che a 64 bit.

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
- `CIEID` ed `EsempioCSP` migrati a progetti SDK-style per **.NET 10** (`net10.0-windows`); Newtonsoft.Json tramite `PackageReference`, `packages.config` rimosso.
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
- Una CIE già presente in cache non viene più rifiutata (`CARD_ALREADY_ENABLED`): PIN e certificato vengono riletti e la cache riscritta.
- L'abbinamento si ferma al primo lettore riuscito, evitando `CKR_GENERAL_ERROR` con altri lettori (ad esempio YubiKey).
- Supporto a CIE già registrate da altre versioni.

### Sicurezza e robustezza
- Revisione generale del codice di abbinamento, firma, cache e comunicazione con i server di marca temporale, con maggiore robustezza nella gestione degli input, delle risorse e degli errori.
- Verifica dei certificati TLS e gestione più prudente delle credenziali eventualmente configurate (proxy, TSA).
- Log strutturato nell'applicazione `CIEID`.

## Sviluppo

### Struttura della soluzione
`CIE-Middleware-NG.sln` contiene:
- `CSP` — libreria nativa `CIEPKI-NG.dll` (minidriver CSP + modulo PKCS#11 + funzioni di abbinamento, firma e cambio/sblocco PIN usate da CIEID);
- `CIEID` — applicazione WinForms .NET 10 (eseguibile `CIE-Middleware-NG.exe`, namespace `CIEID`), chiama la DLL via P/Invoke;
- `CacheLib`, `cie_sign_sdk` (terze parti, non modificare), `PKCS11`, `Setup` (installer NSIS), `EsempioCSP`, `EsempioPCSC`, `EsempioPKCS11`.

Il codice del fork (CIEID, CSP, CacheLib) è commentato in italiano. Attenzione: il nome della DLL nei `DllImport` di `CIEID/MainForm.cs` deve coincidere con il file installato (`CIEPKI-NG.dll`), altrimenti l'abbinamento si blocca.

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

### Repository
Remoti: `github` (principale), `deb1` (mirror interno), `origin` (upstream italia/cie-middleware, sola lettura).

## Disclaimer

Questa è una versione **non ufficiale**, a scopo didattico e di studio open source, utilizzata a rischio dell'utente. Vedere [DISCLAIMER.md](DISCLAIMER.md).
