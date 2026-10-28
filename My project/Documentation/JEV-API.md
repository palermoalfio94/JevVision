# Contratto JEV verificato il 28 ottobre 2026

Le fonti di riferimento sono quelle di **TypeSafe**, il produttore di Jev. I numerosi siti con nomi simili trovati in ricerca non sono stati usati come specifica del client.

- [Indice ufficiale](https://docs.typesafe.ai/llms.txt)
- [System One](https://docs.typesafe.ai/api/system-one)
- [OpenAPI ufficiale](https://api.typesafe.ai/openapi.json)
- [Immagini](https://docs.typesafe.ai/images)
- [Modelli, pricing, rate limit](https://docs.typesafe.ai/models)
- [Client SDK](https://docs.typesafe.ai/sdk)

## Endpoint e autenticazione

`POST https://api.typesafe.ai/v1/systemone`, autenticazione `Authorization: Bearer <TYPESAFE_API_KEY>`.
Il progetto usa direttamente HTTP asincrono di Unity: gli SDK ufficiali Python e JavaScript esistono, ma non sono necessari per un player C# desktop.
Modello iniziale fissato: `jev-1.14.0`. L'alias `jev-latest` può cambiare versione; le risposte registrano il modello effettivo.

## Immagini sperimentali

Le immagini sono una funzione sperimentale, da abilitare con **`TypeSafe-Danger-Zone-Images: true`**. Il client invia un'immagine JPEG come asset nella mappa `assets`; `state.camera` contiene il riferimento UUID a quell'asset. Non usa chat completions, `image_url` o un endpoint OpenAI inventato.

```json
{
  "model": "jev-1.14.0",
  "state": {
    "camera": "<uuid>",
    "task": "Drive from the front-camera image only. The dashed yellow line marks the track center and desired driving path. Stay on the road, anticipate bends, avoid collisions, prefer safety."
  },
  "assets": {
    "<uuid>": {"type": "image", "format": "jpg", "data_base64": "<JPEG base64 senza data: prefix>"}
  },
  "questions": {
    "steering": {"type": "choice", "instructions": "...", "criteria": {"strong_left": "...", "left": "...", "slight_left": "...", "straight": "...", "slight_right": "...", "right": "...", "strong_right": "..."}},
    "throttle": {"type": "choice", "instructions": "...", "criteria": {"coast": "...", "slow": "...", "cruise": "..."}},
    "brake": {"type": "choice", "instructions": "...", "criteria": {"release": "...", "apply": "..."}}
  }
}
```

Tre domande nello stesso invio condividono un unico frame. Il compito generale indica esplicitamente che la linea gialla tratteggiata è il centro della pista e la traiettoria desiderata. La domanda sullo sterzo è: `Choose steering to follow the dashed yellow centerline and its upcoming curvature in camera. Use several consecutive dashes and steer smoothly. If markings are unclear, use the road edges.` Nel payload `camera` è racchiuso tra backtick. La domanda sul gas e tutte le etichette di risposta restano originali.

La domanda sul freno chiede esplicitamente di scegliere `apply` se l'auto appare diretta verso una collisione con la barriera blu o azzurra, frenando prima del contatto. Il criterio `apply` comprende anche altri ostacoli, strada persa e prosecuzione pericolosa; `release` richiede assenza di pericolo immediato e di traiettoria di collisione. I comandi e la priorità del freno sul gas restano gli stessi.

`state.vehicle` e le istruzioni sullo stato dell'auto restano eliminati. Nessuna telemetria del veicolo o informazione dell'evaluator entra nella richiesta. Il JSON effettivo viene salvato in `requests/` senza credenziali.

La risposta contiene `answers.<nome>.choice`, `confidence`, `probabilities` e `usage.input_tokens` / `output_tokens`. La confidence mostrata è il minimo delle tre confidence, quando tutte disponibili. Le distribuzioni complete rimangono nella risposta JSON grezza salvata nei log. Il pannello grande in alto a destra usa le scelte originali dell'ultima risposta valida applicabile: risposte obsolete, scartate o precedenti a un reset non vengono presentate come comandi attivi.

La dimensione documentata per ciascun lato è **280–1568 pixel**: il suggerimento iniziale 256×256 viene quindi adattato a **280×280**. PNG/JPEG statici, massimo 16 MiB per file e 32 MiB per richiesta. Base64 è supportato ufficialmente; multipart è raccomandato dal provider per immagini più grandi. Qui i piccoli JPEG rendono il sovraccarico accettabile.

## Scheduling e guasti

Una sola richiesta in volo; target 2 Hz, senza recuperare richieste arretrate. Se la latenza supera 500 ms, la frequenza reale diminuisce. Timeout iniziale 8 s, configurabile. Il comando valido resta applicato entro il limite di età del frame (2 s iniziali), poi il veicolo frena. Una risposta già vecchia viene registrata ma non applicata.

Backoff esponenziale su errori temporanei, con rispetto di `Retry-After` (secondi o data HTTP). Nessun retry immediato dello stesso frame: il tentativo successivo cattura una nuova immagine. Errori 400/401/402/403/404/413/422 sospendono l'API finché si riseleziona la modalità. Reset/cambio modalità invalidano le richieste pendenti; eventuali token riportati sono contabilizzati anche per risposte scartate.

I rate limit ufficiali sono **per organizzazione** e consultabili nella [console](https://console.typesafe.ai/rate-limits); non è documentato un valore universale da codificare. Budget configurato: 600 richieste e $0,05 stimati per sessione. Il limite di costo usa token effettivamente riportati e può superarsi del costo dell'ultima richiesta. Non sostituisce un limite sul conto del provider.

## Prezzi e token

Il payload usa la sola immagine; il riferimento esplicito alla linea gialla aggiunge del testo alle istruzioni. Il consumo effettivo delle nuove sessioni resta misurato dai contatori `usage`.

La pagina ufficiale modelli elenca **$0,042 per milione di input token** per Jev 1.14 e 1.13, output gratuiti. Le immagini sono conteggiate negli input token allo stesso prezzo; il produttore non pubblica ancora una formula affidabile per stimare i token dai pixel. Per questo non si inventa un costo fisso per immagine.

La tariffa, data e fonte sono centralizzate in `ExperimentSettings.asset`. Il costo è:

`(input_tokens × inputUsdPerMillionTokens + output_tokens × outputUsdPerMillionTokens) / 1_000_000`

I tassi mostrati sono medie sulla durata della sessione; il costo per giro JEV divide tutti i costi API della sessione (compresi tentativi falliti/scartati) per i giri JEV completati. In manuale le chiamate, i token e i costi API sono zero; il costo per giro JEV è non disponibile finché non viene completato un giro. Un uso senza token riportati è segnalato come parziale.

**Verifica reale del 28 ottobre 2026:** test curl testuale HTTP 200, seguito da una richiesta Unity con JPEG a veicolo bloccato e 12 richieste Unity durante una guida breve. Le 13 richieste con immagini sono state accettate e interpretate; tutti i comandi automatici applicati provengono da JEV. La guida breve ha percorso circa 4,49 m in 6,81 s con latenza media 374 ms, senza reset. Utilizzo immagini complessivo: 10.779 input token e 1.882 output token; costo stimato $0,000452718 alla tariffa configurata. Questo verifica il ciclo camera/API/comandi, non il completamento di un giro. Report e audit dei payload in `Validation/JevEvidence`.

