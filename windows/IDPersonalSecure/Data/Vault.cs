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
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    [JsonIgnore] public string TypeLabel => DocumentCatalog.Label(Type);
    [JsonIgnore] public string Subtitle => $"{TypeLabel} · {Country}" + (string.IsNullOrEmpty(Number) ? "" : $" · N.º {Number}");
    [JsonIgnore] public bool IsExpired => HasExpiry && DateTime.TryParse(ExpiryDate, out var d) && d.Date < DateTime.Today;
    [JsonIgnore] public string ExpiryDisplay =>
        HasExpiry && !string.IsNullOrEmpty(ExpiryDate) ? (IsExpired ? $"Vencido: {ExpiryDate}" : $"Vence: {ExpiryDate}") : "";

    public Document Clone() => (Document)MemberwiseClone();
}

public sealed class VaultDb
{
    public int Schema { get; set; } = 1;
    public List<Document> Documents { get; set; } = new();
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

    public byte[] Salt { get; private set; } = Array.Empty<byte>();
    private VaultCrypto.Keys? _keys;
    public ObservableCollection<Document> Documents { get; } = new();

    public VaultRepository()
    {
        _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IDPersonalSecure");
        Directory.CreateDirectory(_dir);
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
        var db = JsonSerializer.Deserialize<VaultDb>(json, Json) ?? new VaultDb();
        foreach (var d in db.Documents) Documents.Add(d);
    }

    private byte[] SerializeDb() =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new VaultDb { Documents = Documents.ToList() }, Json));

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
        using var zip = new ZipArchive(outStream, ZipArchiveMode.Create, leaveOpen: true);
        using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Utf8NoBom))
            w.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(Json) { WriteIndented = true }));
        using (var w = new StreamWriter(zip.CreateEntry("database.enc").Open(), Utf8NoBom))
            w.Write(dbB64);
    }

    public void Import(Stream input, string pin)
    {
        string? manifestText = null, dbB64 = null;
        using (var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var e in zip.Entries)
            {
                using var r = new StreamReader(e.Open(), Encoding.UTF8);
                string content = r.ReadToEnd();
                if (e.FullName == "manifest.json") manifestText = content;
                else if (e.FullName == "database.enc") dbB64 = content;
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
    }
}
