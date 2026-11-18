========================================================================
    APPLICAZIONE CONSOLE : EsempioPKCS11
========================================================================

Esempio di utilizzo del modulo PKCS#11 (CIEPKI-NG.dll) da un'applicazione
console C++. Generato con la procedura guidata di Visual Studio e poi adattato.

File del progetto:

EsempioPKCS11.vcxproj
    File di progetto principale (piattaforme, configurazioni, opzioni).

EsempioPKCS11.vcxproj.filters
    Associa i file ai filtri mostrati in Esplora soluzioni.

EsempioPKCS11.cpp
    Sorgente principale: carica la libreria PKCS#11 e invoca le funzioni di esempio.

StdAfx.h, StdAfx.cpp
    Generano l'header precompilato EsempioPKCS11.pch e StdAfx.obj.

/////////////////////////////////////////////////////////////////////////////
Note:

I commenti "TODO:" indicano le parti da completare o personalizzare.
Il nome del modulo da caricare e' CIEPKI-NG.dll (installata in System32).
