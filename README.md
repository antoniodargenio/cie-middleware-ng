# MIDDLEWARE CSP-PKCS11 PER LA CIE 3.0 [![Build status](https://ci.appveyor.com/api/projects/status/dpc0ditjn04ylw6y?svg=true)](https://ci.appveyor.com/project/italia/cie-middleware)


## CASO D’USO

Il middleware CIE è una libreria software che implementa le interfacce crittografiche standard **PKCS#11** e **CSP**. Esso consente agli applicativi integranti di utilizzare il certificato di autenticazione e la relativa chiave privata memorizzati sul chip della CIE astraendo dalle modalità di comunicazione di basso livello. 

## ARCHITETTURA
La libreria è sviluppata in C++ su Visual Studio 2017 Community; per compilare il modulo di installazione (progetto **Setup**) è inoltre necessario [NSIS 3.02.1](https://sourceforge.net/projects/nsis/). Allo stato attuale è utilizzabile esclusivamente in ambiente Windows. Entrambe le interfacce sono esposte della stessa libreria (CIEPKI.dll), che viene compilata dal progetto CSP. La libreria viene compilata sia in versione a 32 bit che a 64 bit.

L’interfaccia CSP è conforme alla versione 7 delle specifiche dei Minidriver pubblicate da Microsoft a [questo](http://download.microsoft.com/download/7/E/7/7E7662CF-CBEA-470B-A97E-CE7CE0D98DC2/sc-minidriver_specs_V7.docx) indirizzo.
L’interfaccia PKCS11 è conforme alla specifica [RSA 2.11](https://www.cryptsoft.com/pkcs11doc/v211/).

## CSP
Il Minidriver CIE gestisce la carta in modalità **Read-Only**, come previsto dalle specifiche §7.4, pertanto i comandi di creazione e cancellazione di oggetti non sono supportati. Si faccia riferimento alla specifica Microsoft per i dettagli su quali operazioni possono essere effettuate su una carta Read Only.
Il modulo CSP implementa anche uno store provider per i certificati, in modo tale da non richiedere l’operazione di propagazione dei certificati nello store di sistema.

## PKCS11
Allo stesso modo del CSP, anche il PKCS11 gestisce la carta in modalità **read-only**. Pertanto le operazioni di creazione, modifica e distruzione di qualsiasi oggetto restituiranno un errore.

## Setup
Il modulo di installazione del Middleware si compila tramite il progetto **Setup**, che richiede l'installazione di [NSIS 3.02.1](https://sourceforge.net/projects/nsis/). Il setup installa sia la versione a 32 che a 64 bit, ed effettua la registrazione del CSP e dello Store provider. Il modulo PKCS11 non richiede registrazione, ma il nome del modulo (**CIEPKI.dll**) deve essere noto alle applicazioni che lo utilizzano.

## Differenze rispetto alla versione originale

Questa è una build non ufficiale, derivata dal progetto [italia/cie-middleware](https://github.com/italia/cie-middleware) (ultimo commit upstream di partenza: `8dd385e`). La finestra riporta il titolo "CIE ID [build non ufficiale]" e usa una palette verde per distinguerla dall'originale. Il formato della cache delle CIE abbinate è invariato, quindi le CIE già registrate da altre versioni restano utilizzabili.

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
- `firmaConCIE` e `AbbinaCIE`: validazione degli input, PIN azzerato in memoria (`SecureZeroMemory`), RAII (`unique_ptr`, `vector`) al posto di `malloc/free`, nessun leak sui ritorni anticipati, rilascio del contesto PC/SC e del certificato.
- `CacheLib`: controllo di dimensioni e lunghezze in lettura, PAN validato (niente *path traversal*), scrittura atomica tramite file temporaneo e `MoveFileEx`.
- `PdfPreview`: `mutool` eseguito con `ArgumentList` e timeout, risorse rilasciate, nessun blocco dei file immagine.
- `TSAClient`: copie di stringhe con limiti, timeout (10 s connessione, 30 s totali), solo HTTP/HTTPS anche nei redirect, limite di 1 MB alla risposta, controllo dello stato HTTP e rilascio dell'handle curl anche in caso di errore. La verifica del certificato TLS resta disattivata (libcurl 7.64.1 senza bundle di CA); la marca temporale è comunque firmata dalla TSA.
- `CIEID`: log strutturato (`Logger`) al posto di `Console.WriteLine` e dei `catch` vuoti.
