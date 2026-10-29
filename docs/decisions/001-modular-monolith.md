# ADR-001: Modular Monolith

**Status:** Accepted for MVP

## Context

Pathway is a small portfolio product with a 3-week MVP target. Splitting into deployable microservices would add operational complexity without a demonstrated scaling need.

## Decision

Use one ASP.NET Core Web API with clear application, domain and infrastructure boundaries, plus a separate Vue frontend. Start with one backend project and one xUnit test project.

## Alternatives

Microservices; multiple Clean Architecture class libraries from day one.

## Consequences

Faster delivery and simpler deployment. Module boundaries require discipline; split later only when justified by change pressure or scale.
