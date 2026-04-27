1. Collector
   Hämtar nya Truth Social-poster.
   Ex: Python + truthbrush.

2. Backend/API
   Tar emot/sparar poster, analyser, alerts och status.
   Ex: .NET 10 Web API.

3. AI Analyzer
   Analyserar nya poster och sätter market impact score 1–10.
   Kan ligga i .NET-backenden eller som separat worker.

4. Alert Service
   Skickar e-post när score > 6.
   Ex: SendGrid, SMTP eller Azure Communication Services.

5. Database
   Sparar posts, scores, alerts och historik.
   Ex: Azure SQL, PostgreSQL eller SQLite för MVP.

6. Frontend
   Visar dashboard.
   Ex: React + TypeScript.
   
   
------------------

3a. Datamodell för analys

Lägg till fält/tabell för analysresultat:

PostId
MarketImpactScore 1–10
Reasoning
AnalyzedAt
AnalyzerVersion
RawAiResponse
3b. Mock Analyzer

Bygg en lokal fake-analyzer först:

Om text innehåller "tariff", "China", "Fed" → score 7
Om text innehåller "thank you", "great crowd" → score 2
Annars → score 4

Målet är att testa hela flödet utan AI-kostnad.

3c. Analyzer Worker

Skapa en worker som:

hämtar poster där AnalyzedAt är null
analyserar dem
sparar resultatet
loggar vad som hände
3d. Prompt + JSON-format

Bestäm exakt svar från AI:n, t.ex.

{
  "marketImpactScore": 7,
  "reasoning": "Mentions tariffs and China, which may affect market expectations.",
  "affectedAssets": ["stocks", "USD", "China-related equities"]
}
3e. Riktig AI-koppling

Byt ut mocken mot riktig AI-klient:

OpenAI / Azure OpenAI
API key via config/user secrets
timeout
felhantering
retry
3f. Spara och visa analysen

Uppdatera API/frontend så du kan se:

post
score
motivering
analyserad tid
3g. Körning var 5:e minut

När allt fungerar lokalt:

Collector hämtar nya poster
Analyzer analyserar oanalyserade poster
API visar resultat

Jag hade alltså byggt i denna ordning:

3a → 3b → 3c → 3d → 3e → 3f → 3g