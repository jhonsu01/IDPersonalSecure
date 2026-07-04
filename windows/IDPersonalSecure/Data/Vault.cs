using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IDPersonalSecure.Crypto;

namespace IDPersonalSecure.Data;

public sealed class IntegrityException(string message) : Exception(message);

public sealed record DocType(string Code, string Label, string Country);

public static class DocumentCatalog
{
    public static readonly IReadOnlyList<DocType> Types = new List<DocType>
    {
        new("REGISTRO_CIVIL", "Registro Civil", "CO"),
        new("TI", "Tarjeta de Identidad", "CO"),
        new("CC", "Cédula de Ciudadanía", "CO"),
        new("CE", "Cédula de Extranjería", "CO"),
        new("PASAPORTE", "Pasaporte", "CO"),
        new("PPT", "Permiso por Protección Temporal", "CO"),
        new("SC", "Salvoconducto", "CO"),
        new("NIT", "NIT", "CO"),
        new("DNI", "DNI", "XX"),
        new("CURP", "CURP", "MX"),
        new("RUT", "RUT", "CL"),
        new("PASSPORT", "Passport", "XX"),
        new("CERT", "Certificado", "XX"),
    };

    public static string Label(string code) =>
        Types.FirstOrDefault(t => t.Code == code)?.Label ?? code;
}

public sealed class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Type { get; set; } = "CC";
    public string Country { get; set; } = "CO";
    public string Number { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public string ExpiryDate { get; set; } = "";
    public bool HasExpiry { get; set; }
    public string UrlSource { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Notes { get; set; } = "";
    public string ReminderAt { get; set; } = "";
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    [JsonIgnore] public string TypeLabel => DocumentCatalog.Label(Type);
    [JsonIgnore] public string AttachmentBadge => string.IsNullOrEmpty(FileName) ? "" : "📎";
    [JsonIgnore] public bool HasUrl => !string.IsNullOrWhiteSpace(UrlSource);
    [JsonIgnore] public string Subtitle => $"{TypeLabel} · {Country}" + (string.IsNullOrEmpty(Number) ? "" : $" · N.º {Number}");
    [JsonIgnore] public bool IsExpired => HasExpiry && DateTime.TryParse(ExpiryDate, out var d) && d.Date < DateTime.Today;
    [JsonIgnore] public string ExpiryDisplay =>
        HasExpiry && !string.IsNullOrEmpty(ExpiryDate) ? (IsExpired ? $"Vencido: {ExpiryDate}" : $"Vence: {ExpiryDate}") : "";

    public Document Clone() => (Document)MemberwiseClone();
}

/// <summary>Registro de un documento compartido (historial interno).</summary>
public sealed class ShareRecord
{
    public string Id { get; set; } = "";
    public string DocId { get; set; } = "";
    public string DocName { get; set; } = "";
    public string Tramite { get; set; } = "";
    public string DateTime { get; set; } = "";
    public string Recipient { get; set; } = "";
    public bool Watermarked { get; set; }

    [JsonIgnore] public string Line1 => $"{DocName}  ·  {DateTime}";
    [JsonIgnore] public string Line2 => (Watermarked ? $"ID {Id}" : "sin marca de agua") +
        (string.IsNullOrWhiteSpace(Tramite) ? "" : $"  ·  {Tramite}");
    [JsonIgnore] public string RecipientDisplay => string.IsNullOrWhiteSpace(Recipient) ? "Destinatario: —" : $"Destinatario: {Recipient}";
}

public sealed class VaultDb
{
    public int Schema { get; set; } = 1;
    public List<Document> Documents { get; set; } = new();
    public List<ShareRecord> ShareLog { get; set; } = new();
}

file sealed class Manifest
{
    public string Format { get; set; } = "securevault";
    public int FormatVersion { get; set; } = 1;
    public string App { get; set; } = "IDPersonalSecure";
    public string CreatedAt { get; set; } = "";
    public string Kdf { get; set; } = "PBKDF2-HMAC-SHA256";
    public int Iterations { get; set; } = 210_000;
    public string Cipher { get; set; } = "AES-256-GCM";
    public string Salt { get; set; } = "";
    public string DbMac { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

/// <summary>
/// Bóveda local cifrada + export/import de `.securevault`.
/// Mismo esquema que Android (docs/CRYPTO.md) para interoperabilidad directa.
/// </summary>
public sealed class VaultRepository
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _dir;
    private string DbPath => Path.Combine(_dir, "vault.db.enc");
    private string SaltPath => Path.Combine(_dir, "vault.salt");
    private string AttachDir => Path.Combine(_dir, "attachments");
    private string AttachPath(string id) => Path.Combine(AttachDir, $"{id}.enc");

    public byte[] Salt { get; private set; } = Array.Empty<byte>();
    private VaultCrypto.Keys? _keys;
    public ObservableCollection<Document> Documents { get; } = new();
    public ObservableCollection<ShareRecord> ShareLog { get; } = new();

    public VaultRepository()
    {
        _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IDPersonalSecure");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(AttachDir);
    }

    // ── Adjuntos cifrados (files/<id>.enc, AAD = id) ─────────────────────
    public bool HasAttachment(string id) => File.Exists(AttachPath(id));

    public void SaveAttachment(string id, byte[] data)
    {
        if (_keys is null) throw new InvalidOperationException("Bóveda bloqueada");
        Directory.CreateDirectory(AttachDir);
        File.WriteAllBytes(AttachPath(id), VaultCrypto.Encrypt(data, _keys.Enc, id));
    }

    public byte[]? ReadAttachment(string id)
    {
        if (_keys is null || !HasAttachment(id)) return null;
        return VaultCrypto.Decrypt(File.ReadAllBytes(AttachPath(id)), _keys.Enc, id);
    }

    public void DeleteAttachment(string id)
    {
        if (File.Exists(AttachPath(id))) File.Delete(AttachPath(id));
    }

    public bool VaultExists() => File.Exists(SaltPath);
    public bool IsUnlocked => _keys is not null;

    public bool Unlock(string pin)
    {
        Salt = File.Exists(SaltPath) ? File.ReadAllBytes(SaltPath) : CreateSalt();
        var k = VaultCrypto.DeriveKeys(pin, Salt);
        Documents.Clear();
        if (File.Exists(DbPath))
        {
            try
            {
                string json = Encoding.UTF8.GetString(VaultCrypto.Decrypt(File.ReadAllBytes(DbPath), k.Enc, "database"));
                LoadJson(json);
            }
            catch
            {
                return false; // PIN incorrecto o datos corruptos
            }
        }
        _keys = k;
        return true;
    }

    public void Lock() { _keys = null; Documents.Clear(); }

    private byte[] CreateSalt()
    {
        var s = VaultCrypto.NewSalt();
        File.WriteAllBytes(SaltPath, s);
        return s;
    }

    private void LoadJson(string json)
    {
        Documents.Clear();
        ShareLog.Clear();
        var db = JsonSerializer.Deserialize<VaultDb>(json, Json) ?? new VaultDb();
        foreach (var d in db.Documents) Documents.Add(d);
        foreach (var s in db.ShareLog) ShareLog.Add(s);
    }

    private byte[] SerializeDb() =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new VaultDb { Documents = Documents.ToList(), ShareLog = ShareLog.ToList() }, Json));

    public void AddShareRecord(ShareRecord rec) { ShareLog.Insert(0, rec); Save(); }

    public void UpdateShareRecipient(string id, string recipient)
    {
        var r = ShareLog.FirstOrDefault(x => x.Id == id);
        if (r != null) { r.Recipient = recipient; Save(); }
    }

    public void Save()
    {
        if (_keys is null) return;
        File.WriteAllBytes(DbPath, VaultCrypto.Encrypt(SerializeDb(), _keys.Enc, "database"));
    }

    public void Upsert(Document doc)
    {
        var idx = Documents.ToList().FindIndex(d => d.Id == doc.Id);
        if (idx >= 0) Documents[idx] = doc; else Documents.Add(doc);
        Save();
    }

    public void Delete(string id)
    {
        var existing = Documents.FirstOrDefault(d => d.Id == id);
        if (existing != null) Documents.Remove(existing);
        DeleteAttachment(id);
        Save();
    }

    public void Export(Stream outStream)
    {
        if (_keys is null) throw new InvalidOperationException("Bóveda bloqueada");
        var dbBlob = VaultCrypto.Encrypt(SerializeDb(), _keys.Enc, "database");
        string dbB64 = VaultCrypto.B64(dbBlob);
        string dbMac = VaultCrypto.B64(VaultCrypto.Hmac(_keys.Mac, Encoding.UTF8.GetBytes(dbB64)));
        var manifest = new Manifest
        {
            CreatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Salt = VaultCrypto.B64(Salt),
            DbMac = dbMac,
        };
        var fileIds = Documents.Where(d => HasAttachment(d.Id)).Select(d => d.Id).ToList();
        manifest.Files = fileIds;

        using var zip = new ZipArchive(outStream, ZipArchiveMode.Create, leaveOpen: true);
        using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Utf8NoBom))
            w.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(Json) { WriteIndented = true }));
        using (var w = new StreamWriter(zip.CreateEntry("database.enc").Open(), Utf8NoBom))
            w.Write(dbB64);
        foreach (var id in fileIds)
        {
            string blobB64 = VaultCrypto.B64(File.ReadAllBytes(AttachPath(id)));
            using var w = new StreamWriter(zip.CreateEntry($"files/{id}.enc").Open(), Utf8NoBom);
            w.Write(blobB64);
        }
    }

    public void Import(Stream input, string pin)
    {
        string? manifestText = null, dbB64 = null;
        var fileBlobs = new Dictionary<string, string>();
        using (var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var e in zip.Entries)
            {
                using var r = new StreamReader(e.Open(), Encoding.UTF8);
                string content = r.ReadToEnd();
                if (e.FullName == "manifest.json") manifestText = content;
                else if (e.FullName == "database.enc") dbB64 = content;
                else if (e.FullName.StartsWith("files/") && e.FullName.EndsWith(".enc"))
                    fileBlobs[Path.GetFileNameWithoutExtension(e.Name)] = content;
            }
        }
        if (manifestText is null) throw new IntegrityException("manifest.json ausente");
        if (dbB64 is null) throw new IntegrityException("database.enc ausente");

        using var doc = JsonDocument.Parse(manifestText);
        var root = doc.RootElement;
        byte[] impSalt = VaultCrypto.Unb64(root.GetProperty("salt").GetString()!);
        string expectMac = root.GetProperty("dbMac").GetString()!;
        var k = VaultCrypto.DeriveKeys(pin, impSalt);
        string actualMac = VaultCrypto.B64(VaultCrypto.Hmac(k.Mac, Encoding.UTF8.GetBytes(dbB64)));
        if (!VaultCrypto.ConstantTimeEquals(expectMac, actualMac))
            throw new IntegrityException("Integridad inválida o PIN incorrecto");

        string json = Encoding.UTF8.GetString(VaultCrypto.Decrypt(VaultCrypto.Unb64(dbB64), k.Enc, "database"));
        Salt = impSalt;
        File.WriteAllBytes(SaltPath, impSalt);
        _keys = k;
        LoadJson(json);
        Save();

        // Restaura adjuntos: limpia los previos y escribe los importados (misma clave).
        if (Directory.Exists(AttachDir))
            foreach (var f in Directory.GetFiles(AttachDir)) File.Delete(f);
        Directory.CreateDirectory(AttachDir);
        foreach (var (id, b64) in fileBlobs)
            File.WriteAllBytes(AttachPath(id), VaultCrypto.Unb64(b64));
    }
}
