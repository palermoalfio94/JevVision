# Circuito misto e coerenza della scena

La forma riprende il contorno inviato dall'utente, con raccordi più regolari: rientro a sinistra, tratto obliquo superiore e parte destra ampia. La B-spline chiusa è lunga circa **482,47 m**, con carreggiata larga **12 m** e curve in entrambe le direzioni. Il disegno originale è conservato in `Circuit-reference.png` con sfondo bianco per rendere visibile il tratto nero trasparente.

La strada e i cordoli sono cinque mesh continue generate da codice e salvate in `Assets/JevVision/Generated`. Le barriere hanno collider reali. Partenza e sedici checkpoint vengono generati dalla stessa curva. I punti della curva sono dati di costruzione e valutazione: nessun driver li legge per scegliere lo sterzo.

## Problema corretto

Dopo un aggiornamento incompleto, `JevVisionCircuit.unity` conteneva ancora l'ovale (campi serializzati `radius` e `straightHalfLength`), mentre gli script caricati usavano già la nuova spline. I log della sessione `20261025T175806165Z_a90512` mostravano un errore iniziale di **24,03 m**, un reset `off_track` dopo circa 4 s e poi un reset `stuck`. Il reset portava l'auto al punto iniziale della nuova curva dentro una scena ancora ovale.

La correzione rigenera insieme strada, barriere, auto e checkpoint con gli script aggiornati. Il bridge di build importa e ricompila gli script modificati prima di usarne i tipi per salvare la scena.

`TrackLayout` salva una firma della geometria generata. Se larghezza, punti o campionamento non corrispondono più, `ExperimentManager` mantiene i comandi in pausa e mostra **“Scena da rigenerare”**, senza avviare una sequenza di reset sulla geometria sbagliata. Per riprendere usare **JEV Vision → Create or rebuild circuit scene** e riavviare Play.

## Verifica

La build Windows viene generata con Unity 6000.3.26f1. I controlli verificano chiusura, curve in entrambi i versi, distanze dalla spline, mesh salvate, checkpoint invisibili e firma coerente. Sono presenti anche casi negativi che modificano larghezza e punti senza rigenerare la scena e richiedono che la firma risulti invalida.

I risultati storici dei giri sulla pista mista sono in `Validation/CircuitEvidence/`; quelli sotto `Validation/Evidence/` descrivono l'ovale iniziale. Entrambi usavano l'algoritmo a colori rimosso dal codice il 28 ottobre 2026. Le prove attuali con il modello JEV sono in `Validation/JevEvidence/`: una richiesta a veicolo fermo e una guida breve, senza rivendicare un giro completo del modello.
