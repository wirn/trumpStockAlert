# TrumpStockAlert - Infrastructure Overview

## Purpose

Overview of where the different parts of TrumpStockAlert are hosted and how the system is structured.

---

# Home Server (gen-eric-server)

The home server is the primary runtime environment.

## Hosted Services

- Collector
- AI analyzer
- Backend/API
- PostgreSQL
- Internal/admin frontend
- Pi-hole
- Home Assistant (planned)
- Docker
- Tailscale

## Why Home Hosting

- Low cost
- Full control
- Easy experimentation
- Avoid cloud IP blocking
- Good for always-running services

## Network

- Ubuntu Server
- Docker + Docker Compose
- Static DHCP reservation
- Tailscale for remote access

## Remote Access

- Tailscale
- SSH

No public admin exposure.

---

# Azure / Cloud Hosting

## Planned Usage

Potential hosting for:
- public frontend
- public dashboard
- CDN/static hosting

Possible platforms:
- Azure Static Web Apps
- Cloudflare Pages
- Vercel

## Why

- Easier HTTPS
- Better uptime
- No dependency on home IP
- Safer public exposure

---

# Internal vs Public

## Private Components

Protected via Tailscale/API-key:

- Admin dashboard
- Scheduler endpoints
- Collector triggers
- Database
- AI analysis endpoints
- SSH/Docker access

## Public Components

Possible future public services:

- Public website
- Public dashboard
- Read-only alerts

---

# Docker Services

## Current / Planned Containers

- PostgreSQL
- Pi-hole
- Backend API
- Frontend dashboard
- Collector
- Analyzer

## Future Additions

- Reverse proxy
- Monitoring
- Backup services
- Log aggregation

---

# Security

## Current Strategy

- Tailscale remote access
- UFW firewall
- Secrets in `.env`
- No secrets in Git
- Docker restart policies

---

# Future Improvements

- Automated backups
- HTTPS reverse proxy
- Real email provider
- Monitoring/logging
- Automatic Docker updates
- Public frontend separation

---

# High-Level Architecture

```text
Public Users
    ↓
Public Frontend (Azure/Cloudflare/Vercel)

--------------------------------------

Private Admin Access
    ↓
Tailscale
    ↓
Home Server
    ├─ Frontend Dashboard
    ├─ Backend API
    ├─ Collector
    ├─ Analyzer
    ├─ PostgreSQL
    ├─ Pi-hole
    └─ Docker