# Verifiche JEV Vision — 28 ottobre 2026

Il precedente algoritmo di guida basato sul colore è stato **eliminato dal codice e dal player**. Restano esclusivamente Manuale e JEV Vision. I risultati precedenti sono conservati in [HISTORY-2026-10-25.md](HISTORY-2026-10-25.md) e non sono prove di guida JEV.

## Build e controlli

- Unity 6000.3.26f1, player Windows: build riuscita, **0 errori e 0 avvisi**.
- **187 controlli superati** su contratto, parsing, due sole modalità, migrazione del valore serializzato rimosso, geometria e checkpoint.
- File `LocalVisionDriver.cs` e relativo `.meta` assenti; il nome della classe è assente anche dai metadati del player compilato.

## Prove reali

| Prova | Risultato |
|---|---|
| curl testuale | HTTP 200, scelta stop, 331 token input e 32 output; nessuna immagine |
| Immagine Unity, veicolo bloccato | PASS, una chiamata, risposta applicabile, 744 ms, 827 input token e 144 output |
| Guida JEV breve | PASS, 12 chiamate e 12 risposte applicate, 0 errori e 0 reset |
| Fisica/manuale | PASS, 1,2 m/s, sterzo misurato 21,27 gradi, frenata fino a zero |
| Chiave assente simulata | PASS, zero richieste e frenata; la variabile reale dell'utente resta invariata |
| Reset | PASS, uscita pista, ribaltamento e blocco indotti nel test senza API |

Nella guida JEV l'auto ha percorso **4,49 m** in **6,81 s**, a massimo **1 m/s**. La latenza media è stata **374 ms**, errore RMS dalla linea centrale **0,126 m**, massimo **0,339 m**. Il test termina al budget di 12 chiamate: **non è un giro completo e non prova ancora l'affidabilità nelle curve del circuito**.

Le 13 chiamate Unity con immagini hanno consumato 10.779 token input e 1.882 output. Alla tariffa configurata di $0,042/M input e output gratuiti, il costo stimato complessivo è **$0,000452718**. È una stima dai token riportati dal provider, non una lettura della fattura.

## Audit visual-only

L'audit delle 13 richieste ha verificato:

- `state` contiene solo `camera` e un'istruzione statica `task`.
- Le domande sono sterzo, acceleratore e freno; nessuna posizione, velocità, distanza o geometria viene aggiunta al payload.
- I byte JPEG decodificati dall'asset inviato coincidono con i 13 frame salvati.
- Tutti i comandi automatici applicati hanno sorgente `jev`, risposta HTTP 200 ed esito `applied`.
- I checkpoint e le distanze rimangono nel solo evaluator e non generano comandi di guida.

Report, CSV, richieste, risposte, immagini e screenshot sono raccolti in [JevEvidence](JevEvidence/); [final-summary.json](final-summary.json) riepiloga le cinque prove del player e [payload-audit.json](JevEvidence/payload-audit.json) documenta i controlli sui payload. Nessun header Authorization o valore della chiave viene salvato.

Il prossimo collaudo funzionale è un percorso più lungo con JEV e un budget esplicito; il completamento del giro resta da misurare.
