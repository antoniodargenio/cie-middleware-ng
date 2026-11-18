# DISCLAIMER

## Natura di questo software

Questo repository è un **fork non ufficiale** del middleware CIE originale (progetto *italia/cie-middleware*), con modifiche alla sicurezza e alle funzionalità. Le modifiche sono state realizzate a **scopo didattico e di studio open source**.

**Questo software NON è la versione ufficiale distribuita dall'Istituto Poligrafico e Zecca dello Stato (IPZS)** e non è approvato, certificato, supportato né collegato in alcun modo all'IPZS, al Ministero dell'Interno o ad altre amministrazioni. Marchi e denominazioni appartengono ai rispettivi titolari.

## Esclusione di responsabilità

Il software è fornito **"così com'è" (as is)**, senza garanzie di alcun tipo, esplicite o implicite, incluse a titolo esemplificativo garanzie di commerciabilità, idoneità a uno scopo particolare, sicurezza, assenza di errori e non violazione di diritti di terzi.

L'utilizzo avviene **a esclusivo rischio e pericolo dell'utente**. Lo sviluppatore non si assume alcuna responsabilità per danni diretti o indiretti derivanti dall'uso, o dall'impossibilità di usare, il software, ivi inclusi, a titolo esemplificativo: perdita di dati, blocco del PIN o della carta, firme digitali non valide o non riconosciute, malfunzionamenti, danni a dispositivi o a documenti.

## Avvertenze d'uso

- Per usi ufficiali, professionali o aventi valore legale utilizzare esclusivamente il software ufficiale fornito dall'IPZS.
- Le firme prodotte con questo software potrebbero non essere accettate da terzi; verificarne sempre la validità con strumenti ufficiali.
- Il software interagisce con una carta d'identità elettronica reale: un uso errato (ad esempio inserimenti ripetuti di PIN errati) può bloccare il PIN o la carta.
- La verifica del certificato TLS verso i server di marca temporale (TSA) è disattivata (vedere README); la marca temporale è comunque firmata dalla TSA.
- Le credenziali eventualmente configurate (proxy, TSA) sono salvate cifrate con una chiave derivata dalla macchina e non vanno considerate un sistema di custodia sicuro.

## Licenza

Il codice è distribuito con la licenza del progetto originale (vedere il file `LICENSE`). Le modifiche sono rilasciate con la stessa licenza.
