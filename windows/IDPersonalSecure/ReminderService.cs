using System.Linq;
using IDPersonalSecure.Data;

namespace IDPersonalSecure;

/// <summary>
/// Revisa recordatorios vencidos y lanza notificaciones. En Windows el aviso ocurre
/// mientras la app está abierta y al abrirla (para segundo plano completo haría falta
/// registrar una tarea programada — pendiente en el roadmap).
/// </summary>
public static class ReminderService
{
    private static readonly HashSet<string> Notified = new();

    public static void CheckDue(VaultRepository repo)
    {
        var now = DateTime.Now;
        foreach (var d in repo.Documents.ToList())
        {
            if (string.IsNullOrWhiteSpace(d.ReminderAt) || Notified.Contains(d.Id)) continue;
            if (DateTime.TryParse(d.ReminderAt, out var when) && when <= now)
            {
                Notified.Add(d.Id);
                string msg = string.IsNullOrEmpty(d.ExpiryDate)
                    ? $"Recordatorio: {d.Name}"
                    : $"{d.Name} — vence el {d.ExpiryDate}";
                App.Notify("Recordatorio de documento", msg);
            }
        }
    }
}
