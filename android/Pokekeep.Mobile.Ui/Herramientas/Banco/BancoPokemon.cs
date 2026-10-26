// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Qué encontró <see cref="BancoPokemon.Cargar"/> y qué tuvo que arreglar para dejar el índice al día con los archivos.</summary>
/// <param name="IndiceDeLaCopia">El índice no se pudo leer y se usó su copia anterior.</param>
/// <param name="IndiceReconstruido">No había índice válido ni copia: se rehízo entero desde los archivos (favoritos, etiquetas y papelera se pierden).</param>
/// <param name="Reconstruidas">Archivos del depósito que no estaban en el índice y se han añadido.</param>
/// <param name="Perdidas">Filas del índice cuyo archivo ya no está (se quitan de la lista).</param>
/// <param name="Actualizadas">Filas cuyo archivo no coincidía con lo anotado y se han vuelto a leer.</param>
/// <param name="Ilegibles">Archivos del depósito con pinta de Pokémon que el Core no puede leer; NO se tocan.</param>
public sealed record InformeCarga(bool IndiceDeLaCopia, bool IndiceReconstruido, int Reconstruidas, int Perdidas, int Actualizadas, IReadOnlyList<string> Ilegibles, string? ErrorDeposito)
{
    public bool HuboArreglos => IndiceDeLaCopia || IndiceReconstruido || Reconstruidas > 0 || Perdidas > 0 || Actualizadas > 0;
    public bool Limpio => !HuboArreglos && Ilegibles.Count == 0 && ErrorDeposito is null;
}

public enum TipoGuardado { Guardado, YaEstaba, Recuperado, Fallo }

/// <summary>Qué pasó con un Pokémon al guardarlo: nuevo, ya estaba (misma huella), sacado de la papelera porque estaba allí, o fallo con su motivo.</summary>
public sealed record ResultadoGuardado(TipoGuardado Tipo, EntradaBanco? Entrada, string? Motivo)
{
    public bool Correcto => Tipo != TipoGuardado.Fallo;
}

/// <summary>Un Pokémon leído del banco: el Pokémon (o el motivo de no poder leerlo) y si el archivo ya no es el que se guardó.</summary>
public sealed record LecturaBanco(PKM? Pokemon, string? Error, bool Alterado);

/// <summary>Cuánto se ha traído de otro banco al cambiar de carpeta.</summary>
public sealed record InformeImportacion(int Importados, int YaEstaban, int Fallos, string? Error);

/// <summary>
/// El banco propio de Pokémon (X38), fuera de cualquier partida. Cada Pokémon es un archivo estándar <c>.pk*</c> sin cifrar (el mismo que exporta PKHeX de escritorio y que éste puede abrir) con el identificador en el
/// nombre (<c>[pkb-id]</c>), y un índice JSON aparte (<see cref="NombreIndice"/>) con lo necesario para listar y filtrar sin abrir los archivos.
/// <para>
/// Seguridad de los datos: (1) un archivo de Pokémon se escribe UNA vez y no se reescribe; (2) cada cambio del índice guarda antes la versión anterior como <see cref="NombreCopia"/>; (3) el índice es reconstruible:
/// si falta, está roto o no coincide con los archivos, <see cref="Cargar"/> lo arregla leyendo los archivos y lo cuenta en un <see cref="InformeCarga"/>. Los archivos NUNCA se borran por una discrepancia: solo por una orden
/// explícita (vaciar la papelera o borrar definitivamente).
/// </para>
/// </summary>
public sealed partial class BancoPokemon
{
    public const string NombreIndice = "pokekeep-banco-indice.json";
    public const string NombreCopia = "pokekeep-banco-indice.bak.json";
    public const int MaxEtiquetas = 12;
    public const int MaxLargoEtiqueta = 24;

    private static readonly HashSet<string> ExtensionesConocidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pk1", ".pk2", ".sk2", ".pk3", ".ck3", ".xk3", ".pk4", ".bk4", ".rk4", ".pk5", ".pk6", ".pk7", ".pb7", ".pk8", ".pa8", ".pb8", ".pk9", ".pa9",
    };

    [GeneratedRegex(@"\[pkb-([0-9a-f]{16})\]", RegexOptions.IgnoreCase)]
    private static partial Regex MarcaId();

    private readonly Func<DateTime> _reloj;
    private readonly Func<string> _nuevoId;
    private List<EntradaBanco> _entradas = [];
    private byte[]? _indiceEnDisco;
    private bool _cargado;

    public BancoPokemon(IDepositoBanco deposito, Func<DateTime>? reloj = null, Func<string>? nuevoId = null)
    {
        Deposito = deposito;
        _reloj = reloj ?? (() => DateTime.Now);
        _nuevoId = nuevoId ?? (() => Guid.NewGuid().ToString("N")[..16]);
    }

    public IDepositoBanco Deposito { get; }
    public IReadOnlyList<EntradaBanco> Entradas => _entradas;
    public IEnumerable<EntradaBanco> Activas => _entradas.Where(e => !e.EnPapelera);
    public IEnumerable<EntradaBanco> EnLaPapelera => _entradas.Where(e => e.EnPapelera).OrderByDescending(e => e.Eliminado ?? e.Guardado);
    public int CantidadActivas => _entradas.Count(e => !e.EnPapelera);
    public int CantidadPapelera => _entradas.Count(e => e.EnPapelera);
    public EntradaBanco? Buscar(string id) => _entradas.FirstOrDefault(e => e.Id == id);

    private static bool EsDeSistema(string nombre) => nombre is NombreIndice or NombreCopia || nombre.EndsWith(".tmp", StringComparison.Ordinal);
    private static bool PareceDePokemon(string nombre) { var ext = Path.GetExtension(nombre); return ExtensionesConocidas.Contains(ext) || ext.StartsWith(".ek", StringComparison.OrdinalIgnoreCase); }

    // ---------------------------------------------------------------------------------------------------------------- cargar y reconciliar
    private static IndiceBanco? LeerIndice(byte[]? datos)
    {
        if (datos is null || datos.Length == 0) return null;
        try
        {
            var i = JsonSerializer.Deserialize(Encoding.UTF8.GetString(datos), BancoJson.Default.IndiceBanco);
            if (i is null || i.Entradas is null) return null;
            return i;
        }
        catch (JsonException) { return null; }
        catch (DecoderFallbackException) { return null; }
    }

    private static bool Valida(EntradaBanco e) => !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Archivo) && !string.IsNullOrWhiteSpace(e.Extension) && e.Especie > 0;

    /// <summary>
    /// Lee el índice (o su copia, o ninguno) y lo deja al día con los archivos del depósito: añade los archivos que no estaban, quita las filas sin archivo y relee las que no coinciden en tamaño. Si arregla algo, guarda el
    /// índice. Un fallo del depósito se devuelve en el informe y deja la lista como estaba.
    /// </summary>
    public InformeCarga Cargar(CancellationToken cancelar = default)
    {
        var archivos = Deposito.Listar(out var errorListado);
        if (errorListado is not null) return new InformeCarga(false, false, 0, 0, 0, [], errorListado);
        var porNombre = archivos.Where(a => !EsDeSistema(a.Nombre)).GroupBy(a => a.Nombre, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // 1.º El índice: el principal, si no vale su copia, si no ninguno.
        var principal = archivos.Any(a => a.Nombre == NombreIndice) ? Deposito.Leer(NombreIndice, out _) : null;
        var indice = LeerIndice(principal);
        var delaCopia = false; var reconstruido = false;
        byte[]? bytesVigentes = principal;
        if (indice is null)
        {
            var copia = archivos.Any(a => a.Nombre == NombreCopia) ? Deposito.Leer(NombreCopia, out _) : null;
            indice = LeerIndice(copia);
            if (indice is not null) { delaCopia = true; bytesVigentes = copia; }
            else if (porNombre.Keys.Any(PareceDePokemon) || principal is not null || copia is not null) reconstruido = true;
        }

        // 2.º Las filas del índice: válidas, sin identificadores repetidos.
        var lista = new List<EntradaBanco>(); var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in indice?.Entradas ?? [])
            if (Valida(e) && vistos.Add(e.Id)) lista.Add(e);

        // 3.º Contra los archivos.
        var perdidas = 0; var actualizadas = 0; var reconstruidas = 0; var ilegibles = new List<string>();
        var resultado = new List<EntradaBanco>(lista.Count);
        foreach (var e in lista)
        {
            cancelar.ThrowIfCancellationRequested();
            if (!porNombre.TryGetValue(e.Archivo, out var archivo)) { perdidas++; continue; }
            if (archivo.Bytes >= 0 && archivo.Bytes != e.Bytes)
            {
                var bytes = Deposito.Leer(e.Archivo, out _);
                var pk = bytes is null ? null : FichaBanco.Leer(bytes, e.Extension);
                if (pk is null || bytes is null) { ilegibles.Add(e.Archivo); perdidas++; continue; }   // el archivo no es un Pokémon legible: queda donde está, pero no en la lista
                resultado.Add(FichaBanco.Entrada(pk, e.Id, e.Archivo, bytes, e.Guardado) with { Favorito = e.Favorito, Etiquetas = e.Etiquetas, EnPapelera = e.EnPapelera, Eliminado = e.Eliminado });
                actualizadas++;
                continue;
            }
            resultado.Add(e);
        }
        var conocidos = resultado.Select(e => e.Archivo).ToHashSet(StringComparer.Ordinal);
        var idsUsados = resultado.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (nombre, archivo) in porNombre.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            cancelar.ThrowIfCancellationRequested();
            if (conocidos.Contains(nombre) || !PareceDePokemon(nombre)) continue;
            var bytes = Deposito.Leer(nombre, out _);
            var pk = bytes is null ? null : FichaBanco.Leer(bytes, Path.GetExtension(nombre));
            if (pk is null || bytes is null) { if (!ilegibles.Contains(nombre)) ilegibles.Add(nombre); continue; }
            var m = MarcaId().Match(nombre);
            var id = m.Success && !idsUsados.Contains(m.Groups[1].Value.ToLowerInvariant()) ? m.Groups[1].Value.ToLowerInvariant() : NuevoIdLibre(idsUsados);
            idsUsados.Add(id);
            resultado.Add(FichaBanco.Entrada(pk, id, nombre, bytes, _reloj()));
            reconstruidas++;
        }

        cancelar.ThrowIfCancellationRequested();   // hasta aquí no se ha cambiado nada: cancelar deja el banco y el disco como estaban
        _entradas = resultado;
        _indiceEnDisco = bytesVigentes;
        _cargado = true;
        var informe = new InformeCarga(delaCopia, reconstruido, reconstruidas, perdidas, actualizadas, ilegibles, null);
        if (informe.HuboArreglos || principal is null && resultado.Count > 0)
        {
            var fallo = Confirmar(resultado, guardarCopia: !delaCopia && !reconstruido);
            if (fallo is not null) return informe with { ErrorDeposito = fallo };
        }
        return informe;
    }

    private string NuevoIdLibre(HashSet<string> usados)
    {
        string id;
        do id = _nuevoId(); while (usados.Contains(id));
        return id;
    }

    // ---------------------------------------------------------------------------------------------------------------- índice
    /// <summary>Escribe el índice nuevo (guardando antes el anterior como copia). Devuelve el motivo del fallo, o null; si falla, la lista en memoria NO cambia.</summary>
    private string? Confirmar(List<EntradaBanco> nuevas, bool guardarCopia = true)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new IndiceBanco(IndiceBanco.VersionActual, nuevas), BancoJson.Default.IndiceBanco));
        if (guardarCopia && _indiceEnDisco is { Length: > 0 }) Deposito.Escribir(NombreCopia, _indiceEnDisco);   // si la copia falla el índice se escribe igual: la copia es un respaldo, no un requisito
        var r = Deposito.Escribir(NombreIndice, json);
        if (r.Error is not null) return r.Error;
        _indiceEnDisco = json;
        _entradas = nuevas;
        return null;
    }

    /// <summary>Nada se escribe antes de haber leído lo que hay: si no se ha cargado, se carga (así un índice sin leer nunca se pisa con una lista vacía).</summary>
    private bool Asegurar() { if (!_cargado) Cargar(); return _cargado; }

    private string? Cambiar(Func<EntradaBanco, EntradaBanco> cambio, IEnumerable<string> ids)
    {
        if (!Asegurar()) return TextosBanco.BancoNoLeido;
        var set = ids.ToHashSet(StringComparer.Ordinal);
        if (set.Count == 0) return null;
        var nuevas = _entradas.Select(e => set.Contains(e.Id) ? cambio(e) : e).ToList();
        return Confirmar(nuevas);
    }

    // ---------------------------------------------------------------------------------------------------------------- guardar
    /// <summary>
    /// Guarda una COPIA de cada Pokémon (el original de la partida no se toca). Uno que ya está en el banco (misma huella) no se duplica: sale como <see cref="TipoGuardado.YaEstaba"/>; si estaba en la papelera, se
    /// recupera de ella. Los archivos se escriben primero y el índice una sola vez al final; si el índice no se puede escribir, los archivos nuevos se retiran y nada queda a medias.
    /// </summary>
    public IReadOnlyList<ResultadoGuardado> Guardar(IReadOnlyList<PKM> pokemon, IReadOnlyList<EstadoLegalBanco?>? legalidades = null)
    {
        if (!Asegurar()) return [.. pokemon.Select(_ => new ResultadoGuardado(TipoGuardado.Fallo, null, TextosBanco.BancoNoLeido))];
        var resultados = new ResultadoGuardado?[pokemon.Count];
        var nuevas = new List<EntradaBanco>(_entradas);
        var recuperadas = new HashSet<string>(StringComparer.Ordinal);
        var escritos = new List<(int Indice, string Archivo)>();
        for (var i = 0; i < pokemon.Count; i++)
        {
            var pk = pokemon[i];
            if (pk.Species == 0) { resultados[i] = new(TipoGuardado.Fallo, null, TextosBanco.PokemonVacio); continue; }
            var huella = FichaBanco.Huella(pk);
            var igual = nuevas.FirstOrDefault(e => e.Huella == huella);
            if (igual is not null)
            {
                if (igual.EnPapelera) { var sacada = igual with { EnPapelera = false, Eliminado = null }; nuevas[nuevas.IndexOf(igual)] = sacada; recuperadas.Add(igual.Id); resultados[i] = new(TipoGuardado.Recuperado, sacada, null); }
                else resultados[i] = new(TipoGuardado.YaEstaba, igual, null);
                continue;
            }
            var bytes = FichaBanco.Bytes(pk);
            var id = NuevoIdLibre(nuevas.Select(e => e.Id).ToHashSet(StringComparer.Ordinal));
            var w = Deposito.Escribir(FichaBanco.NombreArchivo(pk, id), bytes);
            if (w.Error is not null) { resultados[i] = new(TipoGuardado.Fallo, null, w.Error); continue; }
            escritos.Add((i, w.Nombre));
            var entrada = FichaBanco.Entrada(pk, id, w.Nombre, bytes, _reloj(), legalidades is not null && i < legalidades.Count ? legalidades[i] : null);
            nuevas.Add(entrada);
            resultados[i] = new(TipoGuardado.Guardado, entrada, null);
        }
        if (escritos.Count > 0 || recuperadas.Count > 0)
        {
            var fallo = Confirmar(nuevas);
            if (fallo is not null)
            {
                // El índice no se escribió: lo recién escrito se retira para no dejar archivos sin fila (la próxima carga los reconstruiría, pero el usuario ya fue avisado de un fallo).
                foreach (var (indice, archivo) in escritos) { Deposito.Borrar(archivo); resultados[indice] = new(TipoGuardado.Fallo, null, fallo); }
                foreach (var i in Enumerable.Range(0, resultados.Length)) if (resultados[i]?.Tipo == TipoGuardado.Recuperado) resultados[i] = new(TipoGuardado.Fallo, null, fallo);
            }
        }
        return [.. resultados.Select(r => r!)];
    }

    public ResultadoGuardado Guardar(PKM pk, EstadoLegalBanco? legalidad = null) => Guardar([pk], [legalidad])[0];

    // ---------------------------------------------------------------------------------------------------------------- leer
    /// <summary>Lee el Pokémon de un archivo del banco. <see cref="LecturaBanco.Alterado"/> avisa si el archivo ya no es el que se guardó (cambió su contenido después).</summary>
    public LecturaBanco Leer(string id)
    {
        if (Buscar(id) is not { } e) return new(null, TextosBanco.NoEstaEnElBanco, false);
        var bytes = Deposito.Leer(e.Archivo, out var error);
        if (bytes is null) return new(null, error ?? TextosBanco.ArchivoNoEncontrado(e.Archivo), false);
        var pk = FichaBanco.Leer(bytes, e.Extension);
        if (pk is null) return new(null, TextosBanco.ArchivoIlegible(e.Archivo), false);
        return new(pk, null, FichaBanco.Huella(pk) != e.Huella);
    }

    // ---------------------------------------------------------------------------------------------------------------- favoritos y etiquetas
    public string? PonerFavorito(IEnumerable<string> ids, bool favorito) => Cambiar(e => e with { Favorito = favorito }, ids);

    /// <summary>Una etiqueta limpia: sin espacios de más, con tope de largo; vacía si no vale.</summary>
    public static string LimpiarEtiqueta(string texto)
    {
        var t = Regex.Replace(texto ?? string.Empty, @"\s+", " ").Trim();
        return t.Length > MaxLargoEtiqueta ? t[..MaxLargoEtiqueta].TrimEnd() : t;
    }

    /// <summary>Añade y quita etiquetas a varios Pokémon a la vez. Sin distinguir mayúsculas; como mucho <see cref="MaxEtiquetas"/> por Pokémon.</summary>
    public string? Etiquetar(IEnumerable<string> ids, IReadOnlyList<string> anadir, IReadOnlyList<string> quitar)
    {
        var alta = anadir.Select(LimpiarEtiqueta).Where(t => t.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
        var baja = quitar.Select(LimpiarEtiqueta).Where(t => t.Length > 0).ToList();
        return Cambiar(e =>
        {
            var l = e.EtiquetasOVacio.Where(x => !baja.Contains(x, StringComparer.CurrentCultureIgnoreCase)).ToList();
            foreach (var t in alta) if (l.Count < MaxEtiquetas && !l.Contains(t, StringComparer.CurrentCultureIgnoreCase)) l.Add(t);
            return e with { Etiquetas = l };
        }, ids);
    }

    /// <summary>Todas las etiquetas en uso (sin la papelera) con cuántos Pokémon las llevan, de la más usada a la menos.</summary>
    public IReadOnlyList<(string Etiqueta, int Cantidad)> Etiquetas()
        => [.. Activas.SelectMany(e => e.EtiquetasOVacio).GroupBy(t => t, StringComparer.CurrentCultureIgnoreCase).Select(g => (g.First(), g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Item1, StringComparer.CurrentCultureIgnoreCase)];

    // ---------------------------------------------------------------------------------------------------------------- papelera
    public string? AlaPapelera(IEnumerable<string> ids) { var ahora = _reloj(); return Cambiar(e => e.EnPapelera ? e : e with { EnPapelera = true, Eliminado = ahora }, ids); }
    public string? Restaurar(IEnumerable<string> ids) => Cambiar(e => e.EnPapelera ? e with { EnPapelera = false, Eliminado = null } : e, ids);

    /// <summary>
    /// Borra para siempre los de la papelera indicados: primero el archivo y después la fila. Si un archivo no se puede borrar, su fila se queda en la papelera y se cuenta en <c>Fallos</c>. Solo toca lo que está en la
    /// papelera: nunca un Pokémon activo.
    /// </summary>
    public (int Borrados, int Fallos, string? Error) EliminarDefinitivamente(IEnumerable<string> ids)
    {
        if (!Asegurar()) return (0, 0, TextosBanco.BancoNoLeido);
        var set = ids.ToHashSet(StringComparer.Ordinal);
        var objetivo = _entradas.Where(e => e.EnPapelera && set.Contains(e.Id)).ToList();
        var borrados = new HashSet<string>(StringComparer.Ordinal); var fallos = 0;
        foreach (var e in objetivo)
        {
            if (Deposito.Borrar(e.Archivo) is null) borrados.Add(e.Id); else fallos++;
        }
        if (borrados.Count == 0) return (0, fallos, null);
        var fallo = Confirmar([.. _entradas.Where(e => !borrados.Contains(e.Id))]);
        return fallo is null ? (borrados.Count, fallos, null) : (0, fallos, fallo);   // si el índice no se escribe, la próxima carga quita las filas sin archivo
    }

    public (int Borrados, int Fallos, string? Error) VaciarPapelera() => EliminarDefinitivamente(EnLaPapelera.Select(e => e.Id).ToList());

    // ---------------------------------------------------------------------------------------------------------------- cambiar de carpeta
    /// <summary>
    /// Trae a este banco todo lo que tiene <paramref name="origen"/> y aquí no está (por identificador), con sus favoritos, etiquetas y papelera. No borra nada del origen: quien lo llama lo vacía solo si
    /// todo ha ido bien (<see cref="VaciarDeposito"/>).
    /// </summary>
    public InformeImportacion Importar(BancoPokemon origen)
    {
        if (!Asegurar()) return new(0, 0, 0, TextosBanco.BancoNoLeido);
        var nuevas = new List<EntradaBanco>(_entradas);
        var ids = nuevas.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var archivos = nuevas.Select(e => e.Archivo).ToHashSet(StringComparer.Ordinal);
        int importados = 0, yaEstaban = 0, fallos = 0;
        var escritos = new List<string>();
        foreach (var e in origen.Entradas)
        {
            if (ids.Contains(e.Id)) { yaEstaban++; continue; }
            var bytes = origen.Deposito.Leer(e.Archivo, out _);
            if (bytes is null) { fallos++; continue; }
            var w = Deposito.Escribir(archivos.Contains(e.Archivo) ? $"{Path.GetFileNameWithoutExtension(e.Archivo)} ({e.Id[..4]}){Path.GetExtension(e.Archivo)}" : e.Archivo, bytes);
            if (w.Error is not null) { fallos++; continue; }
            escritos.Add(w.Nombre);
            nuevas.Add(e with { Archivo = w.Nombre });
            ids.Add(e.Id); archivos.Add(w.Nombre); importados++;
        }
        if (importados > 0)
        {
            var fallo = Confirmar(nuevas);
            if (fallo is not null) { foreach (var a in escritos) Deposito.Borrar(a); return new(0, yaEstaban, fallos + importados, fallo); }
        }
        return new(importados, yaEstaban, fallos, null);
    }

    /// <summary>Borra del depósito los archivos de ESTE banco (los Pokémon, el índice y su copia); lo demás de la carpeta no se toca. Devuelve cuántos no se pudieron borrar.</summary>
    public int VaciarDeposito()
    {
        var fallos = 0;
        foreach (var e in _entradas) if (Deposito.Borrar(e.Archivo) is not null) fallos++;
        Deposito.Borrar(NombreIndice); Deposito.Borrar(NombreCopia);
        if (fallos == 0) _entradas = [];
        return fallos;
    }
}
