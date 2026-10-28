# JEV Vision Lab

Progetto Unity **6000.3.26f1**, nella cartella **My project**.

- Scena: `My project/Assets/Scenes/JevVisionCircuit.unity`.
- Due modalità: **Manuale** e **JEV Vision**, unica guida autonoma.
- JEV riceve solo la camera frontale, con indicazione di seguire la linea gialla tratteggiata; le decisioni sono mostrate in grande in alto a destra.
- Il precedente algoritmo basato sui colori è stato eliminato dal codice.
- Chiave: `TYPESAFE_API_KEY`, configurabile con `My project/scripts/set-jev-key.ps1`.
- [Istruzioni complete](My%20project/README.md).
- [Risultati e limiti delle verifiche](My%20project/Validation/RESULTS.md).
- [Forma del circuito misto](My%20project/Validation/CircuitEvidence/circuit-plan.png).

All'avvio appare una schermata per scegliere Manuale/JEV, velocità massima e frequenza delle chiamate JEV. L'auto è bloccata e nessuna richiesta API parte prima del pulsante **PLAY · AVVIA GUIDA** dentro il gioco. Durante la guida il tasto 1 seleziona il manuale, 2 (anche 3) seleziona JEV. Nessuna chiave è contenuta nel repository.
