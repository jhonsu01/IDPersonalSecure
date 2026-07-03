# Especificación funcional — IDPersonalSecure

Documento derivado de la guía técnica del producto. Define el alcance funcional
compartido entre las apps Android y Windows.

## Objetivos

- Gestión segura de documentos personales de identidad.
- Funcionamiento offline-first.
- Interoperabilidad Android ↔ Windows mediante archivos cifrados `.securevault`.
- Cifrado fuerte local (ver [`CRYPTO.md`](CRYPTO.md)).
- Experiencia moderna en ambas plataformas.

## Arquitectura

| Plataforma | Lenguaje / UI                         | Almacenamiento                    |
| ---------- | ------------------------------------- | --------------------------------- |
| Android    | Kotlin + Jetpack Compose (MVVM)       | JSON cifrado en almacenamiento interno |
| Windows    | C# .NET 8 + WPF (MVVM)                | JSON cifrado en `%APPDATA%`        |

> En v0.1.0 ambas plataformas usan el mismo almacén JSON cifrado para garantizar
> interoperabilidad directa. Room/SQLite quedan como evolución futura sin cambiar
> el formato `.securevault`.

## Modelo de datos (documento)

| Campo        | Tipo    | Notas                                  |
| ------------ | ------- | -------------------------------------- |
| id           | UUID    | Identificador único                    |
| name         | string  | Nombre visible                         |
| type         | enum    | Tipo de documento (ver catálogo)       |
| country      | string  | ISO 3166-1 alpha-2 (p. ej. `CO`)       |
| number       | string  | Número del documento                   |
| issueDate    | date    | Fecha de expedición                    |
| expiryDate   | date    | Fecha de vencimiento (si aplica)       |
| hasExpiry    | bool    | Si el documento vence                  |
| urlSource    | string  | URL fuente (documento dinámico)        |
| fileName     | string  | Adjunto cifrado asociado (si existe)   |
| notes        | string  | Notas libres                           |
| createdAt    | datetime| Fecha de creación                      |

## Catálogo de tipos

- **Colombia:** Registro Civil, TI, CC, CE, Pasaporte, PPT, SC, NIT.
- **Multipaís (configurable):** DNI, CURP, RUT, Pasaporte, Certificado.

## Funcionalidades

### v0.1.0 (implementadas)

- Desbloqueo por PIN (deriva la clave; no se almacena).
- Alta / edición / borrado de documentos (metadatos).
- Listado y búsqueda por texto.
- Filtros: vencidos / próximos a vencer / sin vencimiento.
- Exportar bóveda a `.securevault` (requiere PIN).
- Importar bóveda desde `.securevault` (valida PIN e integridad).

### Futuras

- Importar PDF / imagen como adjunto cifrado; escaneo (Android).
- OCR y clasificación automática.
- Biometría (Android) / Windows Hello.
- Notificaciones de vencimiento (locales / toast).
- Marca de agua configurable en PDF/imágenes.
- Compartición individual segura; QR verificable.

## Consideraciones críticas

- El algoritmo de cifrado es **idéntico** en ambas plataformas.
- El formato `.securevault` debe mantenerse estable (versionar `formatVersion`).
- Claves nunca hardcodeadas ni persistidas.
- Validación de integridad obligatoria en la importación.
