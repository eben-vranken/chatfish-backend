# Docker Compose Setup - Stappenplan

Dit document legt uit hoe je de Chatfish applicatie opstart met Docker Compose.

## Stap 1: Vereisten

Zorg dat je hebt geïnstalleerd:
- **Docker Desktop** (of Docker + Docker Compose)
- Een teksteditor (bijvoorbeeld Notepad, VS Code, of nano)

## Stap 2: Maak een .env bestand aan

1. Ga naar de `back-end-team10` map
2. Maak een nieuw bestand aan met de naam `.env` (let op: begin met een punt!)
3. Kopieer de onderstaande inhoud naar dit bestand:

```env
MINIO_ENDPOINT=minio:9000
MINIO_AK=minioadmin
MINIO_SK=minioadmin
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin

JWT_AUTHORITY=http://localhost:8080
JWT_AUDIENCE=chatfish-api
JWT_SECRET=verander-dit-in-production

CORS_ALLOWED_ORIGINS=http://localhost:3000

VAPID_SUBJECT=mailto:admin@chatfish.com
VAPID_PUBLIC_KEY=verander-dit-in-production
VAPID_PRIVATE_KEY=verander-dit-in-production

ChatfishDatabase__ConnectionString=mongodb://mongodb:27017

ASPNETCORE_ENVIRONMENT=Development

NEXT_PUBLIC_API_URL=http://localhost:8080
```

4. Sla het bestand op

**Tip**: Voor productie gebruik, verander de waarden die "verander-dit-in-production" bevatten naar veilige wachtwoorden en keys.

## Stap 3: Start de applicatie

1. Open een terminal/command prompt
2. Ga naar de `back-end-team10` map
3. Typ het volgende commando:

```bash
docker compose up -d
```

Dit commando:
- Downloadt alle benodigde images (als je ze nog niet hebt)
- Bouwt de backend en frontend
- Start alle services (MongoDB, MinIO, Backend, Frontend)

**Wacht even** tot alle containers gestart zijn. Je kunt de status controleren met:

```bash
docker compose ps
```

Alle services moeten "Up" tonen.

## Stap 4: Vul de database met testdata

Als je de applicatie voor het eerst opstart, is de database leeg. Je kunt deze vullen met testdata:

1. Typ het volgende commando in de terminal:

```bash
docker compose exec backend bash /app/seed/seed.sh 'mongodb://mongodb:27017' 'ChatfishDb' 'minio:9000' 'minioadmin' 'minioadmin'
```

2. Je krijgt een waarschuwing dat alle bestaande data verwijderd wordt
3. Typ `j` en druk op Enter om door te gaan
4. Wacht tot het script klaar is (dit kan even duren)

**Let op**: Dit verwijdert alle bestaande data in de database!

### Test gebruikers

Na het seeden zijn de volgende test gebruikers beschikbaar:

**Admin gebruiker:**
- Email: `admin@chatfish.be`
- Wachtwoord: `admin123`
- Rol: Admin

**Gewone gebruikers:**
- Email: `jan.jansen@example.com`
- Wachtwoord: `wachtwoord123`
- Rol: User

- Email: `maria.devries@example.com`
- Wachtwoord: `wachtwoord123`
- Rol: User

## Stap 5: Gebruik de applicatie

Na het opstarten zijn de volgende services beschikbaar:

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:8080
- **MinIO Console**: http://localhost:9001
  - Gebruikersnaam: `minioadmin`
  - Wachtwoord: `minioadmin`

## Handige commando's

### Stop de applicatie

```bash
docker compose down
```

### Stop en verwijder alle data

```bash
docker compose down -v
```

**Let op**: Dit verwijdert alle data in de database en MinIO!

### Bekijk de logs

```bash
docker compose logs -f
```

Druk op `Ctrl+C` om de logs te stoppen.

### Herstart een service

```bash
docker compose restart backend
```

Vervang `backend` door `frontend`, `mongodb`, of `minio` om andere services te herstarten.

## Problemen oplossen

### Containers starten niet

1. Controleer of Docker Desktop draait
2. Controleer of de `.env` file bestaat en correct is
3. Bekijk de logs: `docker compose logs`

### Database is leeg

Volg Stap 4 om de database te seeden met testdata.

### Poorten zijn al in gebruik

Als je een foutmelding krijgt dat een poort al in gebruik is:
- Stop andere applicaties die dezelfde poorten gebruiken
- Of pas de poorten aan in `docker-compose.yaml`

## Technische details

De applicatie bestaat uit 4 services:
- **MongoDB**: Database (poort 27017)
- **MinIO**: Bestandsopslag (poorten 9000 en 9001)
- **Backend**: .NET API (poort 8080)
- **Frontend**: Next.js applicatie (poort 3000)

Alle services communiceren via een intern Docker netwerk. De service namen (`mongodb`, `minio`, etc.) worden gebruikt voor interne communicatie.
