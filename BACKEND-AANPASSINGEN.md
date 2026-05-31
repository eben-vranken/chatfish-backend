# Backend-aanpassingen

Dit document houdt bij welke aanpassingen we aan de **geleverde backend**
(`chatfish-backend`, oorspronkelijk geschreven door de lectoren) hebben moeten
doen om de gevraagde **frontend**-userstories te kunnen realiseren.

> **Waarom dit document?**
> Dit is een frontend-vak. De backend werd aangeleverd. Toch bleken bepaalde
> server-side bouwstenen (datamodel, persistente state, autorisatie,
> bestandslevering) te ontbreken om de gevraagde frontend-functionaliteit te
> kunnen bouwen. Elke zo'n aanpassing wordt hier feitelijk gedocumenteerd:
> wat ontbrak, wat we toevoegden, en waarom dat server-werk is en geen
> presentatielogica.

## Overzicht

| Commit | Datum | Frontend-US | Aanpassing |
|---|---|---|---|
| `5290aa0` | 2026-05-29 | US-12 (detailpagina), US-21 (countdown) | Verkoopdetails op `Scenario` |
| `d82585b` | 2026-05-29 | US-21 (wachtruimte-toegang) | Ticket-model + endpoints |
| `237bece` | 2026-05-31 | US-25 (media in theaterchat) | Bijlagen streamen via API |
| `36b41c8` | 2026-05-31 | US-24 (tijdlijn testen) | Scenario met verleden startdatum in seed |
| *(commit hash)* | 2026-05-31 | US-29 (bericht verwijderen) | Soft-delete endpoint voor posts |
| *(commit hash)* | 2026-05-31 | US-30 (bericht verbergen) | `IsHidden`-veld + hide-endpoint + filtering per rol |
| *(commit hash)* | 2026-05-31 | US-31 (moderator waarschuwing) | `Warning`-model + endpoint + per-user WebSocket + push-notificatie |
| *(commit hash)* | 2026-05-31 | US-32 (moderator time-out) | `UserTimeout`-model + endpoints + afdwinging in PostController |

---

## 1. `feat(scenario): voeg verkoopdetails toe aan Scenario`

- **Commit:** `5290aa0` — 2026-05-29
- **Nodig voor:** US-12 (detailpagina: prijs + verkoopstatus), US-21 (wachtruimte-countdown via `startMoment`)
- **Wat ontbrak:** Het `Scenario`-model en de bijhorende API leverden geen
  verkoopgegevens (verkoopstatus, prijs, startmoment).
- **Wat we toevoegden:** Deze velden aan het datamodel + de API-respons.
- **Waarom backend-domein:** De frontend kan prijs/verkoopstatus/startmoment
  niet verzinnen; die moeten uit het datamodel en de API komen.

## 2. `feat(ticket): ticket-model + endpoints voor wachtruimte-toegang`

- **Commit:** `d82585b` — 2026-05-29
- **Nodig voor:** US-21 (wachtruimte enkel toegankelijk na aankoop — `ticketGuard`)
- **Wat ontbrak:** Er was geen ticket-concept: geen model, geen endpoints om een
  ticket te kopen of te controleren of een gebruiker er een heeft.
- **Wat we toevoegden:** Een ticket-model + endpoints (kopen / bezit controleren).
- **Waarom backend-domein:** Tickets kopen en bezit verifiëren is persistente,
  server-side state met autorisatie. De frontend-guard kan enkel een
  serverantwoord raadplegen, niet zelf bijhouden wie wat bezit.

## 3. `feat(storymessage): stream bijlagen via API met juiste content-type`

- **Commit:** `237bece` — 2026-05-31
- **Nodig voor:** US-25 (tekst/foto/video tonen in de theaterchat)
- **Wat ontbrak:** Bijlagen werden enkel als **presigned MinIO-URL** teruggegeven,
  wijzend naar de interne Docker-host `chatfish-minio:9000` — die een browser
  niet kan bereiken. Bovendien was er geen content-type, dus kon de frontend
  foto niet van video onderscheiden.
- **Wat we toevoegden:**
  - `GET /api/StoryMessage/{id}/file` dat het bestand uit MinIO streamt met het
    juiste `Content-Type` en range-ondersteuning (voor video-scrubbing), via een
    browser-bereikbare URL (de API zelf, met cookie-auth).
  - `FileContentType` op de berichtrespons zodat de frontend het juiste
    media-element kan kiezen.
- **Waarom backend-domein:** Een browser kan de interne objectstore niet
  bereiken; het bestand bereikbaar maken én het type bekendmaken is server-werk.
  Media *tonen* is frontend, media *leveren* is backend.

## 4. `feat(seed): voeg scenario met verleden startdatum toe voor tijdlijn-tests`

- **Commit:** *(nog te committen — vul hash in na `git commit`)* — 2026-05-31
- **Nodig voor:** US-24 (theatercontent afspelen volgens tijdlijn)
- **Wat ontbrak:** De twee geleverde scenario's hebben beide een `StartMoment` ver
  in de toekomst (juni / juli 2026). Functionaliteit die een **reeds gestart**
  toneelstuk vereist — tijdlijn-weergave, automatische navigatie bij start,
  `Sent: true` story messages — kon daardoor niet getest worden.
- **Wat we toevoegden:** Een derde scenario *"De Nacht van de Waarheid"*
  (`StartMoment: 2026-05-20T20:00:00Z`, 11 dagen geleden) met een volledige
  dataset: 4 characters, 3 chats, 100 story messages (alle `Sent: true`),
  2 foyer-channels en seed-posts. Toegevoegd aan `seed/seed-mongodb.js` zodat
  elke developer het met het bestaande seed-commando beschikbaar krijgt.
- **Waarom backend-domein:** Testdata voor een tijdlijn-feature is
  server-side state (MongoDB-documenten met correcte `PlannedAt`/`SentAt`
  timestamps). Dit kan niet vanuit de frontend worden aangemaakt of gesimuleerd.

## 5. `feat(post): soft-delete endpoint voor foyer-berichten`

- **Commit:** *(vul hash in na `git commit`)* — 2026-05-31
- **Nodig voor:** US-29 (bericht verwijderen door auteur)
- **Wat ontbrak:** `PostService` had al een `Archive`-methode die `IsArchived = true` zet,
  maar er was geen HTTP-endpoint om die te bereiken. De `DELETE`-endpoint doet een harde
  verwijdering en biedt geen herstelpad.
- **Wat we toevoegden:** `PATCH /api/Post/{id}/archive` — controleert of de aanvrager auteur
  of admin is (via het bestaande `IsDeletable`-vlag), archiveert dan het bericht (soft-delete).
  Het bericht blijft in de database zodat een admin het later kan herstellen (Epic 6).
- **Waarom backend-domein:** Persistente `IsArchived`-state bijhouden en autorisatie
  controleren is server-werk. De frontend kan niet zelf beslissen of iemand een bericht
  mag archiveren.

## 6. `feat(post): verberg-functie voor moderators`

- **Commit:** *(vul hash in na `git commit`)* — 2026-05-31
- **Nodig voor:** US-30 (moderator verbergt schadelijk bericht)
- **Wat ontbrak:** Het `Post`-model had geen manier om een bericht moderator-specifiek te verbergen,
  los van de auteur-gerichte soft-delete (`IsArchived`). Er was ook geen endpoint voor moderators
  om berichten te verbergen, en `GetByChannelId` filtreerde niet op rol.
- **Wat we toevoegden:**
  - `IsHidden`, `HiddenById`, `HiddenAt`, `HiddenReason` op het `Post`-model (auditlog ingebakken).
  - `PATCH /api/Post/{id}/hide` — toegankelijk voor moderator of admin, met optionele reden.
  - `GetByChannelId` filtert verborgen berichten weg voor niet-moderators/admins.
  - `PostResponse` bevat `IsHidden`, `HiddenAt`, `HiddenReason` en `IsHideable` (computed per rol).
- **Waarom backend-domein:** Rol-gebaseerde zichtbaarheidsfiltering en persistente auditgegevens
  (wie verborg, wanneer, waarom) zijn server-side verantwoordelijkheden. De frontend kan niet zelf
  beslissen welke berichten zichtbaar zijn — dat is precies wat autorisatie op de server regelt.

## 7. `feat(warning): waarschuwingssysteem voor moderators`

- **Commit:** *(vul hash in na `git commit`)* — 2026-05-31
- **Nodig voor:** US-31 (moderator stuurt waarschuwing naar gebruiker)
- **Wat ontbrak:** Er was geen concept van waarschuwingen: geen model, geen endpoint om een
  waarschuwing op te slaan of op te halen, en geen manier om een specifieke gebruiker real-time
  te bereiken via WebSocket (de bestaande `WebSocketManager` broadcastte alleen naar iedereen).
- **Wat we toevoegden:**
  - `Warning`-model (`WarningId`, `TargetUserId`, `IssuedById`, `Reason`, `IssuedAt`) opgeslagen
    in een eigen MongoDB-collectie (`Warnings`).
  - `POST /api/Warning` — alleen toegankelijk voor moderator/admin; slaat de waarschuwing op,
    stuurt een real-time WebSocket-bericht naar de doelgebruiker en een browser-push-notificatie
    als die gebruiker een actief push-abonnement heeft.
  - `GET /api/Warning/my` — geeft alle waarschuwingen terug gericht aan de ingelogde gebruiker;
    gebruikt door de frontend om gemiste waarschuwingen te tonen na een herverbinding.
  - `GET /api/Warning/ws` — WebSocket-eindpunt waarop de frontend zich verbindt na inloggen;
    bewaard per gebruiker zodat berichten gericht kunnen worden afgeleverd.
  - `WebSocketManager` uitgebreid met een per-gebruiker woordenboek naast de bestaande
    broadcast-lijst, zodat bestaande story-message-functionaliteit ongewijzigd blijft.
  - `PushNotificationService` uitgebreid met `SendToUserAsync` om gericht (op userId) een
    browser-push te sturen in plaats van naar alle abonnees.
- **Waarom backend-domein:** Persistente logging van waarschuwingen, autorisatiecontrole
  (alleen moderator/admin), en real-time aflevering aan een specifieke gebruiker (per-user
  WebSocket-targeting en browser-push) zijn volledig server-side verantwoordelijkheden.
  De frontend kan niet zelf beslissen wie een waarschuwing mag sturen of wie hem ontvangt.

## 8. `feat(timeout): time-out systeem voor moderators`

- **Commit:** *(vul hash in na `git commit`)* — 2026-05-31
- **Nodig voor:** US-32 (moderator geeft gebruiker een time-out)
- **Wat ontbrak:** Er was geen concept van tijdelijke posteerverboden: geen model, geen
  endpoint om een time-out op te leggen, en geen afdwinging in de post-endpoint zodat
  een gebruiker met actieve time-out toch kon blijven posten.
- **Wat we toevoegden:**
  - `UserTimeout`-model (`TimeoutId`, `UserId`, `IssuedById`, `EndsAt`, `IssuedAt`) in
    een eigen MongoDB-collectie (`Timeouts`).
  - `POST /api/UserTimeout` — alleen moderator/admin; legt een time-out op voor de
    opgegeven duur in minuten.
  - `GET /api/UserTimeout/my` — geeft de actieve (nog niet verlopen) time-out van de
    ingelogde gebruiker terug (204 als er geen is); gebruikt door de frontend om de
    time-out-banner te tonen.
  - `DELETE /api/UserTimeout/{userId}` — heft een actieve time-out vroegtijdig op
    (moderator/admin).
  - `UserTimeoutService.GetActive(userId)` — centrale helper die controleert of een
    gebruiker een actieve time-out heeft op basis van `EndsAt > UtcNow`.
  - `PostController.Add` en `PostController.Update` uitgebreid met een time-out-check:
    bij een actieve time-out wordt HTTP 423 (Locked) teruggegeven met de `EndsAt`
    timestamp, zodat de frontend de banner kan bijwerken.
- **Waarom backend-domein:** Persistente time-out-records, tijdsgebonden autorisatie
  (`EndsAt > UtcNow`) en afdwinging van het posteerverbod zijn volledig server-side
  verantwoordelijkheden. De frontend kan niet zelf beslissen of iemand mag posten; dat
  is precies wat autorisatie en persistente state op de server regelen.

---

## Een nieuwe aanpassing toevoegen

Telkens we opnieuw iets aan de backend moeten wijzigen om een frontend-taak
mogelijk te maken, voegen we hier een entry toe (en een rij in het overzicht):

```markdown
## N. `<conventional commit subject>`

- **Commit:** `<hash>` — JJJJ-MM-DD
- **Nodig voor:** <welke frontend-US>
- **Wat ontbrak:** <wat de geleverde backend niet kon>
- **Wat we toevoegden:** <de aanpassing>
- **Waarom backend-domein:** <waarom dit server-werk is, geen presentatielogica>
```
