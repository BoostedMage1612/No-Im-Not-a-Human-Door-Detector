using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DoorDetector;

public static class Policy
{
    // These IDs are not single ordinary guests. No guessed group/special verdicts.
    public static bool Supported(int type) => type >= 0 && type <= 74 &&
        type != 0 && type != 1 && type != 20 && type != 46 && type != 58 && type != 59 && type != 60;
    public static bool Roll(int visitors, int population, float min, float max, Func<bool> coin)
    {
        if (population <= 0 || visitors < 0 || visitors > population ||
            !float.IsFinite(min) || !float.IsFinite(max) || min < 0 || max > 1 || min > max)
            throw new InvalidDataException("Invalid population or randomization settings.");
        float fraction = (float)visitors / population;
        if (fraction < min) return true;
        if (fraction > max) return false;
        return coin();
    }
    public static string Key(int day, int type) => day + ":" + type;
    public static string Fingerprint(string main, string reserve)
    {
        var a = Canonical(main); var b = Canonical(reserve);
        if (a == "{}" && b == "{}") return "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(a + "\n" + b)));
    }
    private static string Canonical(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Null) return "{}";
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Save snapshot is not an object.");
        var values = new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Name == "$type") continue;
            if (p.Value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Unexpected save snapshot value.");
            values.Add(p.Name, p.Value.GetString()!);
        }
        return JsonSerializer.Serialize(values);
    }
}

public sealed class Ledger
{
    public sealed class Database
    {
        public int Version { get; set; } = 1;
        public Dictionary<string,Dictionary<string,bool>> Snapshots { get; set; } = new();
    }
    private readonly string path;
    private readonly Database database;
    private string anchor = "";
    public Dictionary<string,bool> Current { get; private set; } = new();
    public Ledger(string path)
    {
        this.path = path;
        database = File.Exists(path) ? JsonSerializer.Deserialize<Database>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty reservation database.") : new Database();
        if (database.Version != 1 || database.Snapshots == null || database.Snapshots.Any(p => p.Value == null))
            throw new InvalidDataException("Unrecognized reservation database.");
    }
    public void Load(string snapshot)
    {
        anchor = snapshot;
        Current = snapshot.Length != 0 && database.Snapshots.TryGetValue(snapshot, out var saved)
            ? new Dictionary<string,bool>(saved) : new();
    }
    public bool Reserve(string key, Func<bool> roll)
    {
        if (Current.TryGetValue(key, out bool existing)) return existing;
        bool value = roll();
        Current.Add(key, value);
        try { Persist(); }
        catch { Current.Remove(key); throw; }
        return value;
    }
    public void Bind(string snapshot)
    {
        // Save a copy, so later encounters cannot mutate unrelated historical checkpoints.
        anchor = snapshot;
        Persist();
    }
    private void Persist()
    {
        if (anchor.Length != 0) database.Snapshots[anchor] = new(Current);
        // Empty saves are never shared across new runs. Still exercise write access now.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(database));
        File.Move(temp, path, true);
    }
}

// Shared by the native admission hook and lifecycle tests. The random marker is
// temporary; the identity becomes permanent only when the game admits the guest.
public sealed class IdentityPin:IDisposable
{
    readonly Action<byte> setRandom;
    readonly Action<bool> setIdentity;
    readonly byte priorRandom;
    readonly bool priorIdentity;
    bool active;
    public IdentityPin(Func<byte> getRandom,Action<byte> setRandom,Func<bool> getIdentity,Action<bool> setIdentity,bool assigned)
    {
        this.setRandom=setRandom; this.setIdentity=setIdentity;
        priorRandom=getRandom(); priorIdentity=getIdentity();
        try { active=true; setRandom(0); setIdentity(assigned); }
        catch { Dispose(); setIdentity(priorIdentity); throw; }
    }
    public void AdmissionRejected() { Dispose(); setIdentity(priorIdentity); }
    public void Dispose() { if(active) { setRandom(priorRandom); active=false; } }
}
