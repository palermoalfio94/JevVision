# JEV API — sterzo e acceleratore

Il client invia un JPEG frontale e due domande choice: steering e throttle.
Modello: jev-1.14.0. Endpoint: POST https://api.typesafe.ai/v1/systemone.
Autenticazione: chiave TYPESAFE_API_KEY nell'header Authorization; immagini abilitate con TypeSafe-Danger-Zone-Images: true.

## Input e decisioni

Lo state contiene soltanto camera e task. Il compito generale indica la linea gialla tratteggiata come centro della pista e traiettoria da seguire. La domanda sullo sterzo chiede di considerare più tratteggi consecutivi, anticipare le curve, sterzare dolcemente e usare i bordi quando le strisce non sono chiare.

Le sette opzioni di steering sono strong_left, left, slight_left, straight, slight_right, right, strong_right.
Le tre opzioni di throttle sono coast, slow, cruise: gas 0, 0,35 e 0,7, poi moltiplicato per throttleScale.

La risposta deve contenere answers.steering e answers.throttle. La confidence mostrata è il minimo delle due confidence, oppure -1 se incompleta. I nuovi comandi e i nuovi log contengono solo sterzo e gas.

L'unico input dinamico è il JPEG 280×280. Nessuno stato del veicolo, posizione nel circuito, geometria o checkpoint viene aggiunto. L'HUD resta escluso dalla camera del modello.

## Comportamento

Rilasciando il gas l'auto procede per inerzia, con resistenza al rotolamento di 0,18 m/s². In manuale W/↑ accelera e A/D o ←/→ sterzano. P mette in pausa il veicolo; prima del pulsante Play interno il Rigidbody resta immobilizzato.

Una sola richiesta in volo, frequenza richiesta configurabile da 0,2 a 10 call/s. La latenza può ridurre la frequenza reale. Alla scadenza del comando viene rilasciato il gas. Pausa, reset e cambio modalità invalidano le risposte pendenti. Errori temporanei applicano backoff e Retry-After; quelli permanenti sospendono l'API fino alla riselezione della modalità.

Richieste effettive, risposte, immagini, token ed esiti vengono salvati nei log della sessione senza credenziali. I report archiviati rappresentano protocolli precedenti.

## Fonti e costi

- [System One](https://docs.typesafe.ai/api/system-one)
- [Immagini](https://docs.typesafe.ai/images)
- [Modelli e prezzi](https://docs.typesafe.ai/models)

Tariffa configurata, verificata il 28 ottobre 2026: $0,042 per milione di input token, output gratuiti. Il costo della versione con due domande deve essere misurato dai nuovi contatori usage. Budget per sessione: 600 richieste e $0,05 stimati.
