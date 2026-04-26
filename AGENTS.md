# AGENTS.md - GarageFlow

## Mission
Guide Codex agents to implement GarageFlow with Superpowers workflows and strict architecture boundaries.

## Target Architecture
- GarageFlow.BuildingBlocks
- GarageFlow.Application
- GarageFlow.Api
- GarageFlow.Domain
- GarageFlow.Infrastructure
- GarageFlow.Tests.Unit
- GarageFlow.Tests.Integration
- Customers module inside each layer (for example: `GarageFlow.Application.Customers`)

## Dependency Direction
- Api -> Application -> Domain -> BuildingBlocks
- Infrastructure -> Application, Domain
- Domain must not depend on Api or Infrastructure

## Test Scope
- Unit project focuses on Domain + Application
- Integration project hits API to in-memory database with external integrations mocked
