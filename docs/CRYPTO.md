# Formato canónico `.securevault` y modelo criptográfico

> Este documento es la **fuente de verdad**. Android y Windows deben implementar
> exactamente lo mismo. Cualquier cambio aquí obliga a actualizar ambas apps y a
> subir la versión del formato (`formatVersion`).

## 1. Primitivas

| Elemento              | Valor                                              |
| --------------------- | -------------------------------------------------- |
| Cifrado               | AES-256-GCM                                         |
| Tamaño de clave       | 32 bytes (256 bits)                                 |
| Derivación de clave   | PBKDF2 con HMAC-SHA256                              |
| Iteraciones PBKDF2    | 210000                                             |
| Salt                  | 16 bytes aleatorios (por bóveda)                   |
| Nonce / IV GCM        | 12 bytes aleatorios (por cada cifrado)             |
| Tag GCM               | 16 bytes                                            |
| Integridad adicional  | HMAC-SHA256                                         |

## 2. Derivación de claves

A partir del PIN del usuario y el `salt` de la bóveda se derivan **64 bytes**
con PBKDF2-HMAC-SHA256(210000):

```
DK        = PBKDF2-HMAC-SHA256(PIN_utf8, salt, 210000, 64 bytes)
encKey    = DK[0..32)     # clave AES-256-GCM
macKey    = DK[32..64)    # clave HMAC-SHA256
```

El PIN **nunca** se almacena. El `salt` sí se guarda en el manifiesto (no es secreto).

## 3. Blob cifrado (`EncBlob`)

Todo dato cifrado (la base de datos y cada archivo adjunto) se serializa así,
concatenado y luego codificado en Base64 cuando se guarda como texto:

```
EncBlob = nonce(12) || ciphertext(n) || tag(16)
```

- `ciphertext` y `tag` son la salida de AES-256-GCM sobre el plaintext.
- **AAD (Associated Data)** = bytes UTF-8 del `id` lógico del blob
  (`"database"` para la DB; el `id` del documento para cada archivo).
  Esto ata cada blob a su identidad y evita reordenamientos.

## 4. Estructura del archivo

`.securevault` es un ZIP (sin compresión obligatoria) con:

```
vault.securevault
├── manifest.json      # metadatos + salt + HMAC (texto, UTF-8)
├── database.enc       # EncBlob de la base de datos (JSON) en Base64
└── files/
    ├── <docId>.enc    # EncBlob de cada archivo adjunto en Base64
    └── ...
```

### 4.1 `manifest.json`

```json
{
  "format": "securevault",
  "formatVersion": 1,
  "app": "IDPersonalSecure",
  "createdAt": "2026-07-03T12:00:00Z",
  "kdf": "PBKDF2-HMAC-SHA256",
  "iterations": 210000,
  "cipher": "AES-256-GCM",
  "salt": "<base64 16 bytes>",
  "dbMac": "<base64 HMAC-SHA256(macKey, bytes_utf8(database.enc))>",
  "files": ["<docId1>", "<docId2>"]
}
```

`dbMac` se calcula sobre los **bytes UTF-8 del contenido Base64** de `database.enc`.
En la importación se verifica `dbMac` **antes** de descifrar; si no coincide →
`IntegrityError` (archivo corrupto o PIN de otra bóveda).

## 5. Base de datos (plaintext antes de cifrar)

`database.enc` descifrado es un JSON UTF-8:

```json
{
  "schema": 1,
  "documents": [
    {
      "id": "uuid-v4",
      "name": "Cédula de Ciudadanía",
      "type": "CC",
      "country": "CO",
      "number": "1234567890",
      "issueDate": "2015-01-01",
      "expiryDate": "2035-01-01",
      "hasExpiry": true,
      "urlSource": "",
      "fileName": "",
      "notes": "",
      "createdAt": "2026-07-03T12:00:00Z"
    }
  ]
}
```

Un documento con adjunto tiene `fileName` no vacío y un `files/<id>.enc` asociado.

La base de datos incluye además un arreglo opcional `shareLog` (historial interno de
documentos compartidos), con objetos `{ id, docId, docName, tramite, dateTime, recipient, watermarked }`.
Es aditivo: una versión que no lo entienda simplemente lo ignora.

## 6. Tipos de documento (catálogo inicial)

Colombia: `REGISTRO_CIVIL, TI, CC, CE, PASAPORTE, PPT, SC, NIT`.
Multipaís: `DNI, CURP, RUT, PASSPORT, CERT` (configurable/extensible).

## 7. Vectores de prueba (para validar paridad entre plataformas)

Con `PIN = "1357"`, `salt = 00112233445566778899aabbccddeeff` (hex):

```
encKey (hex) = 9e1b... (se documentará al fijar la implementación de referencia)
macKey (hex) = 7c4a...
```

> TODO(v0.2): fijar vectores de prueba oficiales y añadir tests de interoperabilidad
> Android↔Windows en CI (exportar en una plataforma, importar en la otra).

## 8. Reglas invariables

1. El algoritmo y los parámetros son **idénticos** en ambas plataformas.
2. El PIN nunca se persiste ni se registra en logs.
3. Ningún documento se guarda en texto plano en disco.
4. La integridad (`dbMac` + tag GCM) se valida **siempre** en la importación.
5. Cambiar cualquier parámetro obliga a subir `formatVersion`.
