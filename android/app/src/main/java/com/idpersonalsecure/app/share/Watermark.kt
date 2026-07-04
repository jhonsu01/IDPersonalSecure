package com.idpersonalsecure.app.share

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Rect
import android.graphics.pdf.PdfDocument
import android.graphics.pdf.PdfRenderer
import android.os.ParcelFileDescriptor
import android.text.TextPaint
import android.text.TextUtils
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.UUID

data class ShareInfo(val tramite: String, val shareId: String, val dateTime: String)

/**
 * Aplica marca de agua (trámite + ID único + fecha/hora) a imágenes y PDF.
 * Usa solo APIs nativas de Android (Canvas, PdfRenderer, PdfDocument).
 */
object Watermark {
    private val imageExts = listOf("png", "jpg", "jpeg", "bmp", "gif", "webp")

    fun newShareId(): String =
        "IDPS-" + UUID.randomUUID().toString().replace("-", "").substring(0, 8).uppercase()

    fun isSupported(name: String): Boolean {
        val ext = name.substringAfterLast('.', "").lowercase()
        return ext == "pdf" || imageExts.contains(ext)
    }

    /** Devuelve Pair(bytes, extensiónConPunto). */
    fun apply(context: Context, input: ByteArray, originalName: String, info: ShareInfo): Pair<ByteArray, String> {
        val ext = originalName.substringAfterLast('.', "").lowercase()
        return if (ext == "pdf") Pair(applyPdf(context, input, info), ".pdf")
        else Pair(applyImage(input, info), ".png")
    }

    private fun applyImage(input: ByteArray, info: ShareInfo): ByteArray {
        val src = BitmapFactory.decodeByteArray(input, 0, input.size)
            ?: throw IllegalArgumentException("Imagen inválida")
        val bmp = src.copy(Bitmap.Config.ARGB_8888, true)
        drawWatermark(Canvas(bmp), bmp.width, bmp.height, info)
        val bos = ByteArrayOutputStream()
        bmp.compress(Bitmap.CompressFormat.PNG, 100, bos)
        return bos.toByteArray()
    }

    private fun applyPdf(context: Context, input: ByteArray, info: ShareInfo): ByteArray {
        val tmp = File.createTempFile("idps_in_", ".pdf", context.cacheDir)
        tmp.writeBytes(input)
        val pfd = ParcelFileDescriptor.open(tmp, ParcelFileDescriptor.MODE_READ_ONLY)
        val renderer = PdfRenderer(pfd)
        val out = PdfDocument()
        val scale = 2
        try {
            for (i in 0 until renderer.pageCount) {
                val page = renderer.openPage(i)
                val pw = page.width
                val ph = page.height
                val bmp = Bitmap.createBitmap(pw * scale, ph * scale, Bitmap.Config.ARGB_8888)
                bmp.eraseColor(Color.WHITE)
                page.render(bmp, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                page.close()
                drawWatermark(Canvas(bmp), bmp.width, bmp.height, info)

                val pageInfo = PdfDocument.PageInfo.Builder(pw, ph, i).create()
                val outPage = out.startPage(pageInfo)
                outPage.canvas.drawBitmap(bmp, null, Rect(0, 0, pw, ph), null)
                out.finishPage(outPage)
                bmp.recycle()
            }
            val bos = ByteArrayOutputStream()
            out.writeTo(bos)
            return bos.toByteArray()
        } finally {
            out.close(); renderer.close(); pfd.close(); tmp.delete()
        }
    }

    private fun drawWatermark(canvas: Canvas, w: Int, h: Int, info: ShareInfo) {
        val fs = maxOf(16f, minOf(w, h) / 18f)
        val tilePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.argb(70, 190, 40, 40)
            textSize = fs
        }
        canvas.save()
        canvas.rotate(-30f, w / 2f, h / 2f)
        val stepX = fs * (info.tramite.length + 6) * 0.62f
        val stepY = fs * 3.2f
        var y = -h.toFloat()
        while (y < 2 * h) {
            var x = -w.toFloat()
            while (x < 2 * w) { canvas.drawText(info.tramite, x, y, tilePaint); x += stepX }
            y += stepY
        }
        canvas.restore()

        val band = maxOf(30f, h * 0.07f)
        canvas.drawRect(0f, h - band, w.toFloat(), h.toFloat(), Paint().apply { color = Color.argb(160, 0, 0, 0) })
        val footPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; textSize = maxOf(11f, band * 0.42f) }
        val footer = "ID: ${info.shareId}   |   ${info.dateTime}   |   ${info.tramite}"
        val text = TextUtils.ellipsize(footer, TextPaint(footPaint), w - 20f, TextUtils.TruncateAt.END).toString()
        canvas.drawText(text, 10f, h - band + band * 0.68f, footPaint)
    }
}
