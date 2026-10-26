// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-035: <c>SAV_HoneyTree</c>, los 21 árboles de miel de Sinnoh (Diamante, Perla y Platino): tiempo restante (0-1440 min), agitadas (0-3), grupo (0-3) y espacio (0-5) de cada uno, la especie que sale de grupo y espacio, y
/// «Hacer capturable» (tiempo 1080). Al cambiar de árbol (y al guardar) el formulario hace <c>SaveTree()</c>: tiempo, agitadas, grupo (que reescribe también la subtabla, <c>max(0, grupo - 1)</c>) y espacio, y <c>SetHoneyTree</c>;
/// aquí esos cuatro campos son un grupo de escritura por árbol, en ese orden. «Hacer capturable» solo pone 1080 en el tiempo del borrador, como el botón en el cuadro de tiempo.
/// <para>Divergencias declaradas (ADR 0062): el formulario visita siempre el primer árbol y lo reescribe al guardar (recorta tiempo, agitadas, grupo y espacio a sus máximos y recalcula la subtabla) aunque no se toque;
/// la app solo escribe el árbol que se cambia. Los árboles con datos fuera de rango o con la subtabla distinta de <c>max(0, grupo - 1)</c> son los únicos donde eso se nota.
/// Los nombres de los lugares son los oficiales de Sinnoh (los del Core en castellano): PKHeX los trae solo en inglés en el diseñador del formulario.</para>
/// </summary>
internal static class MielarbolesGen4
{
    public const string Form = "SAV_HoneyTree";
    public static readonly string[] Censo = ["PK-SAV-035"];
    private const int Arboles = 21;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4Sinnoh S(ContextoP c) => (SAV4Sinnoh)c.Sav;

    // (lugar, lado) por árbol, en el orden del formulario: índices de Gen4.Met0 (Ruta 205 = 20...). El lado, si lo hay, es un pueblo o una ciudad.
    private static readonly (int Lugar, int Lado)[] Lugares =
    [
        (20, 3), (20, 9), (21, -1), (22, -1), (23, -1), (24, -1), (25, 4), (25, 5), (26, -1), (27, 10), (27, 11), (28, -1), (29, -1), (30, -1), (33, -1), (36, -1), (37, -1), (47, -1), (48, -1), (49, -1), (58, -1),
    ];

    internal static string Nombre(ContextoP c, int arbol)
    {
        var met = c.Textos.Gen4.Met0;
        var (lugar, lado) = Lugares[arbol];
        return lado < 0 ? met[lugar] : IdiomaInterfaz.F("MielarbolesGen4.LadoDe", met[lugar], met[lado]);
    }

    private static byte[] Munchlax(ContextoP c) { var t = new byte[4]; HoneyTreeUtil.CalculateMunchlaxTrees(S(c).ID32, t); return t; }

    /// <summary>La especie de un grupo y espacio como la etiqueta del formulario; Silcoon y Cascoon dependen de la versión (Diamante o Perla).</summary>
    private static string Especie(ContextoP c, int grupo, int espacio)
    {
        var especie = S(c).GetHoneyTreeSpecies(grupo, espacio);
        var nombres = c.Textos.specieslist;
        if (especie != (ushort)Species.Silcoon) return nombres[especie];
        var juegos = c.Textos.gamelist;
        return $"{nombres[especie]} ({juegos[(int)GameVersion.D]}) / {nombres[especie + 2]} ({juegos[(int)GameVersion.P]})";
    }

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenHoneyTreeEditor</c> solo existe en Diamante, Perla y Platino.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4Sinnoh;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4Sinnoh) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "arboles", Elemento = L("L_HoneyTree"), Cantidad = _ => Arboles,
            Titulo = (c, i) => Nombre(c, i),
            Resumen = (c, i) => { var t = S(c).GetHoneyTree(i); return $"{Math.Min(1440u, t.Time)} min · {Especie(c, Math.Min(3, t.Group), Math.Min(5, t.Slot)).Split(" (")[0]}"; },
            Secciones = (_, i) =>
            {
                var g = $"miel:{i}";
                var idTiempo = col!.IdCampo(i, "tiempo"); var idGrupo = col.IdCampo(i, "grupo"); var idEspacio = col.IdCampo(i, "espacio");
                void Cambiar(ContextoP c, Action<HoneyTreeValue> cambia) { var s = S(c); var t = s.GetHoneyTree(i); cambia(t); s.SetHoneyTree(t, i); }
                bool EsMunchlax(ContextoP c) => Munchlax(c).Contains((byte)i);
                // Munchlax solo es legal en los cuatro árboles que salen del ID del guardado (el formulario avisa con un cuadro al elegirlo en otro).
                string Aviso(ContextoP c)
                {
                    var grupo = (int)Math.Clamp(c.Entero(idGrupo), 0, 3); var espacio = (int)Math.Clamp(c.Entero(idEspacio), 0, 5);
                    var texto = $"{L("L_Species")}: {Especie(c, grupo, espacio)}";
                    return S(c).GetHoneyTreeSpecies(grupo, espacio) == (ushort)Species.Munchlax && !EsMunchlax(c)
                        ? texto + IdiomaInterfaz.T("MielarbolesGen4.CapturarMunchlaxArbolHaria")
                        : texto;
                }
                var campos = new List<CampoP>
                {
                    P.Info(col.IdCampo(i, "munchlax"), L("L_Munchlax"), c => string.Join(", ", Munchlax(c).Select(t => Nombre(c, t)).Distinct())),
                    P.Entero(idTiempo, L("L_Time"), c => Math.Min(1440u, S(c).GetHoneyTree(i).Time), (c, v) => Cambiar(c, t => t.Time = (uint)v), 0, 1440, prioridad: 0, grupo: g, sufijo: "min",
                        detalle: c => EsMunchlax(c) ? IdiomaInterfaz.T("MielarbolesGen4.ArbolUnoCuatroMunchlax") : null),
                    P.Entero(col.IdCampo(i, "agitadas"), L("L_Shake"), c => Math.Min(3, S(c).GetHoneyTree(i).Shake), (c, v) => Cambiar(c, t => t.Shake = (int)v), 0, 3, prioridad: 1, grupo: g),
                    P.Entero(idGrupo, L("L_Group"), c => Math.Min(3, S(c).GetHoneyTree(i).Group), (c, v) => Cambiar(c, t => t.Group = (int)v), 0, 3, prioridad: 2, grupo: g),
                    P.Entero(idEspacio, L("L_Slot"), c => Math.Min(5, S(c).GetHoneyTree(i).Slot), (c, v) => Cambiar(c, t => t.Slot = (int)v), 0, 5, prioridad: 3, grupo: g, detalle: Aviso),
                    // B_Catchable_Click: NUD_Time.Value = 1080. Solo cambia el borrador del tiempo; la escritura la hace el campo del tiempo.
                    new CampoP
                    {
                        Id = col.IdCampo(i, "capturable"), Etiqueta = L("B_Catchable"), Tipo = TipoP.Accion, Leer = _ => false, Escribir = (_, _) => { }, Prioridad = 100, TextoBoton = IdiomaInterfaz.T("MielarbolesGen4.HacerCapturable"),
                        Ayuda = IdiomaInterfaz.T("MielarbolesGen4.PoneTiempoRestanteMinutos"), Deriva = _ => new Dictionary<string, object?> { [idTiempo] = 1080L },
                    },
                };
                return [new SeccionP(L("GB_TreeInfo"), campos)];
            },
        };
        return new EditorDefP("mielarboles", TituloGen4.De("mielarboles"), [new PestanaP("arboles", L("L_HoneyTree"), [], Coleccion: col)], Censo, IdiomaInterfaz.T("Partida.Especifico.mielarboles.Descripcion"));
    }
}
