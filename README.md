# Docker Compose Setup - Stappenplan

Dit deel legt uit hoe je de Chatfish backend lokaal opstart met Docker Compose.

## Stap 1: Vereisten

Zorg dat je hebt geïnstalleerd:
- **Docker Desktop** (of Docker + Docker Compose)
- Een teksteditor (bijvoorbeeld Notepad, VS Code, of nano)

### Substap 1 bis: HTTPS instellen voor Docker (optioneel)
Als je de backend wenst aan te bieden via HTTPS op Docker moet je volgend command uitvoeren:
```
dotnet dev-certs https -ep %USERPROFILE%\.aspnet\https\aspnetapp.pfx -p ChatFish4Development
dotnet dev-certs https --trust
```

In ```docker-compose.yaml``` haal je vervolgens de lijnen met ```[SetUpHTTPS]``` uit commentaar.

Meer informatie: [Hosting ASP.NET Core images with Docker Compose over HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-10.0)

## Stap 2: Start de applicatie

1. Open een terminal/command prompt
2. Ga naar de deze map (= de map waarin dit README.md bestand zit)
3. Typ het volgende commando:

```bash
docker compose up -d
```

Dit commando:
- Downloadt alle benodigde images (als je ze nog niet hebt)
- Bouwt de backend
- Start alle services (MongoDB, MinIO, Backend)

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
docker compose exec chatfish-api bash /app/seed/seed.sh 'mongodb://chatfish-mongodb:27017' 'ChatfishDb' 'chatfish-minio:9000' 'minioadmin' 'minioadmin'
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

- **ASP.NET API (http)**: http://localhost:8080
  - Swagger: http://localhost:8080/swagger
- **ASP.NET API (https, als je stap 1.a hebt uitgevoerd)**: http://localhost:8081
  - Swagger: http://localhost:8081/swagger
- **MinIO Console**: http://localhost:9001
  - Gebruikersnaam: `minioadmin`
  - Wachtwoord: `minioadmin`

### Substap 4 bis: test frontend (optioneel)
Er is ook een ES6 mini-frontend om de login te testen. 
Deze kan je terugvinden in ```/Tests/Frontend```.
De ```index.html``` kan je openen met Live Server.

#### Tip: Live Server onder HTTPS draaien
Als je Live Server ook onder HTTPS wenst te draaien moet je het .pfx certificaat uit stap 1 bis uitpakken naar key.pem en cert.pem files.
Lokaal onder HTTPS draaien is niet strict nodig, maar het kan zijn dat bepaalde browser features niet zullen werken als je onder HTTP draait (omwille van security vereisten).

Live Server op HTTPS instellen doe je als volgt:
1. Open een bash in je USER PROFILE/.aspnet/https directory. Dat is de directory waar je je certificaat van stap 1 bis hebt gezet.
   Git Bash komt automatisch mee als je Git for Windows op je systeem hebt gezet. Zoek op "Git Bash" na installatie.
2. ```openssl pkcs12 -in aspnetapp.pfx -nocerts -out key.pem -nodes```
   Geef het paswoord in: ChatFish4Development
3. ```openssl pkcs12 -in aspnetapp.pfx -clcerts -nokeys -out cert.pem```
   Geef het paswoord in: ChatFish4Development
4. Open VSCode en ga naar de Live Server settings, meer bepaald de "Settings: Https" rubriek.
   - enable: true
   - cert: full path naar je cert.pem file
   - key: full path naar je cert.key file
   - passphrase: mag je leeglaten.
5. Sluit en open VSCode opnieuw: vanaf draait je Live Server onder https.
   - Opgelet: mogelijks opent Live Server onder https://127.0.0.1 en krijg je de melding dat dat domein 'Not Secure' is.  
     Dat moet je aanpassen naar https://localhost aangezien het certificaat voor dat 'domein' werd aangemaakt.

Voor Angular-frontends verwijzen we naar de Angular documentatie.

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
docker compose restart chatfish-aspnet
```

Vervang `chatfish-aspnet` door `chatfish-mongodb`, of `chatfish-minio` om andere services te herstarten.

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
- **ASP.NET Core**: .NET API (HTTP: poort 8080, HTTPS: 8081)

Alle services communiceren via een intern Docker netwerk. De service namen (`cnatfish-mongodb`, `chatfish-minio`, etc.) worden gebruikt voor interne communicatie.

# Azure setup - Stappenplan

Dit deel legt uit hoe je de UnsentStories backend éénmalig manueel configureert in Azure.  

## Stap 1: Vereisten

Zorg dat je een Azure abonnement hebt waarmee je mistens 3 Container-apps kan aanmaken:

* Voor de dev versie van deze applicatie wordt het 'edu-gradprogrammeren-prod' abonnement gebruikt, meer bepaald voor de cursus FEG (frontend gevorderd). Alle resources werden aangemaaktin de 'FEG' resourcegroup. Daar wordt nog gebruik gemaakt van de 'oude' naam van het project, namelijk 'ChatFish'.
* Voor de acceptatie/productie versie van deze applicatie werd door UCLL ICT op vrijdag 22/5/2026 het abonnement 'edu-unsentstories' aangemaakt. De resources worden aangemaakt in 'unsentstories', 'unsentstories-acc' en 'unsentstories-prod' resourcegroups.

## Stap 2: Resourcegroep aanmaken

Maak een resourcegroep 'unsentstories-[dev|acc|prod]' aan, of andere naam moest deze al bestaan.
Maak ook een resourcesgroep 'unsentstories' voor omgevingsgedeelde resources zoals een container registry.

## Stap 3: Maak een Container-app voor MongoDB

Alterntief: Cosmos DB (zie verder).

### Rubriek: Basisinformatie
  - Kies je abonnement en resourcegroep.
  - **Naam van container-app**: unsentstories-[dev|acc|prod]-mongodb
  - **Implementatiebron**: Containerinstallatiekopie
  - **Regio**: West Europe
  - **Container Apps-omgeving**: hier maak je een nieuwe 'unsentstories-[dev|acc|prod]' omgeving aan die je ook gaat hergebruiken voor de 2 andere app containers. Het is belangrijk dat alle containers apps in dezelfde omgeving draaien.

Druk op 'Volgende'

### Rubriek: Container
  - **Naam**: unsentstories-[dev|tst|prod]-mongodb
  - **Bron van installatiekopie**: Docker-hub of andere registers
  - **Installatiekopietype**: Openbaar
  - **Aanmeldingsserver voor register**: docker.io
  - **Installatiekopie en tag**: mongo:7 (zie ook docker-compose.yml )
  - **Opdracht overschrijven**: (leeglaten)
  - **Argument overschrijven**: (leeglaten)
  - **Ontwikkelingsstack**: Niet opgegeven
  - **Workfloadprofiel**: (laagste profiel kiezen)
  - **CPU en geheugen**: 0.5 CPU-kernen, 1 Gi-geheugen
  - **Omgevingsvariabelen**
    - **MONGO_INITDB_DATABASE**: ChatfishDb

Druk op 'Volgende'

### Rubriek: Inkomend
  - **Inkomend**: (zet hier een vinkje)
  - **Inkomend verkeer**: beperkt tot Container-Apps-omgeving.
  - **Type inkomend verekeer**: TCP
  - **Doelpoort**: 27017
  - **Beschikbaar gemaakte poort**: 27017

Druk op 'Beoordelen en maken'.  
Normaal krijg je een 'Geslaagd' melding.  
Druk uiteindelijk op 'Maken' - de app container wordt nu aangemaakt.

### Alternatief: Cosmos DB
Te bekijken.

## Stap 3: Maak een Container-app voor Minio

Alternatief: Azure Blob Storage (zie verder).

### Rubriek: Basisinformatie
  - Kies je abonnement en resourcegroep.
  - **Naam van container-app**: unsentstories-[dev|acc|prod]-minio
  - **Implementatiebron**: Containerinstallatiekopie
  - **Regio**: West Europe
  - **Container Apps-omgeving**: hier gebruik je de 'unsentstories-[dev|acc|prod]' omgeving die je in stap 2 hebt aangemaakt.

Druk op 'Volgende'

### Rubriek: Container
  - **Naam**: unsentstories-[dev|acc|prod]-minio
  - **Bron van installatiekopie**: Docker-hub of andere registers
  - **Installatiekopietype**: Openbaar
  - **Aanmeldingsserver voor register**: docker.io
  - **Installatiekopie en tag**: minio/minio:latest (zie ook docker-compose.yml )
  - **Opdracht overschrijven**: minio
  - **Argument overschrijven**: server, /data
  - **Ontwikkelingsstack**: Niet opgegeven
  - **Workfloadprofiel**: (laagste profiel kiezen)
  - **CPU en geheugen**: 0.5 CPU-kernen, 1 Gi-geheugen
  - **Omgevingsvariabelen**
    - **MINIO_ENDPOINT**: minio:9000
    - **MINIO_AK**: minioadmin
    - **MINIO_SK**: minioadmin
    - **MINIO_USE_SSL**: false
    - **MINIO_ROOT_USER**: minioadmin
    - **MINIO_ROOT_PASSWORD**: minioadmin

Druk op 'Volgende'

### Rubriek: Inkomend
  - **Inkomend**: (zet hier een vinkje)
  - **Inkomend verkeer**: beperkt tot Container-Apps-omgeving.
  - **Type inkomend verekeer**: TCP
  - **Doelpoort**: 9000
  - **Beschikbaar gemaakte poort**: 9000
  - **Extra TCP-poorten**
    - Doelpoort: 9001
    - Beschikbaar gemaakte poort: 9001

Druk op 'Beoordelen en maken'.  
Normaal krijg je een 'Geslaagd' melding.  
Druk uiteindelijk op 'Maken' - de app container wordt nu aangemaakt.

### Alternatief: Azure Blob Storage
Te bekijken.

## Stap 4: Maak een Container-app voor de ASP.NET Core API
Dit is wat moeilijker omdat je je image moet builden en pushen naar een container images repository.
Best maak je eerst een 'Container Registry' resource aan in je namespace.  
Tip: bij 'Prijsplan' kies je voor 'Basis' ipv 'Standard'.

### Rubriek: Basisinformatie
  - Kies je abonnement en resourcegroep.
  - **Naam van container-app**: unsentstories-[dev|acc|prod]-aspnet
  - **Implementatiebron**: Containerinstallatiekopie
  - **Regio**: West Europe
  - **Container Apps-omgeving**: hier gebruik je opnieuw de 'unsentstories-[dev|acc|prod]' omgeving die je in stap 2 hebt aangemaakt.

Druk op 'Volgende'

### Rubriek: Container
  - **Naam**: unsentstories-[dev|acc|prod]-aspnet
  - **Bron van installatiekopie**: Azure Container-registry
  - **Abonnement**: (kies je abonnement waar je je ACR hebt mee aangemaakt)
  - **Register**: (kies je register, vb. mijnregister.azurecr.io)
  - **Installatiekopie**: (kies je image)
  - **Installatiekopietag**: latest
  - **Verificatietype**: Geheimen (kan zijn dat je nu de user/paswoord van je container registry moet toevoegen)
  - **Opdracht overschrijven**: (leeglaten)
  - **Argument overschrijven**: (leeglaten)
  - **Ontwikkelingsstack**: .NET
  - **Workfloadprofiel**: (laagste profiel kiezen)
  - **CPU en geheugen**: 0.5 CPU-kernen, 1 Gi-geheugen
  - **Omgevingsvariabelen**
    - **MINIO_ENDPOINT**: unsentstories[dev|acc|prod]-minio:9000
    - **MINIO_AK**: minioadmin
    - **MINIO_SK**: minioadmin
    - **MINIO_USE_SSL**: false
    - **JWT_AUTHORITY**: http://localhost:8080
    - **JWT_AUDIENCE**: unsentstories-[dev|acc|prod]-api
    - **JWT_SECRET**: verander-dit-in-production
    - **CORS_ALLOWED_ORIGINS**: http://localhost:3000
    - **VAPID_SUBJECT**: mailto:admin@unsentstories.com
    - **VAPID_PUBLIC_KEY**: verander-dit-in-production
    - **VAPID_PRIVATE_KEY**: verander-dit-in-production
    - **ASPNETCORE_ENVIRONMENT**: Development
    - **ChatfishDatabase_ConnectionString**: mongodb://unsentstories-[dev|acc|prod]-mongodb:27017

Druk op 'Volgende'

### Rubriek: Inkomend
  - **Inkomend**: (zet hier een vinkje)
  - **Inkomend verkeer**: Verkeer vanaf elke locatie accepteren
  - **Type inkomend verekeer**: HTTP
  - **Transport**: Automatisch
  - **Onveilige verbindingen**: (niet aanvinken)
  - **Doelpoort**: 8080
  - **Sessie affiniteit**: (niet aanvinken)


Druk op 'Beoordelen en maken'.  
Normaal krijg je een 'Geslaagd' melding.  
Druk uiteindelijk op 'Maken' - de app container wordt nu aangemaakt.

## Stap 5: test data aanmaken (seeding)
Ga naar de 'unsentstories-[dev|acc|prod]-aspnet' resource en open een bash console (Controleren -> Console).
- Navigeer naar /app/seed.  
- Maak seed.sh uitvoerbaar: ```chmod u+r+x seed.sh```
- Voeg het seed script uit. Normaal gezien kan je net dezelfde parameters gebruiken als de lokale uitvoering omdat we in de cloud dezelfde namen en poorten hebben gekozen: 
```./seed.sh 'mongodb://unsentstories-[dev|acc|prod]-mongodb:27017' 'ChatfishDb' 'unsentstories-[dev|acc|prod]-minio:9000' 'minioadmin' 'minioadmin'```
