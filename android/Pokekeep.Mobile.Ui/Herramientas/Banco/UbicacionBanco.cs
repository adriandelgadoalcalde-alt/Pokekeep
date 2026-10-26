// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using Pokekeep.Mobile.Ui.Almacen;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Qué pasó al elegir la carpeta del banco: si se eligió, qué banco había ya allí, cuánto se trasladó y los fallos.</summary>
public sealed record TrasladoBanco(bool Elegida, string? Carpeta, int YaHabia, int Trasladados, int Fallos, string? Error)
{
    public bool Correcto => Elegida && Error is null && Fallos == 0;
}

/// <summary>
/// Dónde está el banco ahora y cómo cambiarlo (X38). Hasta que el usuario elija una carpeta propia (con el selector del sistema, SAF), el banco vive en la carpeta privada de la app: se borra al desinstalar la app, y por
/// eso la pantalla lo avisa y ofrece «Elegir carpeta». Al elegirla se TRASLADA lo que hubiera (y, si la carpeta ya traía un banco, se reencuentra: así se recupera tras reinstalar). Solo cuando todo lo trasladado
/// está comprobado se vacía la carpeta privada.
/// </summary>
public sealed class UbicacionBanco
{
    private const string ArchivoUbicacion = "banco-carpeta.json";
    private readonly string _privada;
    private readonly IAlmacenCarpetas? _carpetas;
    private readonly IAlmacenDocumentos? _documentos;

    public UbicacionBanco(string carpetaPrivada, IAlmacenCarpetas? carpetas, IAlmacenDocumentos? documentos)
    {
        _privada = carpetaPrivada;
        _carpetas = carpetas;
        _documentos = documentos;
        Banco = Abrir();
    }

    /// <summary>Un banco ya hecho (pruebas y capturas; también los anfitriones sin carpeta privada): no hay carpeta que elegir.</summary>
    public UbicacionBanco(BancoPokemon banco, bool simularSelector = false)
    {
        _privada = string.Empty;
        Banco = banco;
        _simularSelector = simularSelector;
    }

    private readonly bool _simularSelector;

    public BancoPokemon Banco { get; private set; }
    public bool EnCarpetaDelUsuario => !Banco.Deposito.EsPrivado;
    public string Descripcion => Banco.Deposito.Descripcion;
    public bool PuedeElegirCarpeta => _simularSelector || (_carpetas is not null && _documentos is not null);
    /// <summary>La carpeta que se había elegido ya no está disponible (se retiró el permiso o se borró): el banco ha vuelto a la carpeta privada, donde NO estaba lo guardado en la otra.</summary>
    public bool CarpetaPerdida { get; private set; }
    public string? NombreCarpetaPerdida { get; private set; }

    private string RutaUbicacion => Path.Combine(_privada, ArchivoUbicacion);
    private string CarpetaPrivadaDelBanco => Path.Combine(_privada, "banco");

    private UbicacionGuardada? Leer()
    {
        try { return File.Exists(RutaUbicacion) ? JsonSerializer.Deserialize(File.ReadAllText(RutaUbicacion), BancoJson.Default.UbicacionGuardada) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private void Escribir(UbicacionGuardada u)
    {
        Directory.CreateDirectory(_privada);
        var t = RutaUbicacion + ".tmp";
        File.WriteAllText(t, JsonSerializer.Serialize(u, BancoJson.Default.UbicacionGuardada), new System.Text.UTF8Encoding(false));
        File.Move(t, RutaUbicacion, overwrite: true);
    }

    private BancoPokemon Privado() => new(new DepositoBancoCarpeta(CarpetaPrivadaDelBanco, privado: true, descripcion: TextosBanco.UbicacionPrivada));

    private BancoPokemon Abrir()
    {
        var guardada = Leer();
        if (guardada is not null && _carpetas is not null && _documentos is not null)
        {
            CarpetaRef? viva = null;
            try { viva = _carpetas.ObtenerCarpetaAsync(guardada.Uri, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { viva = null; }
            if (viva is not null) return new BancoPokemon(new DepositoBancoSaf(_carpetas, _documentos, viva));
            CarpetaPerdida = true; NombreCarpetaPerdida = guardada.Nombre;
        }
        return Privado();
    }

    /// <summary>Abre el banco que ya había (si lo había) y devuelve lo que arregló. Lista la carpeta y lee archivos: la pantalla lo llama SIEMPRE en segundo plano y cancelable.</summary>
    public InformeCarga Cargar(CancellationToken cancelar = default) => Banco.Cargar(cancelar);

    /// <summary>
    /// «Elegir carpeta»: el selector del sistema; el banco de la carpeta elegida se carga y recibe todo lo del banco actual que no tenga. Si todo se ha trasladado sin fallos, el banco antiguo se vacía (solo SUS archivos)
    /// y se recuerda la carpeta. Si algo falla, nada se borra del origen y se sigue usando el banco antiguo.
    /// </summary>
    public async Task<TrasladoBanco> ElegirCarpetaAsync(CancellationToken cancelar)
    {
        if (_carpetas is null || _documentos is null) return new(false, null, 0, 0, 0, TextosBanco.SinSelector);
        CarpetaRef? elegida;
        try { elegida = await _carpetas.ElegirCarpetaAsync(cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return new(false, null, 0, 0, 0, ex.Message); }
        if (elegida is null) return new(false, null, 0, 0, 0, null);
        return await Task.Run(() => TrasladarA(new DepositoBancoSaf(_carpetas, _documentos, elegida), elegida), cancelar).ConfigureAwait(false);
    }

    /// <summary>El traslado en sí, aparte del selector para poder probarlo con un depósito cualquiera.</summary>
    public TrasladoBanco TrasladarA(IDepositoBanco destino, CarpetaRef? carpeta)
    {
        var nuevo = new BancoPokemon(destino);
        var carga = nuevo.Cargar();
        if (carga.ErrorDeposito is not null) return new(false, destino.Descripcion, 0, 0, 0, carga.ErrorDeposito);
        var yaHabia = nuevo.Entradas.Count;
        var viejo = Banco;
        var informe = nuevo.Importar(viejo);
        if (informe.Error is not null || informe.Fallos > 0) return new(false, destino.Descripcion, yaHabia, informe.Importados, informe.Fallos, informe.Error ?? TextosBanco.TrasladoConFallos);
        // Comprobado: cada fila del banco viejo está en el nuevo y su archivo se lee.
        foreach (var e in viejo.Entradas)
            if (nuevo.Buscar(e.Id) is null || !nuevo.Leer(e.Id).Correcto()) return new(false, destino.Descripcion, yaHabia, informe.Importados, 1, TextosBanco.TrasladoNoComprobado);
        try { if (carpeta is not null) Escribir(new UbicacionGuardada(carpeta.Uri, carpeta.Nombre)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(false, destino.Descripcion, yaHabia, informe.Importados, 0, ex.Message); }
        if (viejo.Deposito.EsPrivado) viejo.VaciarDeposito();
        Banco = nuevo;
        CarpetaPerdida = false; NombreCarpetaPerdida = null;
        return new(true, destino.Descripcion, yaHabia, informe.Importados, 0, null);
    }
}

internal static class LecturaBancoExtensiones
{
    public static bool Correcto(this LecturaBanco l) => l.Pokemon is not null && l.Error is null;
}
