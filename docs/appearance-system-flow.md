# Appearance System — Client/Server Flow

## Architecture Overview

```mermaid
graph TB
    subgraph "Client Mod (C# / Unity)"
        Plugin[Plugin.OnEnable] --> Init[AppearanceAPI.Initialize]
        Init --> ReqTicket[Request Steam Ticket]
        Init --> FetchLocal[Fetch Local Appearance]
        Init --> FetchUnlocks[Fetch Unlocks]
        Init --> StartHB[Start Heartbeat Loop]

        subgraph "Appearance Panel UI"
            XPBar[XP / Level Bar]
            HatDrop[Hat Dropdown — filtered by unlocks]
            BodyType[Body Type / Skin / Hair]
        end

        subgraph "Extras Panel UI"
            CodeEntry[Code Entry Box]
            CodeEntry -->|local codes| LocalCodes[BIGHEADS → session toggle]
            CodeEntry -->|server codes| RedeemAPI[POST /redeem]
        end

        subgraph "In-Game Tracking"
            InputPatch[Harmony: PlayerInput.Update] --> TrackTime
            InputPatch --> TrackInput
            TrackTime[Accumulate game/warmup/menu seconds]
            TrackInput[Count movement + stick inputs]
        end

        SpawnPatch[Harmony: PlayerBody.ApplyCustomizations] --> OnSpawn[OnPlayerSpawned]
        LeavePatch[Harmony: PlayerManager.RemovePlayer] --> OnLeave[OnPlayerLeft]
    end

    subgraph "Server (Next.js API + PostgreSQL + Redis)"
        subgraph "Endpoints"
            GET_App[GET /api/appearance]
            POST_App[POST /api/appearance]
            GET_Unlocks[GET /api/appearance/unlocks]
            POST_XP[POST /api/appearance/xp]
            POST_Redeem[POST /api/appearance/redeem]
        end

        subgraph "Data"
            DB_App[(player_appearance)]
            DB_XP[(appearance_player_xp)]
            DB_Hats[(appearance_hats)]
            DB_Unlocks[(appearance_hat_unlocks)]
            DB_HB[(appearance_heartbeats)]
            Redis[(Redis Cache)]
        end
    end

    FetchLocal -->|GET steamIds=self| GET_App
    FetchUnlocks -->|GET steamId=self| GET_Unlocks
    OnSpawn -->|GET steamIds=new player| GET_App
    HatDrop -->|POST appearance| POST_App
    BodyType -->|POST appearance| POST_App
    StartHB -->|every 5 min| POST_XP
    RedeemAPI --> POST_Redeem

    GET_App --> Redis
    GET_App --> DB_App
    POST_App --> DB_App
    POST_App --> Redis
    POST_App -.->|validates hat ownership| DB_Unlocks
    GET_Unlocks --> DB_XP
    GET_Unlocks --> DB_Unlocks
    GET_Unlocks -.->|first call: grants starter hat| DB_Unlocks
    POST_XP --> DB_XP
    POST_XP --> DB_HB
    POST_XP -.->|on level up: grants random hat| DB_Unlocks
    POST_Redeem --> DB_Hats
    POST_Redeem --> DB_Unlocks
```

## Startup Sequence

```mermaid
sequenceDiagram
    participant Mod as Client Mod
    participant Steam as Steam API
    participant Server as puckstats Server

    Mod->>Steam: GetAuthTicketForWebApi("puckstats")
    Steam-->>Mod: Ticket (cached for session)

    par Fetch appearance + unlocks
        Mod->>Server: GET /api/appearance?steamIds=<self>
        Server-->>Mod: { steamId: { body_type, skin_tone, ... } }
        Note over Mod: Update locker room preview
    and
        Mod->>Server: GET /api/appearance/unlocks?steamId=<self>
        Server-->>Mod: { xp, level, xp_to_next_level, unlocked_hats }
        Note over Mod: Populate XP bar + filter hat dropdown
    end

    Note over Mod: Start 5-min heartbeat loop
```

## Heartbeat / XP Flow

```mermaid
sequenceDiagram
    participant Game as Game (Harmony Patches)
    participant API as AppearanceAPI
    participant Server as puckstats Server

    loop Every frame (PlayerInput.Update postfix)
        Game->>API: TrackTime(dt, inGame, inWarmup)
        Game->>API: TrackInput() if movement detected
    end

    loop Every 5 minutes
        API->>Server: POST /api/appearance/xp
        Note right of API: { ticket, in_game_seconds,<br/>in_warmup_seconds, in_menu_seconds,<br/>input_count }
        Server-->>API: { xp, xp_earned, level,<br/>xp_to_next_level, leveled_up, new_hats }

        alt new_hats not empty
            API->>API: Add to UnlockedHatIds
            API->>API: Show toast ("Level X!" or "New Hat!")
        end

        API->>API: Fire OnUnlocksChanged
        Note over API: UI rebuilds XP bar + hat dropdown
    end
```

## XP Formula

| Level | XP to next | Cumulative |
|-------|-----------|------------|
| 1→2   | 100       | 100        |
| 2→3   | 150       | 250        |
| 3→4   | 200       | 450        |
| 4→5   | 250       | 700        |
| N→N+1 | 50 + N×50 | —          |

- **In-game:** 0.1 XP/sec (30 XP per 5-min heartbeat)
- **Warmup:** 0.05 XP/sec (15 XP per 5-min heartbeat)
- **Menu:** 0 XP/sec
- **AFK:** 0 XP if < 10 inputs per heartbeat
- **Diminishing returns:** Full XP for first hour, then 75% → 50% → 25% → 10%

## Player Spawn / Appearance Application

```mermaid
sequenceDiagram
    participant Body as PlayerBody.ApplyCustomizations
    participant API as AppearanceAPI
    participant Server as puckstats Server
    participant Swap as Swappers (Hat, Gender, Skin)

    Body->>API: OnPlayerSpawned(player)

    alt Appearance cached
        API->>Swap: ApplyAppearanceToPlayer(player, cached)
    else Not cached
        API->>Server: GET /api/appearance?steamIds=<player>
        Server-->>API: Appearance data
        API->>API: Cache result
        API->>Swap: ApplyAppearanceToPlayer(player, data)
    end

    Note over Swap: Apply body type (GenderSwapper)<br/>Apply skin tone + hair color<br/>Attach hat (if ShowOtherPlayersHats)
```

## Code Redemption Flow

```mermaid
sequenceDiagram
    participant UI as Extras Section
    participant API as AppearanceAPI
    participant Server as puckstats Server

    UI->>UI: User enters code

    alt Local code (BIGHEADS, UNLOCKALLHATS)
        UI->>UI: Handle client-side (session only)
    else Server code
        UI->>API: RedeemCode(code, callback)
        API->>Server: POST /api/appearance/redeem { ticket, code }

        alt 200 OK
            Server-->>API: { success, hat_id, name }
            API->>API: Add to UnlockedHatIds
            API-->>UI: "Unlocked: Party Hat!"
        else 400
            Server-->>API: { error: "invalid_code" }
            API-->>UI: "Invalid code."
        else 409
            Server-->>API: { error: "already_unlocked" }
            API-->>UI: "Already unlocked."
        end
    end
```

## Display Settings (Client-Only)

These settings affect how **other players'** appearances render locally:

| Setting | Effect |
|---------|--------|
| Show Personalization | Master toggle — off = all players reset to defaults |
| Show Other Players' Hats | Hide/show hats on other players |
| Show Non-Natural Skin Tones | Replace exotic skin tones with a consistent random natural tone |
