# Stato attuale — sterzo e acceleratore

Il protocollo JEV, il controllo manuale, l'HUD, la configurazione e i nuovi log prevedono due soli comandi: sterzo e gas. Il modello riceve il JPEG frontale e due domande choice, con riferimento alla linea gialla tratteggiata.

In manuale: W/↑ per accelerare, A/D o ←/→ per sterzare. Rilasciando il gas l'auto procede per inerzia con resistenza al rotolamento di 0,18 m/s². P mette in pausa il veicolo; prima del Play interno l'auto resta immobilizzata. Un comando scaduto o mancante rilascia il gas.

Aggiornati i controlli del protocollo per risposte con due campi e i test manuale/setup per il rilascio dell'acceleratore e il rallentamento passivo. Compilazione runtime ed editor riuscita con 0 errori e 0 avvisi. Nessuna build Windows automatica o chiamata API avviata per questa modifica; il player precedente va rigenerato prima di collaudare questa versione fuori dall'editor.

Pista e ambiente conservati: circa 482 m, 120 strisce gialle ogni 4 m, 133 alberi low poly e barriere blu alte 1,5 m.

Le verifiche delle versioni precedenti sono conservate in [HISTORY-2026-10-28.md](HISTORY-2026-10-28.md), [HISTORY-2026-10-25.md](HISTORY-2026-10-25.md) e nelle cartelle delle prove. I report storici non dimostrano il funzionamento del protocollo attuale.
