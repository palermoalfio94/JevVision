# Contratto JEV verificato il 25 ottobre 2026

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
    "task": "Drive from the front-camera image only. Stay on the road, anticipate bends, avoid collisions, prefer safety."
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

Tre domande nello stesso invio condividono un unico frame; la risposta contiene `answers.<nome>.choice`, `confidence`, `probabilities` e `usage.input_tokens` / `output_tokens`. La confidence mostrata è il minimo delle tre confidence, quando tutte disponibili. Le distribuzioni complete rimangono nella risposta JSON grezza salvata nei log.

La dimensione documentata per ciascun lato è **280–1568 pixel**: il suggerimento iniziale 256×256 viene quindi adattato a **280×280**. PNG/JPEG statici, massimo 16 MiB per file e 32 MiB per richiesta. Base64 è supportato ufficialmente; multipart è raccomandato dal provider per immagini più grandi. Qui i piccoli JPEG rendono il sovraccarico accettabile.

## Scheduling e guasti

Una sola richiesta in volo; target 2 Hz, senza recuperare richieste arretrate. Se la latenza supera 500 ms, la frequenza reale diminuisce. Timeout iniziale 8 s, configurabile. Il comando valido resta applicato entro il limite di età del frame (2 s iniziali), poi il veicolo frena. Una risposta già vecchia viene registrata ma non applicata.

Backoff esponenziale su errori temporanei, con rispetto di `Retry-After` (secondi o data HTTP). Nessun retry immediato dello stesso frame: il tentativo successivo cattura una nuova immagine. Errori 400/401/402/403/404/413/422 sospendono l'API finché si riseleziona la modalità. Reset/cambio modalità invalidano le richieste pendenti; eventuali token riportati sono contabilizzati anche per risposte scartate.

I rate limit ufficiali sono **per organizzazione** e consultabili nella [console](https://console.typesafe.ai/rate-limits); non è documentato un valore universale da codificare. Budget locale: 600 richieste e $0,05 stimati per sessione, così la pista mista più lunga non esaurisce il budget di chiamate durante un giro a 2 Hz. Il limite di costo usa token effettivamente riportati e può superarsi del costo dell'ultima richiesta. Non sostituisce un limite sul conto del provider.

## Prezzi e token

La pagina ufficiale modelli elenca **$0,042 per milione di input token** per Jev 1.14 e 1.13, output gratuiti. Le immagini sono conteggiate negli input token allo stesso prezzo; il produttore non pubblica ancora una formula affidabile per stimare i token dai pixel. Per questo non si inventa un costo fisso per immagine.

La tariffa, data e fonte sono centralizzate in `ExperimentSettings.asset`. Il costo è:

`(input_tokens × inputUsdPerMillionTokens + output_tokens × outputUsdPerMillionTokens) / 1_000_000`

I tassi mostrati sono medie sulla durata della sessione; il costo per giro JEV divide tutti i costi API della sessione (compresi tentativi falliti/scartati) per i giri JEV completati. In locale le chiamate, i token e i costi API sono zero; il costo per giro JEV è non disponibile. Un uso senza token riportati è segnalato come parziale.

**Stato delle verifiche:** il contratto è stato letto; le risposte del parser sono testate con fixture. Senza credenziali non è stata eseguita una richiesta a JEV e non si rivendicano né capacità di guida di JEV né latenze o token misurati del provider.

