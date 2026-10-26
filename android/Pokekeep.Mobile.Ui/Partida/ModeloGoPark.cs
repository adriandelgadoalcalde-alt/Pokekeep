// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Complejo GO de Let's Go (<c>Tab_Complex</c> de <c>SAV_Trainer7GG</c>): los 1000 huecos del Parque GO (20 zonas de 50), importar y exportar Pokémon de GO (.gp1), vaciar un hueco o todos, con las mismas llamadas del Core que el formulario.
/// Trabaja sobre un borrador; «Aplicar» lo escribe como UNA entrada del historial.
/// </summary>
public sealed class ModeloGoPark
{
    private readonly SesionM _sesion;
    private readonly GoParkStorage _park;
    private readonly Dictionary<int, GP1> _cambios = new();
    private bool _vaciarTodo;

    public ModeloGoPark(SesionM sesion) { _sesion = sesion; _park = ((SAV7b)sesion.Guardado).Park; }

    public static bool Aplica(SaveFile sav) => sav is SAV7b;
    public static string? NoAplica(SaveFile sav) => Aplica(sav) ? null : IdiomaInterfaz.F("Partida.NoAplica.GoPark", NombresJuego.Corto(GameVersion.GP), NombresJuego.Corto(GameVersion.GE));

    public const int Huecos = GoParkStorage.Count;
    public const int HuecosPorZona = GoParkStorage.SlotsPerArea;
    public static string Etiqueta(int i) => IdiomaInterfaz.F("ModeloGoPark.ZonaHueco", i / HuecosPorZona + 1, i % HuecosPorZona + 1);

    public GP1 Leer(int i) => _cambios.TryGetValue(i, out var g) ? g : _vaciarTodo ? new GP1() : _park[i];
    public bool Ocupado(int i) => Leer(i).Species != 0;
    public bool Modificado(int i) => _cambios.ContainsKey(i) || (_vaciarTodo && _park[i].Species != 0);
    public bool HayCambios => _cambios.Count > 0 || _vaciarTodo;
    public int Ocupados => Enumerable.Range(0, Huecos).Count(Ocupado);

    /// <summary>Importa un .gp1 a un hueco (<c>ImportGP1From</c>): el archivo tiene que medir exactamente lo que un Pokémon de GO. Devuelve el motivo del rechazo o null.</summary>
    public string? Importar(int i, byte[] datos)
    {
        if ((uint)i >= Huecos) return IdiomaInterfaz.T("ModeloGoPark.HuecoExiste");
        if (datos.Length != GP1.SIZE) return IdiomaInterfaz.F("ModeloGoPark.ArchivoPokemonGOMide", datos.Length, GP1.SIZE);
        var gp1 = new GP1();
        datos.CopyTo(gp1.Data);
        _cambios[i] = gp1;
        return null;
    }

    /// <summary>Primer hueco libre a partir de uno (como «Importar carpeta» del formulario); -1 si no queda.</summary>
    public int PrimerLibre(int desde = 0)
    {
        for (var i = Math.Max(0, desde); i < Huecos; i++) if (!Ocupado(i)) return i;
        return -1;
    }

    public void Vaciar(int i) => _cambios[i] = new GP1();

    public void VaciarTodo() { _cambios.Clear(); _vaciarTodo = true; }

    public void Descartar() { _cambios.Clear(); _vaciarTodo = false; }

    public InformePartida Aplicar()
    {
        if (!HayCambios) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);
        var desc = _vaciarTodo ? IdiomaInterfaz.T("ModeloGoPark.ComplejoGOVaciadoEntero") : IdiomaInterfaz.P("ModeloGoPark.HuecosCambiados", _cambios.Count);
        var inf = EscrituraPartida.Aplicar(_sesion, desc, () =>
        {
            if (_vaciarTodo) _park.DeleteAll();
            foreach (var (i, g) in _cambios.OrderBy(k => k.Key)) _park[i] = g;
        });
        Descartar();
        return inf;
    }
}
