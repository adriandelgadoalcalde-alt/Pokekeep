// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Caching;

/// <summary>
/// Clave de cache de generacion (plan 8): hash del input + destino/banderas/entrenador pertinentes + version de politica + release + snapshot + perfil de reglas,
/// y ademas el hash del Core y el commit de ALM cargados. Cambiar CUALQUIERA invalida la entrada. No entran el requestId, el plazo ni la seleccion de la UI.
/// </summary>
public static class GenerationCacheKey
{
    public static string Compute(LegalizeRequest r, string coreDllSha256, string almCommit, string ruleProfileHash)
    {
        var sb = new StringBuilder(512);
        void Add(string k, object? v) => sb.Append(k).Append('=').Append(v).Append('\n');
        Add("input", r.OriginalEntitySha256.ToUpperInvariant());
        Add("type", r.EntityRuntimeType);
        Add("context", r.EntityContext);
        Add("action", r.Action);
        Add("game", r.Target.GameVersion);
        Add("targetType", r.Target.SaveRuntimeType);
        Add("targetContext", r.Target.Context);
        Add("slot", r.Target.SlotType);
        Add("real", r.Target.IsRealSave);
        Add("ot", r.Target.Trainer.Ot);
        Add("tid", r.Target.Trainer.Tid16);
        Add("sid", r.Target.Trainer.Sid16);
        Add("lang", r.Target.Trainer.Language);
        Add("gender", r.Target.Trainer.Gender);
        Add("personal", r.Target.PersonalTableKey);
        if (r.Target.SaveFlags is { } f)
            foreach (var kv in f.OrderBy(k => k.Key, StringComparer.Ordinal)) Add("flag:" + kv.Key, kv.Value);
        Add("optimize", r.Options?.OptimizeStats ?? true);
        Add("history", r.Options?.CoherentHistory ?? true);
        Add("requestedLanguage", r.Options?.RequestedLanguage ?? 0);   // ADR 0046: un resultado en japonés nunca se sirve a quien pide español (ni al revés)
        Add("trackerless", r.Options?.AllowHomeTrackerless ?? false);
        Add("maxAttempts", r.Options?.MaxAttemptsPerEncounter ?? 0);
        Add("event", r.OriginEventKey);
        Add("policy", r.PolicyVersion);
        Add("release", r.ReleaseId);
        Add("snapshot", r.DatasetSnapshotId);
        Add("ruleProfile", ruleProfileHash);
        Add("core", coreDllSha256.ToUpperInvariant());
        Add("alm", almCommit);
        Add("engine", PinnedVersions.EngineVersion);
        Add("adapter", PinnedVersions.AdapterVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}

/// <summary>
/// Cache en memoria de resultados Success. Un acierto SIEMPRE se revalida con el validador oficial independiente (el candidato guardado se reconstruye desde sus bytes
/// y se analiza otra vez contra el destino): una entrada que ya no valida se expulsa y se vuelve a generar. Nunca se cachea un fallo.
/// </summary>
public sealed class CachedLegalizationService(ILegalizationService inner, IOfficialValidator validator, string coreDllSha256, string ruleProfileHash) : ILegalizationService
{
    private readonly ConcurrentDictionary<string, LegalizeResponse> _cache = new(StringComparer.Ordinal);

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public string KeyOf(LegalizeRequest r) => GenerationCacheKey.Compute(r, coreDllSha256, PinnedVersions.AlmCommit, ruleProfileHash);

    /// <summary>SOLO PRUEBAS: guarda una respuesta bajo su clave para comprobar que se revalida.</summary>
    public void InjectForTests(LegalizeRequest r, LegalizeResponse response) => _cache[KeyOf(r)] = response;

    public LegalizeResponse Execute(LegalizeRequest request, CancellationToken cancellation = default)
    {
        var key = KeyOf(request);
        if (_cache.TryGetValue(key, out var cached))
        {
            if (Revalidate(request, cached))
            {
                Hits++;
                return cached with { RequestId = request.RequestId, ElapsedMilliseconds = 0, Message = cached.Message + " (resultado en caché, revalidado)" };
            }
            _cache.TryRemove(key, out _);
        }
        Misses++;
        var response = inner.Execute(request, cancellation);
        if (response.Status == ResponseStatus.Success)
            _cache[key] = response;
        return response;
    }

    private bool Revalidate(LegalizeRequest request, LegalizeResponse cached)
    {
        try
        {
            if (cached.Status != ResponseStatus.Success || cached.CandidateEntityBase64 is null) return false;
            if (ResponseValidator.Validate(cached).Count != 0) return false;
            if (!Destination.TryCreate(request.Target, out var dest, out _)) return false;
            if (!Enum.TryParse<EntityContext>(request.EntityContext, false, out var ctx)) return false;
            var original = EntityIo.Parse(Convert.FromBase64String(request.OriginalEntityBase64), ctx);
            var candidate = EntityIo.Parse(Convert.FromBase64String(cached.CandidateEntityBase64), dest!.Context);
            if (original is null || candidate is null) return false;
            return validator.Validate(candidate, original, dest, requireCatalogGate: request.Action == LegalizeAction.Legalize).Valid;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException) { return false; }
    }
}
