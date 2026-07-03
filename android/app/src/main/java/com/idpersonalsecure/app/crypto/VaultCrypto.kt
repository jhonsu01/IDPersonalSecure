package com.idpersonalsecure.app.crypto

import java.security.SecureRandom
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

/**
 * Implementación del formato criptográfico canónico definido en docs/CRYPTO.md.
 * DEBE mantenerse idéntica a la implementación de Windows (VaultCrypto.cs).
 *
 * EncBlob = nonce(12) || ciphertext(n) || tag(16)   [tag concatenado por AES/GCM]
 */
object VaultCrypto {
    private const val ITERATIONS = 210_000
    private const val KEY_BYTES = 32          // AES-256
    private const val NONCE_BYTES = 12        // GCM nonce
    private const val TAG_BITS = 128          // GCM tag
    private const val SALT_BYTES = 16
    private val rng = SecureRandom()

    data class Keys(val enc: ByteArray, val mac: ByteArray)

    fun randomBytes(n: Int): ByteArray = ByteArray(n).also { rng.nextBytes(it) }
    fun newSalt(): ByteArray = randomBytes(SALT_BYTES)

    /** Deriva 64 bytes: [0..32) = encKey (AES), [32..64) = macKey (HMAC). */
    fun deriveKeys(pin: String, salt: ByteArray): Keys {
        val spec = PBEKeySpec(pin.toCharArray(), salt, ITERATIONS, (KEY_BYTES * 2) * 8)
        val dk = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded
        return Keys(dk.copyOfRange(0, KEY_BYTES), dk.copyOfRange(KEY_BYTES, KEY_BYTES * 2))
    }

    fun encrypt(plain: ByteArray, encKey: ByteArray, aad: String): ByteArray {
        val nonce = randomBytes(NONCE_BYTES)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(encKey, "AES"), GCMParameterSpec(TAG_BITS, nonce))
        cipher.updateAAD(aad.toByteArray(Charsets.UTF_8))
        return nonce + cipher.doFinal(plain)
    }

    fun decrypt(blob: ByteArray, encKey: ByteArray, aad: String): ByteArray {
        val nonce = blob.copyOfRange(0, NONCE_BYTES)
        val ctAndTag = blob.copyOfRange(NONCE_BYTES, blob.size)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(encKey, "AES"), GCMParameterSpec(TAG_BITS, nonce))
        cipher.updateAAD(aad.toByteArray(Charsets.UTF_8))
        return cipher.doFinal(ctAndTag)
    }

    fun hmac(macKey: ByteArray, data: ByteArray): ByteArray {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(macKey, "HmacSHA256"))
        return mac.doFinal(data)
    }

    fun b64(data: ByteArray): String = Base64.getEncoder().encodeToString(data)
    fun unb64(s: String): ByteArray = Base64.getDecoder().decode(s)
}
