# Componenti di terze parti

CIE Middleware NG è un fork non ufficiale di *italia/cie-middleware* (vedere `DISCLAIMER.md`). Questo elenco riassume i componenti di terze parti usati o distribuiti. Le licenze complete sono quelle dei rispettivi progetti; verificarle prima di ogni ridistribuzione.

| Componente | Uso | Licenza |
|---|---|---|
| [italia/cie-middleware](https://github.com/italia/cie-middleware) (IPZS) | Codice nativo di base (CSP, PKCS#11, firma) | BSD 3-Clause, Copyright (c) 2017 Istituto Poligrafico e Zecca dello Stato (vedere `LICENSE`) |
| [Crypto++](https://www.cryptopp.com/) | Primitive crittografiche (`CSP/cryptopp`) | Boost Software License 1.0 |
| [OpenSSL](https://www.openssl.org/) | Firma e verifica (`cie_sign_sdk`) | Apache License 2.0 |
| [PoDoFo](https://github.com/podofo/podofo) | Manipolazione dei PDF per la firma PAdES | LGPL 2.0 / GPL |
| libcurl, zlib, libpng, FreeType, libxml2, libiconv, bzip2 | Dipendenze di `cie_sign_sdk` | Licenze permissive/LGPL dei rispettivi progetti |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM nell'interfaccia WPF | MIT |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | Serializzazione JSON | MIT |
| System.Management | Informazioni di sistema (.NET) | MIT |
| [MuPDF](https://mupdf.com/) (`mutool.exe`) | Anteprima delle pagine PDF, eseguito come processo separato | GNU AGPL v3 (Artifex); il sorgente è disponibile da Artifex |
| [Allura](https://fonts.google.com/specimen/Allura) | Carattere della firma grafica | SIL Open Font License 1.1 |

## Note

- `mutool.exe` è un programma indipendente, invocato da riga di comando e non collegato al codice dell'applicazione. Chi ridistribuisce il pacchetto deve rispettare la AGPL v3 per quel solo eseguibile (inclusa la disponibilità del sorgente corrispondente alla versione distribuita).
- L'interfaccia grafica (`CIEID.Wpf`) non contiene loghi, marchi o grafica dell'IPZS o di altre amministrazioni.
- Le versioni esatte dei componenti nativi vanno verificate nei rispettivi sorgenti e binari prima di una release.
