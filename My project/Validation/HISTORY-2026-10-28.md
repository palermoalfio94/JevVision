# Verifiche JEV Vision — 28 ottobre 2026

## Barriere blu e più alte

Aggiornate direttamente tutte le **1.208 barriere** nella scena salvata: altezza **1,5 m** (da 0,9 m, +66,7%), base mantenuta a terra e materiale condiviso `Rail` blu RGB (0,02; 0,22; 0,95). Aggiornati anche il generatore e il materiale per mantenere queste impostazioni nelle rigenerazioni future. Verificati sul file salvato il numero di barriere, tutte le altezze e l'uso del materiale corretto. Compilazione runtime/editor passata con 0 errori e 0 avvisi. Nessuna nuova build Windows automatica, prova di guida o chiamata API avviata per questa modifica.

## Prompt freno: barriera blu

La domanda del freno e i criteri `apply`/`release` ora indicano esplicitamente di frenare prima del contatto se l'immagine suggerisce una traiettoria di collisione con la barriera blu/azzurra o un altro ostacolo. Restano i criteri strada persa e prosecuzione pericolosa, le due scelte originali e l'annullamento del gas quando il freno è applicato. Compilazione runtime passata con 0 errori e 0 avvisi. Nessuna chiamata API o prova di guida avviata per misurare il comportamento del nuovo prompt.

## Prompt: riferimento esplicito alla linea gialla

Modificati solo il compito generale e la domanda sullo sterzo: linea gialla tratteggiata come centro della pista e traiettoria desiderata; lettura di più tratteggi consecutivi, anticipo della curvatura, sterzo dolce e uso dei bordi quando le strisce sono poco chiare. Gas, freno e opzioni di risposta invariati; input dinamico sempre limitato al JPEG. Compilazione runtime/editor passata con 0 errori e 0 avvisi. Nessuna chiamata API o prova di guida avviata per confrontare questo prompt con il precedente.

## Strisce gialle centrali più frequenti

Intervallo ridotto da 8 m a **4 m**, mantenendo lunghezza di 1,6 m e larghezza di 0,12 m: la scena salvata contiene ora **120 strisce** invece di 60. Aggiunto l'aggiornamento delle vecchie scene all'avvio; alberi e protocollo JEV invariati. Compilazione runtime superata con 0 errori e 0 avvisi. Nessuna prova API o di guida eseguita per questa modifica visiva.

## Stato attuale: ripristinato l'input con sola immagine

Su richiesta dell'utente sono stati rimossi `state.vehicle`, la raccolta di dati per il payload e tutte le istruzioni relative allo stato dell'auto. Ripristinati esattamente task e domande originali. Alberi, pannello grande delle decisioni e configurazione iniziale sono mantenuti; HUD e log conservano la telemetria locale. Compilazione degli script runtime ed editor superata senza errori né avvisi, senza chiamate API o nuove prove di guida. Le sezioni seguenti documentano gli interventi precedenti, inclusa la versione con telemetria poi ritirata.

## Alberi low poly fuori strada

Generati **133 alberi** all'avvio, oltre le barriere, con chiome a sei lati, tronchi semplici e variazioni deterministiche di colore e dimensione. Il posizionamento verifica la distanza dall'intero circuito e mantiene le chiome fuori strada. Nessun collider aggiunto; invio di stato, istruzioni e comandi JEV invariati rispetto alla versione con telemetria.

Build Windows riuscita con **0 errori e 0 avvisi**. Breve prova manuale **PASS**, 0 reset e **0 chiamate API**; controllata visivamente la presenza degli alberi nella camera frontale e nella vista osservatore. Immagini e report in [TreesEvidence](TreesEvidence/). Nessuna prova dell'effetto degli alberi sulla guida JEV è stata eseguita.

## Aggiornamento: immagine e telemetria, decisioni grandi nell'HUD

Il client ora invia nello stesso frame camera e stato dell'auto: velocità, sterzo applicato, rotazione, gas/freno, limiti e intervallo richiesto tra decisioni. Le tre istruzioni considerano questi dati. Il pannello in alto a destra mostra le scelte JEV originali e distingue decisioni attive, attesa, pausa e frenata di sicurezza. La preview è sotto il pannello.

Gli script runtime ed editor sono stati compilati con `dotnet build --no-restore`: **0 errori, 0 avvisi** per entrambi. Unity ha inoltre eseguito **188 controlli superati**, inclusi il nuovo payload con telemetria, le unità e la convenzione dei segni. Nessuna chiamata API o prova di guida è stata eseguita per questo aggiornamento. I report di guida riportati di seguito si riferiscono alla precedente versione con solo immagini e non certificano l'efficacia della telemetria ad alta velocità.

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
