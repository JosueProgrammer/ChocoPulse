# CallTarifier

Sistema web de tarificación de llamadas internas (ASP.NET Core Razor Pages + Worker AMI/CDR/Firebase).

## Proyectos

- `CallTarifier.Web` — Razor Pages, Firebase Auth, lectura Firestore
- `CallTarifier.Integration` — Worker AMI → CDR → tarifa → factura → Firestore

## Requisitos

- .NET 8 SDK
- Acceso LAN a Issabel (`192.168.3.183`)
- Archivo `secrets/firebase-service-account.json` (gitignored)
- Secret AMI en User Secrets del proyecto Integration
- **Firestore creado** en el proyecto Firebase `pymex-8c7e4` (Console → Build → Firestore → Create database). Sin esto el Worker no puede guardar llamadas.

## Configurar secret AMI

```bash
dotnet user-secrets set "Asterisk:Secret" "TU_SECRET_ADMIN" --project CallTarifier.Integration
```

## Ejecutar

```bash
dotnet build CallTarifier.sln
dotnet run --project CallTarifier.Integration
dotnet run --project CallTarifier.Web
```

Login: usuario Firebase Auth (Email/Password).

## Prueba end-to-end

1. Worker en máquina de la red `192.168.3.0/24`
2. Log debe mostrar `AMI connected`
3. Llamar `4000 → 4021` con Zoiper
4. Worker registra `CDR received` / `Call processed` / `Invoice generated`
5. Historial y Facturación en la Web muestran el registro

## WebRTC (JsSIP + WSS Issabel)

Config en `appsettings.json`:

- WSS: `wss://192.168.3.183:8089/ws`
- Dominio SIP: `192.168.3.183`

El navegador obtiene credenciales solo vía `GET /api/webrtc/session` (usuario autenticado).

### Secret de extensión (obligatorio)

Una de estas opciones (sin committear secretos):

1. Firestore `extensions/{ext}` con campo `sipSecret`, o `users/{uid}.sipSecret`
2. User Secrets:

```bash
dotnet user-secrets set "WebRTC:ExtensionSecrets:4000" "SECRET_ISABEL" --project CallTarifier.Web
```

Confirmar WSS en el PBX: `asterisk -rx "http show status"` y `https://192.168.3.183:8089/ws` (aceptar certificado si aplica).
