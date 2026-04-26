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