# TrumpStockAlert

TrumpStockAlert is an AI-powered market monitoring system that tracks new posts from Donald Trump’s Truth Social account and evaluates their potential impact on financial markets.

The platform continuously collects new Truth Social posts, analyzes them using AI, assigns a market impact score, and sends alerts when a post is considered significant enough to potentially affect stocks, currencies, sectors, or broader market sentiment.

The goal of the project is to create a lightweight, low-cost, near real-time monitoring platform that can identify politically driven market-moving events before they are fully reflected in the market.

---

# Core Features

- Automatically fetches new Truth Social posts
- Stores posts and historical analysis data
- Uses AI to estimate market impact
- Assigns impact scores and reasoning
- Sends alerts for high-impact posts
- Provides API access and dashboard support
- Supports low-cost self-hosted deployment
- Designed for modularity and future expansion

---

# Tech Stack

| Layer | Technology |
|---|---|
| Collector | Python + Playwright |
| Backend/API | .NET 10 Web API |
| AI Integration | OpenAI / Azure OpenAI |
| Database | PostgreSQL |
| Frontend | React + TypeScript |
| Deployment | Docker Compose |
| Hosting | Ubuntu Server / Azure |

---

# Project Structure

```text
/trumpStockAlert
  /api
  /collector
  /frontend
  /database
  /docs
  docker-compose.yml
  README.md
```

---

# High-Level Architecture

The system consists of several components.

---

## 1. Collector

Responsible for fetching new Truth Social posts.

### Responsibilities

- Poll Truth Social every few minutes
- Detect new posts
- Avoid duplicates
- Store raw post data
- Handle retries and rate limiting
- Bypass blocking and anti-bot protections when necessary

### Technologies

- Python
- Playwright

### Notes

The project originally explored API-based collection through third-party tooling such as `truthbrush`.

Due to rate limiting and blocking issues from Truth Social, the project is moving toward a browser automation/scraping approach using Playwright and Python.

This approach provides:

- Better resilience against blocking
- More realistic browser behavior
- Easier debugging of anti-bot protections
- Greater long-term flexibility

---

## 2. Backend/API

The central service that coordinates the system.

### Responsibilities

- Store posts
- Store analysis results
- Store alerts and logs
- Expose REST API endpoints
- Trigger analysis jobs
- Provide dashboard data

### Technologies

- .NET 10 Web API
- ASP.NET Core
- Entity Framework Core

---

## 3. AI Analyzer

Analyzes posts and estimates their market impact.

### Responsibilities

- Read unanalyzed posts
- Generate impact scores
- Explain reasoning
- Detect affected assets and sectors
- Store analysis results

### Example Output

```json
{
  "marketImpactScore": 7,
  "reasoning": "Mentions tariffs and China, which may affect market expectations.",
  "affectedAssets": [
    "stocks",
    "USD",
    "China-related equities"
  ]
}
```

### Technologies

- OpenAI
- Azure OpenAI
- GPT models

---

## 4. Alert Service

Sends notifications when important posts are detected.

### Responsibilities

- Trigger alerts above configured thresholds
- Avoid duplicate alerts
- Log sent notifications
- Support multiple delivery providers

### Technologies

- SMTP
- SendGrid
- Azure Communication Services

---

## 5. Database

Stores all persistent system data.

### Stored Data

- Posts
- AI analyses
- Alert history
- Scheduler runs
- Logs
- System status

### Technologies

- PostgreSQL
- Azure SQL
- SQLite (MVP/testing)

---

## 6. Frontend Dashboard

Displays system status and analysis results.

### Features

- View latest Truth Social posts
- View AI impact scores
- Read analysis reasoning
- View alert history
- Monitor collector and analyzer health

### Technologies

- React
- TypeScript

---

# Analysis Data Model

Example analysis fields:

| Field | Description |
|---|---|
| PostId | Reference to analyzed post |
| MarketImpactScore | Score from 1–10 |
| Reasoning | AI explanation |
| AnalyzedAt | Analysis timestamp |
| AnalyzerVersion | AI/version identifier |
| RawAiResponse | Raw AI response |

---

# Current Status

## Implemented

- .NET backend/API
- PostgreSQL integration
- Docker deployment
- Collector scheduler
- Basic post collection
- Initial AI analysis pipeline

## In Progress

- Playwright-based collector
- Improved anti-blocking handling
- Alert pipeline improvements

## Planned

- Frontend dashboard
- Advanced analytics
- Real-time notifications
- Historical market correlation analysis

---

# Development Strategy

The recommended implementation order.

---

## Step 1 — Database Model

Create the data model for storing analyses.

---

## Step 2 — Mock Analyzer

Implement a fake analyzer before integrating real AI.

### Example Rules

Posts containing:

- `tariff`
- `China`
- `Fed`

→ score `7`

Posts containing:

- `thank you`
- `great crowd`

→ score `2`

Otherwise:

→ score `4`

This allows the entire pipeline to be tested without AI costs.

---

## Step 3 — Analyzer Worker

Create a background worker that:

- Fetches unanalyzed posts
- Runs analysis
- Stores results
- Logs execution details

---

## Step 4 — AI Prompt + JSON Contract

Define a strict and predictable AI response format.

### Example

```json
{
  "marketImpactScore": 7,
  "reasoning": "Mentions tariffs and China, which may affect market expectations.",
  "affectedAssets": [
    "stocks",
    "USD",
    "China-related equities"
  ]
}
```

---

## Step 5 — Real AI Integration

Replace the mock analyzer with a real AI provider.

### Requirements

- API key configuration
- Timeout handling
- Error handling
- Retries
- Logging

---

## Step 6 — Frontend/API Integration

Expose analysis results in the API and dashboard.

Users should be able to see:

- Post content
- Score
- Reasoning
- Analysis timestamp

---

## Step 7 — Scheduled Execution

Run the full pipeline automatically every 5 minutes.

### Full Flow

1. Collector fetches new posts
2. Backend stores them
3. Analyzer processes new posts
4. Alerts are triggered if thresholds are exceeded
5. Dashboard/API displays results

---

# Quick Start

## Requirements

- Docker
- Docker Compose
- OpenAI API key
- PostgreSQL (if running outside Docker)

---

## Start the System

```bash
docker compose up --build
```

---

## API

### Swagger

```text
http://localhost:8080/swagger
```

### Health Endpoint

```text
http://localhost:8080/health
```

---

# Deployment

The project supports both cloud hosting and self-hosted deployment.

---

## Self-Hosted

Example setup:

- Ubuntu Server
- Docker Compose
- PostgreSQL
- Local scheduler/worker

---

## Cloud

Example setup:

- Azure App Service
- Azure Functions
- Azure SQL
- Azure Static Web Apps

---

# Design Goals

## Low Cost

The platform is intentionally designed to run with minimal operational cost.

---

## Modularity

Each component can be replaced independently:

- Different collectors
- Different AI providers
- Different databases
- Different notification services

---

## Reliability

The system should:

- Retry failed jobs
- Handle rate limits
- Recover from temporary failures
- Log important operations

---

## Scalability

Although initially designed as a lightweight personal project, the architecture supports future expansion:

- More monitored accounts
- More AI models
- More alert channels
- More advanced analytics

---

# Example Use Cases

- Detect politically driven market events
- Monitor tariff and trade announcements
- Monitor geopolitical escalation
- Track sentiment changes
- Generate automated trading research
- Build historical datasets for AI/finance research

---

# Future Ideas

Potential future improvements:

- Multi-account monitoring
- Sentiment trend analysis
- Market correlation tracking
- Sector-specific alerts
- Telegram/Discord notifications
- Historical AI performance tracking
- AI confidence scoring
- Advanced dashboards
- Real-time streaming updates

---

# License

This project is currently intended for personal and experimental use.
