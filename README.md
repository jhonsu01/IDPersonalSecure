<div align="center">

# 🛡️ IDPersonalSecure

**Tarjetero / bóveda digital personal, multiplataforma y offline-first.**
Gestiona documentos de identidad cifrados localmente en **Android** y **Windows**,
con exportación/importación segura entre dispositivos.

[![Release](https://img.shields.io/github/v/release/jhonsu01/IDPersonalSecure?label=última%20release)](https://github.com/jhonsu01/IDPersonalSecure/releases/latest)
[![Build](https://github.com/jhonsu01/IDPersonalSecure/actions/workflows/release.yml/badge.svg)](https://github.com/jhonsu01/IDPersonalSecure/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

</div>

---

## ✨ Características

- 🔐 **Cifrado fuerte local**: AES-256-GCM, clave derivada del PIN con PBKDF2-HMAC-SHA256 (210 000 iter) e integridad HMAC-SHA256.
- 📱💻 **Dos apps nativas**: Android (Kotlin + Jetpack Compose) y Windows (C# .NET 8 + WPF).
- 🔗 **Interoperables**: exporta una bóveda `.securevault` en un dispositivo e impórtala en el otro.
- 📴 **Offline-first**: ningún dato sale del dispositivo; ningún documento se guarda en texto plano.
- 🎨 **UI moderna**: Material 3 (Android) y Fluent/tarjetas (Windows), con modo oscuro.

> El formato de cifrado es idéntico en ambas plataformas y está documentado en
> [`docs/CRYPTO.md`](docs/CRYPTO.md) (fuente de verdad).

## 📥 Descargas

Cada nueva versión publica automáticamente los instaladores en la
**[última release](https://github.com/jhonsu01/IDPersonalSecure/releases/latest)**:

| Plataforma | Archivo                         | Instalación                                  |
| ---------- | ------------------------------- | -------------------------------------------- |
| Android    | `IDPersonalSecure-<ver>.apk`    | Habilita "orígenes desconocidos" e instala.  |
| Windows    | `IDPersonalSecure-<ver>.msi`    | Doble clic → instalador MSI.                  |

## 🗺️ Estructura del proyecto

```
IDPersonalSecure/
├── android/            App Android (Kotlin + Jetpack Compose)
├── windows/            App Windows (C# .NET 8 + WPF) + instalador WiX (MSI)
├── docs/               Especificación funcional y criptográfica
├── tools/              Utilidades (generación de iconos multi-densidad)
└── .github/workflows/  CI/CD: compila APK + MSI y publica la release
```

## 🔨 Compilación

El **APK** y el **MSI** se compilan automáticamente en GitHub Actions al publicar
un tag `vX.Y.Z`. Para compilar localmente:

- **Android:** requiere Android SDK + JDK 17 → `cd android && ./gradlew assembleDebug`
- **Windows:** requiere .NET 8 SDK → `cd windows && dotnet build -c Release`

## 🚀 Versionado y releases

- Versionado semántico por tags git: `vMAYOR.MINOR.PATCH`.
- El workflow mantiene **solo la última release**: al publicar una nueva, borra las anteriores.
- Bump de versión asistido: `pwsh scripts/bump.ps1 -Part patch` (o `minor` / `major`).

## 🧭 Roadmap

- [x] v0.1.0 — Bóveda, PIN, cifrado, export/import `.securevault`, iconos, CI auto-release.
- [x] v0.2.0 — Adjuntar PDF/imagen cifrado, selector de fecha (calendario), compartir con marca de agua (trámite + ID único + fecha/hora); Android usa el share nativo. Fixes de UI en Windows.
- [x] v0.3.0 — Marca de agua **opcional** al compartir, **historial de compartidos** (con destinatario editable), fix del calendario en Windows y del share en Android.
- [x] v0.4.0 — Calendario propio legible (Windows), **tipo de documento personalizado**, **URL clicable** (abre el navegador), y **recordatorios con hora + notificación** (Android nativa; Windows con la app abierta / al abrirla).
- [ ] OCR (ML Kit / Tesseract), clasificación de documentos.
- [ ] Biometría (Android) y Windows Hello.
- [ ] Recordatorios en segundo plano en Windows (tarea programada) y re-programación tras reinicio (Android).
- [ ] Firma de release del APK y tests de interoperabilidad cruzada en CI.
- [ ] QR verificable, sincronización opcional en la nube.

## 📄 Licencia

MIT © 2026 [jhonsu01](https://github.com/jhonsu01). Ver [`LICENSE`](LICENSE).

---

## ☕ Apoyo / Donaciones

Si esta app te resulta útil, puedes apoyar el proyecto con una donación:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/V7V81LV7GX)

**Repositorio:** https://github.com/jhonsu01/IDPersonalSecure
