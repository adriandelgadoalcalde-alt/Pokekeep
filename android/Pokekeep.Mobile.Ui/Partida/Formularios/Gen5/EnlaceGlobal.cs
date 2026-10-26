// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-088 a PK-SAV-091 · <c>SAV_GlobalLink5</c>, el Pokémon Global Link de Negro/Blanco y Negro 2/Blanco 2 (bloque <c>GlobalLink5</c> del guardado). Las tres pestañas del original: «General» (<c>Tab_General</c>: fecha de
/// carga —con la casilla «Establecer», que decide si la fecha vale o se vacía—, conteo y estado de carga, ranura ocupada, tarjeta registrada, acceso total y los tres números de contenido descargado), «Objetos»
/// (<c>Tab_Items</c>: las 20 filas objeto + cantidad de <c>DGV_Items</c>; una colección de 20 elementos) y «Muebles» (<c>Tab_Furniture</c>: los 5 muebles del Dream World con su número y su nombre, el mueble seleccionado y
/// «Sincronizado»). Reglas calcadas de <c>SaveData</c>: la fecha es un <c>DateTimePicker</c> de 2000 a 2099 (la casilla decide entre <c>FromDateOnly</c> y <c>SetEmpty</c>); «Mueble seleccionado» llega a 255 como el control
/// pero el Core guarda solo 7 bits (el 8.º es «Sincronizado»); el nombre del mueble no tiene límite en el formulario y el Core lo recorta a 12 caracteres (la app limita la escritura a 12 en vez de recortar sin avisar).
/// El formulario reescribe TODOS los bloques al guardar (también el sobrante tras el nombre de los muebles); la app solo escribe lo que se cambia (ADR 0062).
/// </summary>
internal static class EnlaceGlobal
{
    public const string Form = "SAV_GlobalLink5";
    public static readonly string[] Censo = ["PK-SAV-088", "PK-SAV-089", "PK-SAV-090", "PK-SAV-091"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static GlobalLink5 Bloque(ContextoP c) => ((SAV5)c.Sav).GlobalLink;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5;

    private static readonly DateTime FechaMin = new(2000, 1, 1), FechaMax = new(2099, 12, 31);

    /// <summary>Lo que hace <c>SaveData</c> con la fecha: con «Establecer» la escribe y sin ella vacia los cuatro bytes.</summary>
    private static void GuardarFecha(ContextoP c)
    {
        var fecha = Bloque(c).UploadDate;
        if (c.Marca("fecha-fijada")) { var v = c.Fecha("fecha"); fecha.FromDateOnly(new DateOnly(v.Year, v.Month, v.Day)); }
        else fecha.SetEmpty();
    }

    private static IEnumerable<SeccionP> General()
    {
        yield return new SeccionP(L("L_UploadDate"),
        [
            P.Marca("fecha-fijada", L("CHK_DateSet"), c => Bloque(c).UploadDate.IsValid, (c, _) => GuardarFecha(c), grupo: "fecha"),
            new CampoP
            {
                Id = "fecha", Etiqueta = L("L_UploadDate"), Tipo = TipoP.FechaHora, SoloFecha = true, FechaMin = FechaMin, FechaMax = FechaMax, GrupoEscritura = "fecha",
                Visible = c => c.Marca("fecha-fijada"),
                Leer = c => Bloque(c).UploadDate is { IsValid: true } d ? d.ToDateOnly().ToDateTime(TimeOnly.MinValue) : DateTime.Today,
                Escribir = (c, _) => GuardarFecha(c),
            },
        ]);
        yield return new SeccionP(string.Empty,
        [
            P.Entero("cargas", L("L_UploadCount"), c => Bloque(c).UploadCount, (c, v) => Bloque(c).UploadCount = (int)v, int.MinValue, int.MaxValue, ayuda: IdiomaInterfaz.T("EnlaceGlobal.NuncaUsado")),
            P.Entero("estado", L("L_UploadStatus"), c => Bloque(c).UploadStatus, (c, v) => Bloque(c).UploadStatus = (byte)v, 0, byte.MaxValue),
            P.Marca("ranura", L("CHK_IsSlotPresent"), c => Bloque(c).IsSlotPresent, (c, v) => Bloque(c).IsSlotPresent = v),
            P.Marca("registrada", L("CHK_IsRegistered"), c => Bloque(c).IsRegistered, (c, v) => Bloque(c).IsRegistered = v),
            P.Marca("acceso-total", L("CHK_IsFullAccess"), c => Bloque(c).IsAccountFullAccess, (c, v) => Bloque(c).IsAccountFullAccess = v),
        ]);
        yield return new SeccionP(string.Empty,
        [
            P.Entero("musical", L("L_Musical"), c => Bloque(c).Musical, (c, v) => Bloque(c).Musical = (byte)v, 0, byte.MaxValue),
            P.Entero("skin-cgear", L("L_CGearSkin"), c => Bloque(c).CGearSkin, (c, v) => Bloque(c).CGearSkin = (byte)v, 0, byte.MaxValue),
            P.Entero("skin-dex", L("L_DexSkin"), c => Bloque(c).DexSkin, (c, v) => Bloque(c).DexSkin = (byte)v, 0, byte.MaxValue),
        ]);
    }

    private static string NombreObjeto(ContextoP c, int id) => id >= 0 && id < c.Textos.itemlist.Length ? c.Textos.itemlist[id] : $"{L("DGV_Item")} {id}";

    private static ColeccionDefP Objetos() => new()
    {
        Id = "objetos", Elemento = L("DGV_Item"), Cantidad = _ => GlobalLink5.CountItems,
        Titulo = (_, i) => $"{L("DGV_Item")} {i + 1}",
        Resumen = (c, i) => Bloque(c).GetItem(i) is var id && id == 0 && Bloque(c).GetItemQuantity(i) == 0 ? null : $"{NombreObjeto(c, id)} · {Bloque(c).GetItemQuantity(i)}",
        Secciones = (_, i) =>
        [
            new SeccionP(string.Empty,
            [
                P.Opcion($"objetos[{i}].objeto", L("DGV_Item"), c => Bloque(c).GetItem(i), (c, v) => Bloque(c).SetItem(i, (ushort)v), AyudasGen3.Objetos, titulo: L("DGV_Item")),
                P.Entero($"objetos[{i}].cantidad", L("DGV_Count"), c => Bloque(c).GetItemQuantity(i), (c, v) => Bloque(c).SetItemQuantity(i, (byte)v), 0, byte.MaxValue),
            ]),
        ],
    };

    private static IEnumerable<SeccionP> Muebles()
    {
        for (var i = 0; i < GlobalLink5.CountFurniture; i++)
        {
            var n = i;
            yield return new SeccionP($"{L("L_Furniture")} {n + 1}",
            [
                P.Entero($"mueble{n}", L("L_FurnitureNumber"), c => Bloque(c).GetFurniture(n).Value, (c, v) => { var m = Bloque(c).GetFurniture(n); m.Value = (ushort)v; }, 0, ushort.MaxValue),
                P.Texto($"mueble{n}-nombre", L("L_FurnitureName"), c => Bloque(c).GetFurniture(n).Name, (c, v) => { var m = Bloque(c).GetFurniture(n); m.Name = v; }, 12, ayuda: IdiomaInterfaz.T("EnlaceGlobal.HastaCaracteres")),
            ]);
        }
        yield return new SeccionP(string.Empty,
        [
            P.Entero("mueble-elegido", L("L_FurnitureSelected"), c => Bloque(c).SelectedFurnitureIndex, (c, v) => Bloque(c).SelectedFurnitureIndex = (byte)v, 0, byte.MaxValue, ayuda: IdiomaInterfaz.T("EnlaceGlobal.SoloCuentanBitsNinguno")),
            P.Marca("mueble-sincronizado", L("CHK_FurnitureSynchronized"), c => Bloque(c).IsFurnitureSynchronized, (c, v) => Bloque(c).IsFurnitureSynchronized = v),
        ]);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var pestanas = new List<PestanaP>
        {
            new("general", L("Tab_General"), [.. General()]),
            new("objetos", L("Tab_Items"), [], Coleccion: Objetos()),
            new("muebles", L("Tab_Furniture"), [.. Muebles()]),
        };
        return new EditorDefP("globallink", IdiomaInterfaz.T("Partida.Especifico.globallink.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.globallink.Descripcion"));
    }
}
