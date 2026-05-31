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
