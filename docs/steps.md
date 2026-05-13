1. Serverplattform
    1a. Uppdatera Ubuntu Server
    1b. Installera Git
    1c. Installera Docker
    1d. Installera Docker Compose-plugin
    1e. Verifiera att Docker startar automatiskt efter reboot
    1f. Skapa projektmapp på servern
    1g. Klona TrumpStockAlert-repot till servern
    1h. Skapa .env-fil för lokala secrets
    1i. Lägg till .env i .gitignore

2. Nätverk och åtkomst
    2a. Sätt fast IP/DHCP reservation för servern i Asus-routern
    2b. Verifiera SSH från din vanliga dator
    2c. Installera Tailscale på servern
    2d. Installera Tailscale på klientdatorn
    2e. Verifiera SSH via Tailscale
    2f. Aktivera UFW-brandvägg
    2g. Tillåt endast nödvändiga portar
    2h. Undvik publik port forwarding i första versionen

3. Docker Compose-bas
    3a. Skapa docker-compose.yml
    3b. Lägg till backend/API-container
    3c. Lägg till databas-container
    3d. Lägg till frontend-container
    3e. Lägg till collector-container
    3f. Lägg till analyzer-container
    3g. Lägg till restart: unless-stopped på alla tjänster
    3h. Verifiera att docker compose up -d startar allt
    3i. Verifiera att containers startar efter reboot

4. Databas/datamodell
    4a. Välj databas för self-hosting, förslagsvis PostgreSQL
    4b. Lägg till PostgreSQL i Docker Compose
    4c. Skapa persistent databasvolym
    4d. Byt EF Core-provider till PostgreSQL/Npgsql
    4e. Lägg till connection string i .env/config
    4f. Skapa eller anpassa DbContext
    4g. Skapa entiteter för TruthPost, PostAnalysis, AlertNotification och FetcherRun
    4h. Skapa migration
    4i. Uppdatera databasen
    4j. Verifiera tabeller i databasen

5. Backend/API
    5a. Dockerisera .NET API:t
    5b. Lägg till health endpoint
    5c. Lägg till Swagger endast för intern/dev-miljö
    5d. Koppla API:t till databasen
    5e. Skapa endpoint för att lista senaste poster
    5f. Skapa endpoint för att lista senaste analyser
    5g. Skapa endpoint för att lista alerts
    5h. Testa API:t från servern
    5i. Testa API:t från klient via Tailscale

6. Collector
    6a. Välj collector-teknik, exempelvis Python + truthbrush eller .NET worker
    6b. Dockerisera collector
    6c. Hämta senaste Truth Social-poster
    6d. Mappa hämtad data till TruthPost
    6e. Spara poster i databasen via API eller direkt DB
    6f. Lägg till ExternalId/TruthSocialId
    6g. Lägg till unik constraint för att undvika dubletter
    6h. Logga varje collectorkörning i FetcherRun
    6i. Testa manuell collectorkörning
    6j. Verifiera sparade poster i databasen

7. Manuell trigger
    7a. Skapa POST /api/collector/run
    7b. Skydda endpointen med scheduler/API-key
    7c. Kör collector via dependency injection eller separat process
    7d. Returnera FetcherRun-resultat som JSON
    7e. Testa endpointen via Swagger/curl
    7f. Verifiera att körningen loggas
    7g. Verifiera att nya poster sparas
    7h. Verifiera att dubletter inte sparas

8. Scheduler
    8a. Välj enkel scheduler, förslagsvis cron eller systemd timer
    8b. Skapa schemalagd körning för collector var 5:e minut
    8c. Skapa schemalagd körning för analyzer
    8d. Skapa schemalagd körning för alerts
    8e. Lägg scheduler-key i .env
    8f. Testa scheduler manuellt
    8g. Verifiera att scheduler fungerar efter reboot
    8h. Logga varje schedulerkörning
    8i. Dokumentera scheduler-kommandon i README

9. AI-analys
    9a. Lägg till OpenAI-konfiguration
    9b. Spara OpenAI API-nyckel och modell i .env
    9c. Skapa OpenAI-klient
    9d. Skapa mock analyzer först
    9e. Hämta oanalyserade TruthPost-rader
    9f. Bygg prompt baserad endast på sparade poster
    9g. Returnera strikt JSON från AI:n
    9h. Inför MarketImpactScore, ConfidenceScore och Direction
    9i. Lägg till Summary, Reasoning, AffectedAssets och Risks
    9j. Skapa POST /api/analysis/run
    9k. Testa analys via Swagger/curl
    9l. Verifiera sparad analys i databasen

10. Spara AI-analys
    10a. Skapa PostAnalysis-entitet
    10b. Lägg till DbSet<PostAnalysis>
    10c. Konfigurera relation mellan TruthPost och PostAnalysis
    10d. Skapa migration
    10e. Uppdatera databasen
    10f. Spara MarketImpactScore, ConfidenceScore och Direction
    10g. Spara Summary, Reasoning, AffectedAssets och Risks
    10h. Spara AnalyzerVersion och RawAiResponse
    10i. Spara AnalyzedAt
    10j. Testa sparad analys via API
    10k. Verifiera sparad analys i databasen

11. E-post/alerts
    11a. Skapa AlertSettings-konfiguration
    11b. Skapa AlertNotification-entitet
    11c. Skapa EF migration för AlertNotifications
    11d. Skapa alert evaluator-service
    11e. Skapa IEmailSender-interface
    11f. Implementera LogOnlyEmailSender först
    11g. Skapa POST /api/alerts/run
    11h. Lägg till threshold, exempelvis score >= 7
    11i. Lägg till dedupe så samma analys inte skickas flera gånger
    11j. Testa alert-flöde lokalt
    11k. Koppla alerts till scheduler-flödet
    11l. Lägg till riktig e-postleverantör
    11m. Verifiera riktigt e-postutskick

12. Frontend/dashboard
    12a. Dockerisera React-frontenden
    12b. Servera frontend via Nginx eller Caddy
    12c. Koppla frontend till self-hosted API
    12d. Visa senaste Truth Social-poster
    12e. Visa senaste analyser
    12f. Visa MarketImpactScore och ConfidenceScore
    12g. Visa Direction, Summary och Reasoning
    12h. Visa skickade alerts
    12i. Lägg till loading/error/empty states
    12j. Ta bort fallback/mock data när API-flödet är stabilt
    12k. Lägg till adminvy för schedulerstatus
    12l. Lägg till adminvy för senaste collectorkörningar
    12m. Lägg senare till alertinställningar

13. Backup
    13a. Skapa backup-script för PostgreSQL
    13b. Kör pg_dump till lokal backupmapp
    13c. Schemalägg nattlig backup med cron
    13d. Spara backup med datum i filnamnet
    13e. Rensa gamla backuper efter X dagar
    13f. Testa restore från backup
    13g. Kopiera backup till annan dator/NAS/moln senare

14. Loggning + felhantering
    14a. Strukturera backendloggar
    14b. Logga collector started/completed/failed
    14c. Logga analysis started/completed/failed
    14d. Logga alert evaluation started/completed/failed
    14e. Lägg till korrelations-id/run-id
    14f. Spara tekniska fel i databasen där det är relevant
    14g. Returnera tydliga ProblemDetails från API:t
    14h. Undvik att läcka secrets eller interna detaljer
    14i. Lägg till health checks
    14j. Dokumentera felsökning i README

15. Driftstabilitet
    15a. Lägg restart: unless-stopped på alla containers
    15b. Verifiera att Docker startar efter reboot
    15c. Verifiera att hela systemet startar efter reboot
    15d. Testa simulerat strömavbrott/restart
    15e. Kontrollera diskförbrukning
    15f. Kontrollera loggstorlek
    15g. Lägg till log rotation vid behov
    15h. Dokumentera start/stopp/restart-kommandon

16. Säkerhet
    16a. Ha alla secrets i .env eller Docker secrets senare
    16b. Lägg aldrig secrets i Git
    16c. Använd starka lösenord/API-nycklar
    16d. Skydda scheduler-endpoints med API-key
    16e. Begränsa publik exponering
    16f. Använd Tailscale som första remote access-lösning
    16g. Håll Ubuntu uppdaterat
    16h. Håll Docker images uppdaterade

17. Dokumentation
    17a. Dokumentera lokal serverstruktur
    17b. Dokumentera docker compose-kommandon
    17c. Dokumentera .env-exempel utan secrets
    17d. Dokumentera scheduler-konfiguration
    17e. Dokumentera backup/restore
    17f. Dokumentera felsökning
    17g. Dokumentera hur man deployar ny version