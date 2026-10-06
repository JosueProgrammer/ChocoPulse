# CallTarifier

Sistema web de tarificación de llamadas internas (ASP.NET Core Razor Pages + Worker de integración).

## Solución

- `CallTarifier.Web` — interfaz Razor Pages
- `CallTarifier.Integration` — Worker Service (AMI/CDR/Firebase; stubs en Fase 1)

## Requisitos

- .NET 8 SDK

## Ejecutar (modo mock)

```bash
dotnet restore
dotnet build CallTarifier.sln
dotnet run --project CallTarifier.Web
```

Worker (opcional en Fase 1):

```bash
dotnet run --project CallTarifier.Integration
```

Abrir la URL mostrada en consola (por defecto HTTPS en `launchSettings`).

### Login demo

- Correo: `juan.perez@chocopulse.local`
- Contraseña: cualquier valor no vacío

`Application:UseMockData` está en `true` por defecto.
