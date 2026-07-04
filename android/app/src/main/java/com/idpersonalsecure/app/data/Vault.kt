package com.idpersonalsecure.app.data

import android.content.Context
import com.idpersonalsecure.app.crypto.VaultCrypto
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.InputStream
import java.io.OutputStream
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import java.util.zip.ZipEntry
import java.util.zip.ZipInputStream
import java.util.zip.ZipOutputStream

fun nowIso(): String = OffsetDateTime.now(ZoneOffset.UTC).withNano(0).toString()

class IntegrityException(msg: String) : Exception(msg)

/** Tipo de documento del catálogo (Colombia + multipaís). */
data class DocType(val code: String, val label: String, val country: String)

object DocumentCatalog {
    val types: List<DocType> = listOf(
        DocType("REGISTRO_CIVIL", "Registro Civil", "CO"),
        DocType("TI", "Tarjeta de Identidad", "CO"),
        DocType("CC", "Cédula de Ciudadanía", "CO"),
        DocType("CE", "Cédula de Extranjería", "CO"),
        DocType("PASAPORTE", "Pasaporte", "CO"),
        DocType("PPT", "Permiso por Protección Temporal", "CO"),
        DocType("SC", "Salvoconducto", "CO"),
        DocType("NIT", "NIT", "CO"),
        DocType("DNI", "DNI", "XX"),
        DocType("CURP", "CURP", "MX"),
        DocType("RUT", "RUT", "CL"),
        DocType("PASSPORT", "Passport", "XX"),
        DocType("CERT", "Certificado", "XX"),
    )
    fun label(code: String): String = types.firstOrNull { it.code == code }?.label ?: code
}

data class Document(
    val id: String = UUID.randomUUID().toString(),
    var name: String = "",
    var type: String = "CC",
    var country: String = "CO",
    var number: String = "",
    var issueDate: String = "",
    var expiryDate: String = "",
    var hasExpiry: Boolean = false,
    var urlSource: String = "",
    var fileName: String = "",
    var notes: String = "",
    val createdAt: String = nowIso(),
) {
    fun toJson(): JSONObject = JSONObject().apply {
        put("id", id); put("name", name); put("type", type); put("country", country)
        put("number", number); put("issueDate", issueDate); put("expiryDate", expiryDate)
        put("hasExpiry", hasExpiry); put("urlSource", urlSource); put("fileName", fileName)
        put("notes", notes); put("createdAt", createdAt)
    }

    companion object {
        fun fromJson(o: JSONObject) = Document(
            id = o.optString("id", UUID.randomUUID().toString()),
            name = o.optString("name"),
            type = o.optString("type", "CC"),
            country = o.optString("country", "CO"),
            number = o.optString("number"),
            issueDate = o.optString("issueDate"),
            expiryDate = o.optString("expiryDate"),
            hasExpiry = o.optBoolean("hasExpiry", false),
            urlSource = o.optString("urlSource"),
            fileName = o.optString("fileName"),
            notes = o.optString("notes"),
            createdAt = o.optString("createdAt", nowIso()),
        )
    }
}

/** Registro de un documento compartido (historial interno). */
data class ShareRecord(
    val id: String,
    val docId: String,
    val docName: String,
    val tramite: String,
    val dateTime: String,
    var recipient: String,
    val watermarked: Boolean,
) {
    fun toJson(): JSONObject = JSONObject().apply {
        put("id", id); put("docId", docId); put("docName", docName); put("tramite", tramite)
        put("dateTime", dateTime); put("recipient", recipient); put("watermarked", watermarked)
    }

    companion object {
        fun fromJson(o: JSONObject) = ShareRecord(
            id = o.optString("id"), docId = o.optString("docId"), docName = o.optString("docName"),
            tramite = o.optString("tramite"), dateTime = o.optString("dateTime"),
            recipient = o.optString("recipient"), watermarked = o.optBoolean("watermarked", false),
        )
    }
}

/**
 * Bóveda local cifrada + export/import de `.securevault`.
 * El almacén local usa el MISMO esquema que el archivo exportado (docs/CRYPTO.md).
 */
class VaultRepository(context: Context) {
    private val filesDir: File = context.filesDir
    private val dbFile get() = File(filesDir, "vault.db.enc")
    private val saltFile get() = File(filesDir, "vault.salt")
    private val attachDir get() = File(filesDir, "attachments").apply { mkdirs() }
    private fun attachFile(id: String) = File(attachDir, "$id.enc")

    var salt: ByteArray = ByteArray(0); private set
    private var keys: VaultCrypto.Keys? = null
    val documents = mutableListOf<Document>()
    val shareLog = mutableListOf<ShareRecord>()

    fun vaultExists(): Boolean = saltFile.exists()
    val isUnlocked: Boolean get() = keys != null

    /** Desbloquea (o crea) la bóveda local con el PIN. false si el PIN es incorrecto. */
    fun unlock(pin: String): Boolean {
        salt = if (saltFile.exists()) saltFile.readBytes()
        else VaultCrypto.newSalt().also { saltFile.writeBytes(it) }
        val k = VaultCrypto.deriveKeys(pin, salt)
        documents.clear()
        if (dbFile.exists()) {
            try {
                loadJson(String(VaultCrypto.decrypt(dbFile.readBytes(), k.enc, "database"), Charsets.UTF_8))
            } catch (e: Exception) {
                return false
            }
        }
        keys = k
        return true
    }

    fun lock() { keys = null; documents.clear() }

    private fun loadJson(json: String) {
        documents.clear()
        shareLog.clear()
        val root = JSONObject(json)
        val arr = root.optJSONArray("documents") ?: JSONArray()
        for (i in 0 until arr.length()) documents.add(Document.fromJson(arr.getJSONObject(i)))
        val log = root.optJSONArray("shareLog") ?: JSONArray()
        for (i in 0 until log.length()) shareLog.add(ShareRecord.fromJson(log.getJSONObject(i)))
    }

    private fun buildDbJson(): String {
        val arr = JSONArray()
        documents.forEach { arr.put(it.toJson()) }
        val log = JSONArray()
        shareLog.forEach { log.put(it.toJson()) }
        return JSONObject().put("schema", 1).put("documents", arr).put("shareLog", log).toString()
    }

    fun addShareRecord(rec: ShareRecord) { shareLog.add(0, rec); save() }

    fun updateShareRecipient(id: String, recipient: String) {
        shareLog.firstOrNull { it.id == id }?.let { it.recipient = recipient; save() }
    }

    fun save() {
        val k = keys ?: return
        dbFile.writeBytes(VaultCrypto.encrypt(buildDbJson().toByteArray(Charsets.UTF_8), k.enc, "database"))
    }

    fun upsert(doc: Document) {
        val idx = documents.indexOfFirst { it.id == doc.id }
        if (idx >= 0) documents[idx] = doc else documents.add(doc)
        save()
    }

    fun delete(id: String) { documents.removeAll { it.id == id }; deleteAttachment(id); save() }

    // ── Adjuntos cifrados (files/<id>.enc, AAD = id) ─────────────────────
    fun hasAttachment(id: String): Boolean = attachFile(id).exists()

    fun saveAttachment(id: String, data: ByteArray) {
        val k = keys ?: throw IllegalStateException("Bóveda bloqueada")
        attachFile(id).writeBytes(VaultCrypto.encrypt(data, k.enc, id))
    }

    fun readAttachment(id: String): ByteArray? {
        val k = keys ?: return null
        val f = attachFile(id)
        return if (f.exists()) VaultCrypto.decrypt(f.readBytes(), k.enc, id) else null
    }

    fun deleteAttachment(id: String) { attachFile(id).delete() }

    /** Exporta la bóveda actual a un `.securevault`. */
    fun export(out: OutputStream) {
        val k = keys ?: throw IllegalStateException("Bóveda bloqueada")
        val dbBlob = VaultCrypto.encrypt(buildDbJson().toByteArray(Charsets.UTF_8), k.enc, "database")
        val dbB64 = VaultCrypto.b64(dbBlob)
        val dbMac = VaultCrypto.b64(VaultCrypto.hmac(k.mac, dbB64.toByteArray(Charsets.UTF_8)))
        val fileIds = documents.filter { hasAttachment(it.id) }.map { it.id }
        val manifest = JSONObject().apply {
            put("format", "securevault"); put("formatVersion", 1); put("app", "IDPersonalSecure")
            put("createdAt", nowIso()); put("kdf", "PBKDF2-HMAC-SHA256"); put("iterations", 210000)
            put("cipher", "AES-256-GCM"); put("salt", VaultCrypto.b64(salt)); put("dbMac", dbMac)
            put("files", JSONArray(fileIds))
        }
        ZipOutputStream(out).use { zip ->
            zip.putNextEntry(ZipEntry("manifest.json"))
            zip.write(manifest.toString(2).toByteArray(Charsets.UTF_8)); zip.closeEntry()
            zip.putNextEntry(ZipEntry("database.enc"))
            zip.write(dbB64.toByteArray(Charsets.UTF_8)); zip.closeEntry()
            for (id in fileIds) {
                val blobB64 = VaultCrypto.b64(attachFile(id).readBytes())
                zip.putNextEntry(ZipEntry("files/$id.enc"))
                zip.write(blobB64.toByteArray(Charsets.UTF_8)); zip.closeEntry()
            }
        }
    }

    /** Importa un `.securevault`: valida integridad (HMAC) y reemplaza la bóveda local. */
    fun import(input: InputStream, pin: String) {
        var manifestText: String? = null
        var dbB64: String? = null
        val fileBlobs = HashMap<String, String>()
        ZipInputStream(input).use { zip ->
            var e: ZipEntry? = zip.nextEntry
            while (e != null) {
                val bytes = readAll(zip)
                val name = e.name
                when {
                    name == "manifest.json" -> manifestText = String(bytes, Charsets.UTF_8)
                    name == "database.enc" -> dbB64 = String(bytes, Charsets.UTF_8)
                    name.startsWith("files/") && name.endsWith(".enc") ->
                        fileBlobs[name.substringAfterLast('/').removeSuffix(".enc")] = String(bytes, Charsets.UTF_8)
                }
                zip.closeEntry(); e = zip.nextEntry
            }
        }
        val manifest = JSONObject(manifestText ?: throw IntegrityException("manifest.json ausente"))
        val impSalt = VaultCrypto.unb64(manifest.getString("salt"))
        val db = dbB64 ?: throw IntegrityException("database.enc ausente")
        val k = VaultCrypto.deriveKeys(pin, impSalt)
        val expectMac = manifest.getString("dbMac")
        val actualMac = VaultCrypto.b64(VaultCrypto.hmac(k.mac, db.toByteArray(Charsets.UTF_8)))
        if (!constantTimeEquals(expectMac, actualMac)) throw IntegrityException("Integridad inválida o PIN incorrecto")
        val json = String(VaultCrypto.decrypt(VaultCrypto.unb64(db), k.enc, "database"), Charsets.UTF_8)
        salt = impSalt; saltFile.writeBytes(impSalt); keys = k
        loadJson(json); save()

        // Restaura adjuntos: limpia los previos y escribe los importados (misma clave).
        attachDir.listFiles()?.forEach { it.delete() }
        for ((id, b64) in fileBlobs) attachFile(id).writeBytes(VaultCrypto.unb64(b64))
    }

    private fun constantTimeEquals(a: String, b: String): Boolean {
        val x = a.toByteArray(); val y = b.toByteArray()
        if (x.size != y.size) return false
        var r = 0
        for (i in x.indices) r = r or (x[i].toInt() xor y[i].toInt())
        return r == 0
    }

    private fun readAll(input: InputStream): ByteArray {
        val bos = ByteArrayOutputStream(); val buf = ByteArray(8192); var n: Int
        while (input.read(buf).also { n = it } >= 0) bos.write(buf, 0, n)
        return bos.toByteArray()
    }
}
