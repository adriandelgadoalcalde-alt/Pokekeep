// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text;
using System.Text.Json;
using AutoLegalidad.ALMAdapter;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Registro;
using PKHeX.Core;

namespace AutoLegalidad.Worker;

/// <summary>Logica del Worker, separada de <see cref="Program"/> para poder probarla en proceso y, ademas, como proceso real.</summary>
public static class WorkerHost
{
    /// <summary>Margen tras el <c>deadlineUtc</c> antes de que el propio Worker corte la busqueda y responda Timeout.</summary>
    public static readonly TimeSpan HardStopGrace = TimeSpan.FromSeconds(2);

    public static string DefaultDbPath(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? AppContext.BaseDirectory;
        foreach (var rel in new[] { "autolegalidad.sqlite", Path.Combine("datos", "autolegalidad.sqlite"), Path.Combine("..", "datos", "autolegalidad.sqlite") })
        {
            var p = Path.GetFullPath(Path.Combine(dir, rel));
            if (File.Exists(p)) return p;
        }
        // Desarrollo: subir hasta el repo (fuentes.lock.json) y usar datos\autolegalidad.sqlite.
        var d = new DirectoryInfo(dir);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "fuentes.lock.json")))
                return Path.Combine(d.FullName, "datos", "autolegalidad.sqlite");
            d = d.Parent;
        }
        return Path.Combine(dir, "autolegalidad.sqlite");
    }

    public static int WriteManifest(string dbPath, Stream stdout)
    {
        try
        {
            using var catalog = new CatalogReader(dbPath);
            var manifest = BuildManifest(catalog);
            stdout.Write(Json.SerializeToUtf8(manifest));
            stdout.Flush();
            return Protocol.ExitOk;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"[worker] manifiesto imposible: {ex.GetType().Name}: {ex.Message}");
            RegistroFallos.Error("No se pudo escribir el manifiesto del Worker.", ex, "WorkerHost.WriteManifest");
            return Protocol.ExitInfrastructure;
        }
    }

    public static WorkerManifest BuildManifest(CatalogReader catalog, IBinaryIdentity? identity = null)
    {
        var id = catalog.ReadIdentity();
        var asm = typeof(PKM).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        return new WorkerManifest
        {
            ProtocolVersion = Protocol.Version,
            ContractsVersion = Protocol.ContractsVersion,
            EngineVersion = PinnedVersions.EngineVersion,
            PolicyVersion = Protocol.PolicyVersion,
            CoreVersion = PinnedVersions.CoreTag + " (" + info + ")",
            CoreCommit = PinnedVersions.CoreCommit,
            CoreDllSha256 = (identity ?? BinaryIdentity.Current).CoreSha256,
            AlmVersion = PinnedVersions.AlmTag,
            AlmCommit = PinnedVersions.AlmCommit,
            AdapterVersion = PinnedVersions.AdapterVersion,
            ReleaseId = PinnedVersions.ReleaseId,
            RuleProfileHash = RuleProfile.Sha256,
            DatasetSnapshotId = id.SnapshotId,
            SchemaVersion = id.SchemaVersion,
        };
    }

    /// <summary>
    /// Lee UNA solicitud de <paramref name="stdin"/>, la ejecuta y escribe UNA respuesta en <paramref name="stdout"/>. Devuelve el codigo de salida del proceso.
    /// <paramref name="cancellation"/> (Android F1) permite cancelar de forma cooperativa desde quien hospeda el motor en proceso; <paramref name="hardStop"/> es lo que se hace tras
    /// responder Timeout cuando el generador no devuelve el control (por defecto <see cref="Environment.Exit(int)"/>: el Worker de escritorio es un proceso por solicitud; quien lo hospeda
    /// en proceso, p. ej. las pruebas, pasa otra cosa para no terminar su propio proceso).
    /// </summary>
    public static int Run(Stream stdin, Stream stdout, string dbPath, TextWriter? log = null, string? solutionsPath = null, CancellationToken cancellation = default, Action<int>? hardStop = null)
    {
        var raw = ReadBounded(stdin, Protocol.MaxRequestBytes, out var tooBig);
        if (tooBig)
        {
            RegistroFallos.Aviso($"Solicitud rechazada: supera {Protocol.MaxRequestBytes} bytes.", contexto: "WorkerHost.Run");
        }
        if (tooBig)
            return Write(stdout, Reject(Guid.Empty.ToString(), "La solicitud supera el tamaño máximo.", $"Más de {Protocol.MaxRequestBytes} bytes."));

        LegalizeRequest request;
        try
        {
            request = Json.Deserialize<LegalizeRequest>(raw);
        }
        catch (JsonException ex)
        {
            RegistroFallos.Aviso("Solicitud rechazada: no es un JSON válido del contrato.", ex, "WorkerHost.Run");
            return Write(stdout, Reject(TryRequestId(raw), "La solicitud no es un JSON válido del contrato.", ex.Message));
        }

        CatalogReader catalog;
        try
        {
            catalog = new CatalogReader(dbPath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log?.WriteLine($"[worker] catalogo no disponible: {ex.Message}");
            RegistroFallos.Error("No se pudo abrir el catálogo de datos.", ex, Contexto(request));
            return Write(stdout, Fail(request, ResponseStatus.VersionMismatch, "No se pudo abrir el catálogo de datos.", ex.Message));
        }

        using (catalog)
        {
            // R-011: cache persistente de soluciones (opcional; un fallo al abrirla la deja desactivada y NUNCA rompe la solicitud).
            using var solutions = WorkerSolutions.Open(dbPath, solutionsPath, catalog, log);
            // Progreso de las busquedas largas: una linea JSON por lote en stderr (prefijo fijo) que el cliente puede mostrar; stdout queda reservado a la respuesta.
            // Hito 4: la fase (A-E) tambien se cuenta por stderr con prefijo fijo (POKEKEEP-FASE <nombre del enum Phase>) para el cliente de la interfaz.
            var settings = new ServiceSettings
            {
                Progress = p => log?.WriteLine("POKEKEEP-PROGRESO " + System.Text.Json.JsonSerializer.Serialize(new { p.Strategy, p.Fraction, p.BestSum, p.TargetSum, segundos = Math.Round(p.Elapsed.TotalSeconds, 1) })),
                PhaseChanged = phase => log?.WriteLine("POKEKEEP-FASE " + phase),
            };
            var service = new LegalizationService(catalog, [new CoreTemplateStrategy(), new AlmGenerationStrategy(), new CoreEnumeratedStrategy()], settings: settings);
            var deadline = request.DeadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(request.Action == LegalizeAction.Optimize ? 300 : 60);
            LegalizeResponse? response = null;
            Exception? failure = null;
            using var cts = new CancellationTokenSource();
            using var externalCancel = cancellation.Register(static state => ((CancellationTokenSource)state!).Cancel(), cts);
            var worker = new Thread(() =>
            {
                try
                {
                    // Gancho SOLO de pruebas de proceso (nunca se define en produccion): simula un generador que no devuelve el control a tiempo.
#if !POKEKEEP_PORTABLE
                    if (int.TryParse(Environment.GetEnvironmentVariable("POKEKEEP_TEST_DELAY_MS"), out var delay) && delay > 0) Thread.Sleep(delay);
                    if (Environment.GetEnvironmentVariable("POKEKEEP_TEST_THROW") == "1") throw new InvalidOperationException("excepción de prueba del registro de fallos");
#endif
                    response = service.Execute(request, cts.Token);
                }
                catch (Exception ex) { failure = ex; }
            })
            { IsBackground = true, Name = "pokekeep-search" };
            worker.Start();

            // Vigilante duro: un generador (ALM incluido) puede quedarse DENTRO de un encuentro sin comprobar el tiempo. Si pasa el plazo mas el margen,
            // el Worker responde Timeout por su cuenta y termina el proceso (nada de Thread.Abort: se mata el proceso).
            var limit = deadline.UtcDateTime + HardStopGrace - DateTime.UtcNow;
            if (limit < TimeSpan.Zero) limit = TimeSpan.Zero;
            if (!worker.Join(limit))
            {
                cts.Cancel();
                RegistroFallos.Aviso($"Vigilante duro: el generador no devolvió el control a tiempo (plazo {deadline:O} + margen); el Worker responde Timeout y se termina.", contexto: Contexto(request));
                var timeout = Fail(request, ResponseStatus.Timeout, "Se agotó el tiempo antes de terminar la búsqueda.",
                    "El generador no devolvió el control antes del plazo; esto NO prueba que no exista solución.");
                var code = Write(stdout, timeout);
                stdout.Flush();
                (hardStop ?? Environment.Exit)(code); // mata el hilo bloqueado
                return code;
            }

            if (failure is not null)
            {
                log?.WriteLine($"[worker] excepcion: {failure}");
                RegistroFallos.Error("Excepción del motor durante la solicitud.", failure, Contexto(request));
                return Write(stdout, Fail(request, ResponseStatus.InternalError, "Error interno del motor.", $"{failure.GetType().Name}: {failure.Message}"));
            }
            var final = response!;
            var problems = ResponseValidator.Validate(final);
            if (problems.Count > 0)
            {
                // Invariante rota: jamas se devuelve un candidato que contradiga el contrato.
                log?.WriteLine("[worker] respuesta incoherente: " + string.Join("; ", problems));
                RegistroFallos.Error("El motor produjo una respuesta incoherente y se descartó: " + string.Join("; ", problems), contexto: Contexto(request));
                return Write(stdout, Fail(request, ResponseStatus.InternalError, "El motor produjo una respuesta incoherente y se descartó.", string.Join("; ", problems)));
            }
            if (final.Status is ResponseStatus.InternalError or ResponseStatus.VersionMismatch)
                RegistroFallos.Error($"El motor respondió {final.Status}: {final.Message} {final.Reason}", contexto: Contexto(request));
            return Write(stdout, final);
        }
    }

    /// <summary>Contexto de una entrada del registro: operación, juego de destino y casilla. NUNCA OT, TID/SID ni nombre de entrenador (ADR 0040).</summary>
    private static string Contexto(LegalizeRequest r)
        => $"solicitud {r.RequestId} · acción {r.Action} · destino {r.Target.GameVersion} ({r.EntityRuntimeType}, {r.Target.SlotType})" + (r.Selection is { } s ? $" · {s.Kind} caja {s.Box} casilla {s.Slot}" : "");

    private static LegalizeResponse Reject(string requestId, string message, string reason) => new()
    {
        ProtocolVersion = Protocol.Version,
        RequestId = requestId,
        Action = LegalizeAction.Legalize,
        Status = ResponseStatus.InvalidInput,
        Message = message,
        Reason = reason,
    };

    private static LegalizeResponse Fail(LegalizeRequest r, ResponseStatus status, string message, string reason) => new()
    {
        ProtocolVersion = Protocol.Version,
        RequestId = r.RequestId,
        ReleaseId = PinnedVersions.ReleaseId,
        DatasetSnapshotId = r.DatasetSnapshotId,
        Action = r.Action,
        Status = status,
        Message = message,
        Reason = reason,
    };

    private static string TryRequestId(byte[] raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("requestId", out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out _))
                return id.GetString()!;
        }
        catch (JsonException) { }
        return Guid.Empty.ToString();
    }

    private static int Write(Stream stdout, LegalizeResponse response)
    {
        stdout.Write(Json.SerializeToUtf8(response));
        stdout.WriteByte((byte)'\n');
        stdout.Flush();
        return Protocol.ExitOk;
    }

    private static byte[] ReadBounded(Stream s, int max, out bool tooBig)
    {
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        int n;
        tooBig = false;
        while ((n = s.Read(buf, 0, buf.Length)) > 0)
        {
            ms.Write(buf, 0, n);
            if (ms.Length > max) { tooBig = true; return []; }
        }
        return ms.ToArray();
    }
}
