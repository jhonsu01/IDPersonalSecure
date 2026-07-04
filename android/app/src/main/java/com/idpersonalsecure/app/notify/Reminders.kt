package com.idpersonalsecure.app.notify

import android.app.AlarmManager
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.idpersonalsecure.app.R

/** Canal de notificaciones de recordatorios. */
object Notifications {
    const val CHANNEL = "reminders"

    fun ensureChannel(context: Context) {
        val mgr = context.getSystemService(NotificationManager::class.java)
        if (mgr.getNotificationChannel(CHANNEL) == null) {
            mgr.createNotificationChannel(
                NotificationChannel(CHANNEL, "Recordatorios de documentos", NotificationManager.IMPORTANCE_HIGH)
            )
        }
    }
}

/** Programa/cancela alarmas locales que disparan la notificación de recordatorio. */
object ReminderScheduler {
    fun schedule(context: Context, docId: String, title: String, text: String, whenMillis: Long) {
        if (whenMillis <= System.currentTimeMillis()) return
        val am = context.getSystemService(AlarmManager::class.java)
        am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, whenMillis, pending(context, docId, title, text))
    }

    fun cancel(context: Context, docId: String) {
        val am = context.getSystemService(AlarmManager::class.java)
        am.cancel(pending(context, docId, "", ""))
    }

    private fun pending(context: Context, docId: String, title: String, text: String): PendingIntent {
        val intent = Intent(context, ReminderReceiver::class.java).apply {
            putExtra("docId", docId); putExtra("title", title); putExtra("text", text)
        }
        return PendingIntent.getBroadcast(
            context, docId.hashCode(), intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }
}

/** Recibe la alarma y publica la notificación en el teléfono. */
class ReminderReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        Notifications.ensureChannel(context)
        val title = intent.getStringExtra("title") ?: "Recordatorio"
        val text = intent.getStringExtra("text") ?: ""
        val notif = NotificationCompat.Builder(context, Notifications.CHANNEL)
            .setSmallIcon(R.mipmap.ic_launcher)
            .setContentTitle(title)
            .setContentText(text)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
        val id = (intent.getStringExtra("docId") ?: "0").hashCode()
        try {
            NotificationManagerCompat.from(context).notify(id, notif)
        } catch (e: SecurityException) {
            // Sin permiso POST_NOTIFICATIONS: se ignora silenciosamente.
        }
    }
}
