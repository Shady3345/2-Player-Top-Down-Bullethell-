Technologie: Unity 6000.0.62f1 + FishNet | Arbeitsform: Einzelarbeit oder 2er-Team

Kurzbeschreibung des Spiels

In diesem wellenbasierten Co-op Top-Down-Shooter kämpfen zwei Spieler gemeinsam gegen Wellen von Gegnern. Das Ziel ist es, so lange wie möglich zu überleben und dabei einen möglichst hohen Score zu erreichen. Mit jeder Welle steigt die Anzahl der Gegner, was die Herausforderung kontinuierlich erhöht. Beide Spieler müssen zusammenarbeiten, da das Spiel endet, sobald alle Spieler besiegt wurden.

Anlteitung zum Starten von Host und Client

  - Voraussetzungen

    - Unity 6000.0.62f1 + FishNet Networking installiert

  - Host
    - Windows Defender Firewall: Neue eingehende Regel ->  Typ: Port Protokoll: UDP Port: (siehe Tugboat) -> Verbindung zulassen -> Häkchen bei allen Profilen setzen ODER Firewall deaktivieren
    - Server starten
    - Client starten

  - Client
    - Tugboat: IP-Adresse des Hosts eingeben
    - Client starten

Technischer Überblick

- Verwendete RPCs

  Server RPCs
  - SetReadyServerRpc
  - RotateServerRpc
  - MoveServerRpc
  - ShootServerRpc
  - BurstShootServerRpc
  - RequestRestartServerRpc
  - RequestReturnToLobbyServerRpc

  Observer RPCs
  - RpcReturnToLobby
  - RpcOnGameStart
  - RpcOnGameEnd
  - RpcAnnounceWave
  - RpcAnnounceWaveComplete
  - RpcDisablePlayer
  - RpcRespawn

 - Verwendete SyncVars

   NetworkGameManager
   - Player1Name / Player2Name
   - player1Health / player2Health
   - totalScore
   - currentWave
   - enemiesKilled
   - gameState

   PlayerStats
   - currentHealth
   - isInvincible
   - isReady
   - playerName
   - playerIndex

    WaveSpawn
    - currentWave
    - enemiesAlive
    - waveInProgress 

  - Bullet-Logik

    PlayerBullet
    - Automatische Zerstörung nach 5 Sekunden Lebensdauer
    - Fügt Gegnern 1 Schadenspunkt zu
    - Wird bei Treffer oder außerhalb des Bildschirms despawnt

    EnemyBullet
    - Automatische Zerstörung nach 5 Sekunden Lebensdauer
    - Fügt Spielern 1 Schadenspunkt zu
    - Wird bei Treffer oder außerhalb des Bildschirms despawnt

  - Gegner-Logik

    Gegner suchen automatisch den nächstgelegenen lebenden Spieler -> Neuberechnung des Ziels alle 2 Sekunden -> Zielwechsel bei Tod des verfolgten Spielers

Persistenz

(Coming soon)

Bonusfeatures

- Lobby-System 

Bekannte Bugs
  - Player-Namen werden nach erneutem Eintragen nach der Rückkehr zur Lobby nicht angezeigt.
