// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>
/// El editor de una entidad sin pantalla: el Pokemon del borrador, la partida, las listas y el catalogo de campos con sus reglas. Las pruebas del host lo usan para recorrer TODOS los campos de
/// cada formato; el ViewModel lo envuelve para la pantalla. Nada aqui conoce Avalonia. Cada escritura va al borrador (un clon del Pokemon de la casilla); llevarlo a la casilla es cosa de
/// <c>EditorCasillas.Colocar</c> (un solo Deshacer).
/// </summary>
public sealed class ModeloEntidad
{
    private static IReadOnlyList<PestanaDef>? _catalogo;
    private static string? _catalogoIdioma;
    private static readonly object _cerrojoCatalogo = new();
    /// <summary>El catalogo completo de pestanas, secciones y campos (el mismo para todos los formatos; la visibilidad se decide con el Pokemon). Sus textos van en el idioma de la interfaz: se reconstruye al cambiarlo.</summary>
    public static IReadOnlyList<PestanaDef> Catalogo
    {
        get
        {
            lock (_cerrojoCatalogo)
            {
                var idioma = IdiomaInterfaz.Actual.Codigo;
                if (_catalogo is null || _catalogoIdioma != idioma)
                {
                    _catalogo = [Editor.Catalogo.Principal(), Editor.Catalogo.Encuentro(), Editor.Catalogo.Estadisticas(), Editor.Catalogo.Movimientos(), Editor.Catalogo.Cosmetico(), Editor.Catalogo.EO()];
                    _catalogoIdioma = idioma;
                }
                return _catalogo;
            }
        }
    }

    public ModeloEntidad(PKM borrador, SaveFile sav, FuentesEditor? fuentes = null)
    {
        Borrador = borrador;
        Fuentes = fuentes ?? FuentesEditor.Crear(sav, Ajustes.Pkhex.ProgramaPkhex.HaX);   // en modo HaX (Program.HaX) el editor abre ya en modo avanzado (bloque 10-1)
        Contexto = new ContextoEditor(borrador, sav, Fuentes);
        Original = borrador.Data.ToArray();
    }

    public PKM Borrador { get; }
    public FuentesEditor Fuentes { get; private set; }
    public ContextoEditor Contexto { get; private set; }
    /// <summary>Bytes del Pokemon al abrir el editor (para saber si hay cambios y para el round-trip de las pruebas).</summary>
    public byte[] Original { get; }

    /// <summary>Se dispara tras cada escritura correcta (la pantalla relee los campos).</summary>
    public event Action? Cambiado;

    public bool HayCambios => !Borrador.Data.SequenceEqual(Original);

    /// <summary>Una escritura hecha fuera de los campos (un boton de un subeditor, el motor) avisa a la pantalla.</summary>
    public void NotificarCambio() => Cambiado?.Invoke();

    public IAnfitrionEditor? Anfitrion { get => Contexto.Anfitrion; set => Contexto.Anfitrion = value; }

    /// <summary>Modo avanzado (HaX de PKHeX): listas completas, habilidad libre, bytes extra, estadisticas a mano. Reconstruye las listas.</summary>
    public void CambiarAvanzado(bool avanzado)
    {
        if (avanzado == Fuentes.Avanzado) return;
        var anfitrion = Contexto.Anfitrion;
        var manuales = Contexto.StatsManuales;
        Fuentes = FuentesEditor.Crear(Contexto.Sav, avanzado);
        Contexto = new ContextoEditor(Borrador, Contexto.Sav, Fuentes) { Anfitrion = anfitrion, StatsManuales = manuales && avanzado };
        Cambiado?.Invoke();
    }

    /// <summary>Relee únicamente fuentes de presentación; conserva el borrador, el original y el modo manual.</summary>
    public void ActualizarIdioma()
    {
        Fuentes = FuentesEditor.Crear(Contexto.Sav, Fuentes.Avanzado);
        Contexto = new ContextoEditor(Borrador, Contexto.Sav, Fuentes)
        {
            Anfitrion = Contexto.Anfitrion, StatsManuales = Contexto.StatsManuales,
            IndiceByteExtra = Contexto.IndiceByteExtra,
        };
    }

    // ---- Consulta ----
    public IReadOnlyList<PestanaDef> PestanasVisibles => [.. Catalogo.Where(p => p.EsVisible(Contexto) && p.Secciones.Any(s => s.EsVisible(Contexto) && s.Campos.Any(c => c.EsVisible(Contexto))))];

    public IEnumerable<(PestanaDef Pestana, SeccionDef Seccion, CampoDef Campo)> Campos(bool soloVisibles = true)
    {
        foreach (var p in Catalogo)
        {
            if (soloVisibles && !p.EsVisible(Contexto)) continue;
            foreach (var s in p.Secciones)
            {
                if (soloVisibles && !s.EsVisible(Contexto)) continue;
                foreach (var c in s.Campos)
                    if (!soloVisibles || c.EsVisible(Contexto)) yield return (p, s, c);
            }
        }
    }

    public CampoDef? Buscar(string clave) => Campos(false).Select(x => x.Campo).FirstOrDefault(c => c.Clave == clave);

    public string Valor(CampoDef c) => c.Valor(Contexto);
    public int IdActual(CampoDef c) => c.IdActual?.Invoke(Contexto) ?? 0;
    public bool Marcado(CampoDef c) => c.Marcado?.Invoke(Contexto) ?? false;
    public IReadOnlyList<OpcionCampo> Opciones(CampoDef c) => c.Opciones?.Invoke(Contexto) ?? [];
    public string? Rechazo(CampoDef c) => c.Rechazo(Contexto);
    public bool EsEditable(CampoDef c) => Rechazo(c) is null && c.Tipo != TipoCampo.Info;

    // ---- Escritura: null = aceptada; si no, el motivo ----
    public string? Escribir(CampoDef c, string texto) => c.Escribir is null ? IdiomaInterfaz.T("ModeloEntidad.CampoEscribeTexto") : Tras(c, () => c.Escribir(Contexto, texto));
    public string? EscribirId(CampoDef c, int id) => c.EscribirId is null ? IdiomaInterfaz.T("ModeloEntidad.CampoEligeLista") : Tras(c, () => c.EscribirId(Contexto, id));
    public string? EscribirMarca(CampoDef c, bool valor) => c.EscribirMarca is null ? IdiomaInterfaz.T("ModeloEntidad.CampoInterruptor") : Tras(c, () => c.EscribirMarca(Contexto, valor));
    public string? Ejecutar(CampoDef c) => Tras(c, () => { c.Ejecutar?.Invoke(Contexto); return null; });

    private string? Tras(CampoDef c, Func<string?> escribir)
    {
        var rechazo = Rechazo(c);
        if (rechazo is not null) return rechazo;
        var copia = Borrador.Data.ToArray();
        string? resultado;
        try { resultado = escribir(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or FormatException or OverflowException)
        {
            copia.CopyTo(Borrador.Data);
            return IdiomaInterfaz.T("ModeloEntidad.PodidoAplicarValor") + ex.Message;
        }
        if (resultado is not null) { copia.CopyTo(Borrador.Data); return resultado; }   // un rechazo nunca deja el Pokemon a medias
        if (c.AfectaStats && !Contexto.StatsManuales && Borrador.PartyStatsPresent) Reglas.RecalcularStats(Borrador);
        Cambiado?.Invoke();
        return null;
    }

    /// <summary>
    /// Deja el Pokemon como lo guarda PKHeX al leer los campos (<c>PreparePKM</c>): sin huecos en los movimientos, movimientos recordados y recuerdos coherentes y el checksum recalculado.
    /// Solo hace falta si se ha cambiado algo; un Pokemon intacto se devuelve tal cual (los bytes ajenos no se tocan).
    /// </summary>
    public PKM Preparar()
    {
        if (!HayCambios) return Borrador.Clone();
        Reglas.Preparar(Borrador);
        return Borrador.Clone();
    }

    /// <summary>Mensaje de lo que falta si el Pokemon no esta completo (especie vacia, etc.), o null. Equivale a <c>EditsComplete</c> de PKHeX salvo las listas con valor fuera de la lista.</summary>
    public string? Incompleto()
    {
        if (Borrador.Species == 0) return IdiomaInterfaz.T("ModeloEntidad.EligeEspecie");
        if (Borrador.Species > Borrador.MaxSpeciesID) return IdiomaInterfaz.T("ModeloEntidad.EspecieExisteFormato");
        if (Borrador.Format >= 3 && Borrador.Nature > (Nature)24 && Borrador.Format < 8) return IdiomaInterfaz.T("ModeloEntidad.EligeNaturaleza");
        return null;
    }

    /// <summary>El Pokemon abierto cambia por otro del mismo formato (importar un set de Showdown o un archivo): se copian los bytes al borrador.</summary>
    public string? CargarDesde(PKM otro)
    {
        if (otro.GetType() != Borrador.GetType()) return IdiomaInterfaz.T("ModeloEntidad.PokemonImportadoFormatoPartida");
        otro.Data.CopyTo(Borrador.Data);
        Cambiado?.Invoke();
        return null;
    }
}
