# Vaktklar – Workforce & Competence Management

Full-stack bemannings- og kompetanseløsning for ledere, skiftplanleggere og kompetanseansvarlige.

Målet er at systemet skal **redusere planleggingsarbeid**, ikke skape mer. Det kombinerer bemanning, kompetanse, fravær, dekningsanalyse og kandidatforslag i samme arbeidsflyt.

## Hva systemet gjør

- Ansatt- og kompetanseoversikt
- Kompetansenivå og gyldighetsdato
- Automatisk varsling om utløpt/nær utløpt kompetanse
- Vaktplanlegging
- Minimumsbemanning per vakt
- Kompetansekrav per vakt
- Kritiske kompetansekrav
- Automatisk GREEN / YELLOW / RED-vurdering
- Forklaring på hvorfor en vakt ikke er dekket
- Kandidatforslag for manglende bemanning
- Hard-validering av kompetanse før tildeling
- Fraværsregistrering
- «Hva hvis denne ansatte blir borte?»-scenario
- Dashboard med handlingsprioritering
- Audit-logg
- Rollebasert tilgang
- HTTP-only autentiseringscookie
- Rate limiting på innlogging
- PWA/app-shell for mobil bruk
- Docker Compose
- GitHub Actions CI

## Teknologi

### Frontend

- React 19
- Vite 7
- JavaScript
- Responsiv CSS
- PWA manifest/service worker

### Backend

- C#
- .NET 9
- ASP.NET Core
- Minimal APIs
- Entity Framework Core
- SQL Server
- JWT-baserte autentiseringscookies
- BCrypt-passordhashing

### Engineering

- xUnit
- Docker / Docker Compose
- GitHub Actions
- Dependabot
- CodeQL
- Git
- GitHub

## Sikkerhet

API-et krever autentisering for applikasjonsdata. Skriveoperasjoner krever Admin, Manager eller HR-rolle. Innlogging er ratebegrenset, og kontoer låses midlertidig etter gjentatte feilforsøk.

Produksjonsdeploy skal bruke eksterne secrets for databasepassord, JWT-nøkkel og bootstrap-nøkkel. Se `README-SECURITY.md`.

## Viktig om produksjonsstatus

Repoet er betydelig utvidet, men dette skal **ikke** beskrives som ferdig produksjonssystem for ekte ansattdata ennå. Før produksjon må blant annet OIDC/etablert identitetsleverandør, MFA, full CSRF-vurdering, sentral audit-identitet, secret rotation, ekstern logging/monitorering, ordentlige EF Core-migrasjoner, backup/restore-testing og formell GDPR-/sikkerhetsgjennomgang ferdigstilles.

## Kjør lokalt

Kopier `.env.example` til `.env` og sett egne verdier for:

- `DB_PASSWORD`
- `JWT_SECRET_KEY`
- `VAKTKLAR_BOOTSTRAP_KEY`

Deretter:

```bash
docker compose up --build
```

Frontend: `http://localhost:8088`

API: `http://localhost:5080`

Health: `http://localhost:5080/health`

Første administrator opprettes én gang via `/api/auth/bootstrap` med bootstrap-nøkkelen. Se `README-SECURITY.md`.

## Tester og CI

GitHub Actions bygger og tester backend, lint/build-er frontend og bygger Docker Compose-stacken. CI-status må alltid verifiseres mot den konkrete workflow-kjøringen; README-et påstår ikke at en lokal kjøring er utført når den ikke er dokumentert.

## Demo-data

All eksisterende ansatt-, kompetanse- og vaktdata er fiktive demonstrasjonsdata. Det skal ikke legges inn reelle personopplysninger i dette offentlige repoet.

## Formål

Prosjektet er utviklet som en reell full-stack demonstrator rundt et konkret bemannings- og kompetanseproblem, med vekt på forklarbare regler, brukervennlighet, automatisering og sikkerhet.

## Forfatter

Anne Beth Andersen

## Portefølje

Prosjektet er et sentralt full-stack-prosjekt i utviklerporteføljen.
