# Docker Compose Setup - Stappenplan

Dit document legt uit hoe je de Chatfish applicatie opstart met Docker Compose.

## Stap 1: Vereisten

Zorg dat je hebt geïnstalleerd:
- **Docker Desktop** (of Docker + Docker Compose)
- Een teksteditor (bijvoorbeeld Notepad, VS Code, of nano)

## Stap 2: Start de applicatie

1. Open een terminal/command prompt
2. Ga naar de deze map (= de map waarin dit README.md bestand zit)
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

> Als je een ```failed to solve: mcr.microsoft.com/dotnet/aspnet:9.0:``` foutmelding krijgt heeft dat allicht te maken met een DNS/IPv6 adres issues. Je kan daarrond werken door eerst het IPv4 adres van ```mcr.microsoft.com``` op te halen. Dat doe je met ```nslookup mcr.microsoft.com```. Allicht krijg je meerdere adressen te zien (vb. ```150.171.70.10```). Dat adress voeg je toe in het ```C:\Windows\System32\drivers\etc\hosts``` bestand:  
```
...
150.171.70.10 mcr.microsoft.com
...
```

## Stap 3: Vul de database met testdata

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

## Stap 4: Gebruik de applicatie

Na het opstarten zijn de volgende services beschikbaar:

- **Backend API**: http://localhost:8080
  - Swagger: http://localhost:8080/swagger
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

Vervang `backend`, `mongodb`, of `minio` om andere services te herstarten.

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

De applicatie bestaat uit 3 services:
- **MongoDB**: Database (poort 27017)
- **MinIO**: Bestandsopslag (poorten 9000 en 9001)
- **Backend**: .NET API (poort 8080)

Alle services communiceren via een intern Docker netwerk. De service namen (`mongodb`, `minio`, etc.) worden gebruikt voor interne communicatie.
