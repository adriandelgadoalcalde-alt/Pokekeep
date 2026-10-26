// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-160 · <c>SAV_Poffin8b</c>, los Pokochos de Diamante Brillante y Perla Reluciente: 100 ranuras con tipo, nivel, suavidad, marca de «nuevo» y los cinco sabores. «Guardar» (<c>B_Save_Click</c>) vuelve a escribir las
/// 100 filas desde la cuadrícula y llama a <c>SetPoffins</c>, que las REORDENA (las vacías al final, y dentro de cada grupo primero las que no son nuevas): aquí, cuando hay algún cambio, cada ranura se escribe y después se
/// repite esa misma normalización sobre las 100 (tipo por su nombre, «nuevo» como 0 o 1 y el orden), con lo que los bytes son los del formulario. «Todos» (<c>B_All</c>) pone en las 100 el tipo 0x1C, nivel 60 y suavidad y
/// sabores a 255; «Ninguno» (<c>B_None</c>) deja el tipo en «vacío». En el formulario las dos solo cambian la cuadrícula y se guardan con «Guardar»; aquí son acciones inmediatas (escriben y normalizan de golpe, con su
/// Deshacer) porque una acción pendiente no podría ganar a lo que se edite antes en la misma ranura. Divergencias declaradas en el ADR 0066.
/// </summary>
internal static class Pokochos8b
{
    public const string Form = "SAV_Poffin8b";
    public static readonly string[] Censo = ["PK-SAV-160"];
    private const int Ranuras = PoffinSaveData8b.COUNT_POFFIN;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV8BS Sav(ContextoP c) => (SAV8BS)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV8BS;

    /// <summary>Los nombres como los arma el formulario: la lista <c>poffin8b</c> con el hueco 0 sustituido por el «objeto 0» del juego.</summary>
    private static string[] Nombres(ContextoP c)
    {
        var n = Util.GetStringList("poffin8b", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        n[0] = c.Textos.itemlist[0];
        return n;
    }

    /// <summary>El nombre de un tipo (<c>GetPoffinName</c>): un tipo fuera de la lista da el hueco 0.</summary>
    private static string NombreDe(string[] nombres, byte tipo)
    {
        var indice = (int)(byte)(tipo + 1);
        if ((uint)indice >= nombres.Length) indice = 0;
        return nombres[indice];
    }

    /// <summary>El tipo de un nombre (<c>SetPoffinName</c>): el primer hueco con ese nombre menos uno, o 0xFF si no esta.</summary>
    private static byte TipoDe(string[] nombres, string nombre)
    {
        var indice = Array.IndexOf(nombres, nombre);
        return indice != -1 ? (byte)(indice - 1) : unchecked((byte)-1);
    }

    /// <summary>
    /// Lo que hace «Guardar» con las 100 filas (<c>SaveItems</c> + <c>SetPoffins</c>): el tipo pasa por su nombre y vuelve, «nuevo» se reescribe como 0 o 1 en sus cuatro bytes y las 100 se reordenan. Un tipo
    /// desconocido (que la lista no nombra) queda como «vacío» (0xFF), igual que en el original.
    /// </summary>
    internal static void Normalizar(ContextoP c)
    {
        var nombres = Nombres(c);
        var todos = Sav(c).Poffins.GetPoffins();
        foreach (var p in todos)
        {
            p.MstID = TipoDe(nombres, NombreDe(nombres, p.MstID));
            p.IsNew = p.IsNew;
        }
        Sav(c).Poffins.SetPoffins(todos);
    }

    private static void Cambiar(ContextoP c, int i, Action<Poffin8b> cambio)
    {
        var p = Sav(c).Poffins.GetPoffin(i);
        cambio(p);
        Sav(c).Poffins.SetPoffin(i, p);
    }

    /// <summary>Los tipos: el valor es el byte guardado (0 a 29) y 255 es «vacío»; el texto es el de la lista del formulario (el tipo n lleva el nombre n + 1).</summary>
    private static IReadOnlyList<OpcionP> Tipos(ContextoP c)
    {
        var nombres = Nombres(c);
        var l = new List<OpcionP> { new(255, "---") };
        for (var t = 0; t + 1 < nombres.Length; t++) l.Add(new OpcionP(t, nombres[t + 1]));
        return l;
    }

    private static string Nombre(ContextoP c, int i)
    {
        var p = Sav(c).Poffins.GetPoffin(i);
        return p.IsNull ? IdiomaInterfaz.T("Form.Fama1.Vacio") : NombreDe(Nombres(c), p.MstID);
    }

    private static string Resumen(ContextoP c, int i)
    {
        var p = Sav(c).Poffins.GetPoffin(i);
        return p.IsNull ? IdiomaInterfaz.T("Pokochos8b.Pokocho") : IdiomaInterfaz.F("Pokochos8b.NivelSuavidad", p.Level, p.Taste);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV8BS) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "pokochos", Elemento = IdiomaInterfaz.T("Pokochos8b.Pokocho2"), Cantidad = _ => Ranuras,
            Titulo = (c, i) => $"{i + 1:000} · {Nombre(c, i)}",
            Resumen = Resumen,
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Opcion(col!.IdCampo(i, "tipo"), L("Col_Type"), c => Sav(c).Poffins.GetPoffin(i).MstID, (c, v) => Cambiar(c, i, p => p.MstID = (byte)v), Tipos, titulo: IdiomaInterfaz.T("Pokochos8b.TipoPokocho")),
                    P.Entero(col.IdCampo(i, "nivel"), L("Col_Level"), c => Sav(c).Poffins.GetPoffin(i).Level, (c, v) => Cambiar(c, i, p => p.Level = (byte)v), 0, 255),
                    P.Entero(col.IdCampo(i, "suavidad"), L("Col_Smooth"), c => Sav(c).Poffins.GetPoffin(i).Taste, (c, v) => Cambiar(c, i, p => p.Taste = (byte)v), 0, 255),
                    P.Marca(col.IdCampo(i, "nuevo"), L("Col_New"), c => Sav(c).Poffins.GetPoffin(i).IsNew, (c, v) => Cambiar(c, i, p => p.IsNew = v)),
                    P.Entero(col.IdCampo(i, "picante"), L("Col_Spicy"), c => Sav(c).Poffins.GetPoffin(i).FlavorSpicy, (c, v) => Cambiar(c, i, p => p.FlavorSpicy = (byte)v), 0, 255),
                    P.Entero(col.IdCampo(i, "seco"), L("Col_Dry"), c => Sav(c).Poffins.GetPoffin(i).FlavorDry, (c, v) => Cambiar(c, i, p => p.FlavorDry = (byte)v), 0, 255),
                    P.Entero(col.IdCampo(i, "dulce"), L("Col_Sweet"), c => Sav(c).Poffins.GetPoffin(i).FlavorSweet, (c, v) => Cambiar(c, i, p => p.FlavorSweet = (byte)v), 0, 255),
                    P.Entero(col.IdCampo(i, "amargo"), L("Col_Bitter"), c => Sav(c).Poffins.GetPoffin(i).FlavorBitter, (c, v) => Cambiar(c, i, p => p.FlavorBitter = (byte)v), 0, 255),
                    P.Entero(col.IdCampo(i, "acido"), L("Col_Sour"), c => Sav(c).Poffins.GetPoffin(i).FlavorSour, (c, v) => Cambiar(c, i, p => p.FlavorSour = (byte)v), 0, 255),
                ]),
            ],
        };
        var acciones = new SeccionP(string.Empty,
        [
            P.Accion("todos", L("B_All"), c =>
            {
                foreach (var (p, i) in Sav(c).Poffins.GetPoffins().Select((p, i) => (p, i)))
                {
                    p.MstID = 0x1C; p.Level = 60; p.Taste = 0xFF;
                    p.FlavorSpicy = p.FlavorBitter = p.FlavorDry = p.FlavorSour = p.FlavorSweet = 0xFF;
                    Sav(c).Poffins.SetPoffin(i, p);
                }
                Normalizar(c);
            }, inmediata: true, textoBoton: IdiomaInterfaz.T("Comun.Dar"), confirmacion: IdiomaInterfaz.T("Pokochos8b.SustituyenPokochosTipo0x1C"), ayuda: IdiomaInterfaz.T("Pokochos8b.PonePokochosMaximo")),
            P.Accion("ninguno", L("B_None"), c =>
            {
                foreach (var (p, i) in Sav(c).Poffins.GetPoffins().Select((p, i) => (p, i)))
                {
                    p.ToNull();
                    Sav(c).Poffins.SetPoffin(i, p);
                }
                Normalizar(c);
            }, inmediata: true, textoBoton: IdiomaInterfaz.T("GoPark.Vaciar"), confirmacion: IdiomaInterfaz.T("Pokochos8b.VacianPokochos"), ayuda: IdiomaInterfaz.T("Pokochos8b.DejaRanurasVacias")),
        ]);
        return new EditorDefP("pozoffins8b", IdiomaInterfaz.T("Partida.Especifico.pozoffins8b.Titulo"),
            [new PestanaP("acciones", EtiquetasLang.RequerirFormulario(Form), [acciones]), new PestanaP("pokochos", IdiomaInterfaz.T("Pokochos8b.Pokochos"), [], Coleccion: col)],
            Censo, IdiomaInterfaz.T("Partida.Especifico.pozoffins8b.Descripcion"))
        {
            PrepararAplicar = _ => Normalizar,
        };
    }
}
