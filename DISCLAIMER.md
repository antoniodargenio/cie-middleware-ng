# DISCLAIMER

## Natura di questo software

Questo repository è un **fork non ufficiale** del middleware CIE originale (progetto *italia/cie-middleware*), con modifiche alla sicurezza e alle funzionalità. Le modifiche sono state realizzate a **scopo didattico e di studio open source**.

**Questo software NON è la versione ufficiale distribuita dall'Istituto Poligrafico e Zecca dello Stato (IPZS)** e non è approvato, certificato, supportato né collegato in alcun modo all'IPZS, al Ministero dell'Interno o ad altre amministrazioni. Marchi e denominazioni appartengono ai rispettivi titolari.

## Supporto

Questa versione non è ufficialmente supportata dall'IPZS. Essendo un fork pubblico, riceve supporto dagli sviluppatori su GitHub (issue e pull request del repository del progetto).

## Esclusione di responsabilità

Il software è fornito **"così com'è" (as is)**, senza garanzie di alcun tipo, esplicite o implicite, incluse a titolo esemplificativo garanzie di commerciabilità, idoneità a uno scopo particolare, sicurezza, assenza di errori e non violazione di diritti di terzi.

L'utilizzo avviene **a esclusivo rischio e pericolo dell'utente**. Lo sviluppatore non si assume alcuna responsabilità per danni diretti o indiretti derivanti dall'uso, o dall'impossibilità di usare, il software, ivi inclusi, a titolo esemplificativo: perdita di dati, blocco del PIN o della carta, firme digitali non valide o non riconosciute, malfunzionamenti, danni a dispositivi o a documenti.

## Avvertenze d'uso

- Per usi ufficiali, professionali o aventi valore legale utilizzare esclusivamente il software ufficiale fornito dall'IPZS.
- Le firme prodotte con questo software potrebbero non essere accettate da terzi; verificarne sempre la validità con strumenti ufficiali.
- Il software interagisce con una carta d'identità elettronica reale: un uso errato (ad esempio inserimenti ripetuti di PIN errati) può bloccare il PIN o la carta.
- Il certificato TLS dei server di marca temporale (TSA) è verificato con l'archivio certificati di Windows; le credenziali TSA non vengono mai inviate su connessioni `http://` non cifrate.
- Le credenziali eventualmente configurate (proxy, TSA) sono salvate cifrate con una chiave derivata dalla macchina e non vanno considerate un sistema di custodia sicuro.

## Licenza

Il codice è distribuito con la licenza **BSD 3-Clause** del progetto originale (vedere il file `LICENSE`), il cui testo è riportato di seguito. Le modifiche di questo fork sono rilasciate con la stessa licenza.

> BSD 3-Clause License
>
> Copyright (c) 2017, Istituto Poligrafico e Zecca dello Stato
> All rights reserved.
>
> Redistribution and use in source and binary forms, with or without
> modification, are permitted provided that the following conditions are met:
>
> * Redistributions of source code must retain the above copyright notice, this
>   list of conditions and the following disclaimer.
>
> * Redistributions in binary form must reproduce the above copyright notice,
>   this list of conditions and the following disclaimer in the documentation
>   and/or other materials provided with the distribution.
>
> * Neither the name of the copyright holder nor the names of its
>   contributors may be used to endorse or promote products derived from
>   this software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
> AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
> IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
> DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
> FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
> DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
> SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
> CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
> OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
> OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Come previsto dalla licenza, il nome dei titolari del copyright (Istituto Poligrafico e Zecca dello Stato) e dei contributori originali non è usato per promuovere questo fork, che non è approvato da loro.
