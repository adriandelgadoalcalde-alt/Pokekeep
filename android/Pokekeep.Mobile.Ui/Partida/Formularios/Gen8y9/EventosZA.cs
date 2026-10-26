// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-185, 190 y 191 · <c>SAV_FlagWork9a</c> y <c>EventWorkGrid64</c> (<c>EventWorkGrid64</c>, <c>EventWorkGridTuple</c>, <c>EventWorkGrid128</c> y <c>EventWorkGrid192</c>), las marcas, variables, misiones y contadores de Leyendas Pokémon Z-A:
/// 15 almacenes de entradas con clave de 64 bits (el hash FNV-1a del nombre interno) y valor de 64 bits (<c>EventWorkStorage</c>): marcas (<c>Flags</c>, <c>Event</c>, <c>FieldItems</c>, valor 0 o 1), variables (<c>Work</c>, <c>Quest</c>,
/// <c>WorkMable</c>, <c>CountMable</c>, <c>CountTitle</c>, <c>WorkSpawn</c>, <c>InfiniteRank</c>), con dos claves de 128 bits (<c>Report</c>, <c>Obstruction</c> y <c>Spawner2</c> con la segunda como número con signo; <c>Spawner4</c> con la segunda como hash)
/// y con tres claves (<c>FieldObjectInteractable</c>). El formulario enseña cada almacén en una cuadrícula con TODAS las posiciones (el índice, la clave y el valor; la clave se escribe como nombre, que se convierte en hash, o como
/// hexadecimal, y sin lista de nombres se enseña en hexadecimal) y «Guardar» reescribe todas las filas y llama a <c>Compress</c> en cada almacén, que sube las entradas usadas para cerrar los huecos. La pestaña «Investigar»
/// compara dos guardados y lista lo que se ha añadido o cambiado en nueve almacenes. Aquí cada almacén es una colección con un elemento por posición y un campo por columna; con algún cambio se repite la normalización de «Guardar»
/// (valor de las marcas a 0 o 1 y <c>Compress</c> de los 15). Divergencias declaradas en el ADR 0066.
/// </summary>
internal static class EventosZA
{
    public const string Form = "SAV_FlagWork9a";
    public static readonly string[] Censo = ["PK-SAV-185", "PK-SAV-190", "PK-SAV-191"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV9ZA Sav(ContextoP c) => (SAV9ZA)c.Sav;
    private static SaveBlockAccessor9ZA B(ContextoP c) => Sav(c).Blocks;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV9ZA;

    // ---------------- claves y valores como texto ----------------
    /// <summary>El nombre de una clave (<c>EventWorkLookup.GetName</c> sin lista de nombres): vacía si es la clave de hueco, y si no el hash en 16 cifras hexadecimales.</summary>
    internal static string Nombre(ulong hash) => hash == FnvHash.HashEmpty ? string.Empty : hash.ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>La clave de un texto (<c>EventWorkLookup.GetHash</c>): vacío = hueco; hexadecimal tal cual; cualquier otro texto, su hash FNV-1a de 64 bits.</summary>
    internal static ulong Hash(string texto)
    {
        var t = texto.Trim();
        if (t.Length == 0) return FnvHash.HashEmpty;
        return ulong.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : FnvHash.HashFnv1a_64(t);
    }

    private static bool EsEntero(string s, bool conSigno) => conSigno ? long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) : ulong.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private static CampoP Clave(string id, string etiqueta, Func<ContextoP, ulong> leer, Action<ContextoP, ulong> escribir, string? ayuda = null) => new()
    {
        Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, MaxLongitud = 64, Ayuda = ayuda ?? IdiomaInterfaz.T("EventosZA.NombreInternoConvierteHash"),
        Leer = c => Nombre(leer(c)), Escribir = (c, v) => escribir(c, Hash((string)v!)),
        Normaliza = t => Nombre(Hash(t)),
    };

    /// <summary>Un número de 64 bits en decimal (con o sin signo): como texto para no perder el rango entero; lo que no es un número se rechaza al escribir (WinForms avisa y devuelve el valor guardado).</summary>
    private static CampoP Numero(string id, string etiqueta, Func<ContextoP, string> leer, Action<ContextoP, string> escribir, bool conSigno, string? ayuda = null) => new()
    {
        Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, MaxLongitud = 20, Ayuda = ayuda ?? (conSigno ? IdiomaInterfaz.T("EventosZA.NumeroEnteroSignoBits") : IdiomaInterfaz.T("BloquesSC.NumeroEnteroSignoBits")),
        Leer = c => leer(c), Escribir = (c, v) => escribir(c, (string)v!),
        Validar = (_, t) => EsEntero(t, conSigno) ? null : conSigno ? IdiomaInterfaz.T("EventosZA.EscribeNumeroEnteroSigno") : IdiomaInterfaz.T("BloquesSC.EscribeNumeroEnteroSigno"),
        Normaliza = t => EsEntero(t, conSigno) ? (conSigno ? long.Parse(t.Trim(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : ulong.Parse(t.Trim(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)) : "0",
    };

    // ---------------- almacenes ----------------
    private static string Rotulo(ulong clave) => clave == FnvHash.HashEmpty ? IdiomaInterfaz.T("EventosZA.Hueco") : Nombre(clave);

    private static string? Resumen(ulong clave, string valor) => clave == FnvHash.HashEmpty ? null : valor;

    private static PestanaP Marcas(string id, string titulo, Func<SaveBlockAccessor9ZA, EventWorkFlagStorage> almacen)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = c => almacen(B(c)).Count, MostrarPosicion = false,
            Titulo = (c, i) => $"{i:0000} · {Rotulo(almacen(B(c)).GetKey(i))}",
            Resumen = (c, i) => Resumen(almacen(B(c)).GetKey(i), almacen(B(c)).GetValue(i) ? IdiomaInterfaz.T("Cajas.Si") : "no"),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    Clave(col!.IdCampo(i, "clave"), L("Col_Key"), c => almacen(B(c)).GetKey(i), (c, v) => almacen(B(c)).SetKey(i, v)),
                    P.Marca(col.IdCampo(i, "valor"), L("Col_Value"), c => almacen(B(c)).GetValue(i), (c, v) => almacen(B(c)).SetValue(i, v)),
                ]),
            ],
        };
        return new PestanaP(id, titulo, [], Coleccion: col);
    }

    private static PestanaP Valores(string id, string titulo, Func<SaveBlockAccessor9ZA, EventWorkValueStorage> almacen)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = c => almacen(B(c)).Count, MostrarPosicion = false,
            Titulo = (c, i) => $"{i:0000} · {Rotulo(almacen(B(c)).GetKey(i))}",
            Resumen = (c, i) => Resumen(almacen(B(c)).GetKey(i), almacen(B(c)).GetValue(i).ToString(CultureInfo.InvariantCulture)),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    Clave(col!.IdCampo(i, "clave"), L("Col_Key"), c => almacen(B(c)).GetKey(i), (c, v) => almacen(B(c)).SetKey(i, v)),
                    Numero(col.IdCampo(i, "valor"), L("Col_Value"), c => almacen(B(c)).GetValue(i).ToString(CultureInfo.InvariantCulture), (c, v) => almacen(B(c)).SetValue(i, ulong.Parse(v, CultureInfo.InvariantCulture)), conSigno: false),
                ]),
            ],
        };
        return new PestanaP(id, titulo, [], Coleccion: col);
    }

    /// <summary><c>EventWorkGridTuple</c>: clave A (hash), «valor 1» (la segunda mitad de la clave, como número con signo) y valor.</summary>
    private static PestanaP Tuplas(string id, string titulo, Func<SaveBlockAccessor9ZA, EventWorkValueStorageKey128> almacen)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = c => almacen(B(c)).Count, MostrarPosicion = false,
            Titulo = (c, i) => $"{i:0000} · {Rotulo(almacen(B(c)).GetKey(i).A)}",
            Resumen = (c, i) => Resumen(almacen(B(c)).GetKey(i).A, $"{(long)almacen(B(c)).GetKey(i).B} / {almacen(B(c)).GetValue(i)}"),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    Clave(col!.IdCampo(i, "clave"), L("Col_KeyA"), c => almacen(B(c)).GetKey(i).A, (c, v) => almacen(B(c)).SetKey(i, v, almacen(B(c)).GetKey(i).B)),
                    Numero(col.IdCampo(i, "valor1"), L("Col_Value1"), c => ((long)almacen(B(c)).GetKey(i).B).ToString(CultureInfo.InvariantCulture), (c, v) => almacen(B(c)).SetKey(i, almacen(B(c)).GetKey(i).A, (ulong)long.Parse(v, CultureInfo.InvariantCulture)), conSigno: true),
                    Numero(col.IdCampo(i, "valor2"), L("Col_Value2"), c => almacen(B(c)).GetValue(i).ToString(CultureInfo.InvariantCulture), (c, v) => almacen(B(c)).SetValue(i, ulong.Parse(v, CultureInfo.InvariantCulture)), conSigno: false),
                ]),
            ],
        };
        return new PestanaP(id, titulo, [], Coleccion: col);
    }

    /// <summary><c>EventWorkGrid128</c>: dos claves (hash) y un valor.</summary>
    private static PestanaP Claves128(string id, string titulo, Func<SaveBlockAccessor9ZA, EventWorkValueStorageKey128> almacen)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = c => almacen(B(c)).Count, MostrarPosicion = false,
            Titulo = (c, i) => $"{i:0000} · {Rotulo(almacen(B(c)).GetKey(i).A)}",
            Resumen = (c, i) => Resumen(almacen(B(c)).GetKey(i).A, $"{Nombre(almacen(B(c)).GetKey(i).B)} / {almacen(B(c)).GetValue(i)}"),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    Clave(col!.IdCampo(i, "clave-a"), L("Col_KeyA"), c => almacen(B(c)).GetKey(i).A, (c, v) => almacen(B(c)).SetKey(i, v, almacen(B(c)).GetKey(i).B)),
                    Clave(col.IdCampo(i, "clave-b"), L("Col_KeyB"), c => almacen(B(c)).GetKey(i).B, (c, v) => almacen(B(c)).SetKey(i, almacen(B(c)).GetKey(i).A, v)),
                    Numero(col.IdCampo(i, "valor"), L("Col_Value"), c => almacen(B(c)).GetValue(i).ToString(CultureInfo.InvariantCulture), (c, v) => almacen(B(c)).SetValue(i, ulong.Parse(v, CultureInfo.InvariantCulture)), conSigno: false),
                ]),
            ],
        };
        return new PestanaP(id, titulo, [], Coleccion: col);
    }

    /// <summary><c>EventWorkGrid192</c>: tres claves (hash) y un valor.</summary>
    private static PestanaP Claves192(string id, string titulo, Func<SaveBlockAccessor9ZA, EventWorkValueStorageKey192> almacen)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = id, Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = c => almacen(B(c)).Count, MostrarPosicion = false,
            Titulo = (c, i) => $"{i:0000} · {Rotulo(almacen(B(c)).GetKey(i).A)}",
            Resumen = (c, i) => Resumen(almacen(B(c)).GetKey(i).A, $"{Nombre(almacen(B(c)).GetKey(i).B)} / {Nombre(almacen(B(c)).GetKey(i).C)} / {almacen(B(c)).GetValue(i)}"),
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    Clave(col!.IdCampo(i, "clave-a"), L("Col_KeyA"), c => almacen(B(c)).GetKey(i).A, (c, v) => { var k = almacen(B(c)).GetKey(i); almacen(B(c)).SetKey(i, v, k.B, k.C); }),
                    Clave(col.IdCampo(i, "clave-b"), L("Col_KeyB"), c => almacen(B(c)).GetKey(i).B, (c, v) => { var k = almacen(B(c)).GetKey(i); almacen(B(c)).SetKey(i, k.A, v, k.C); }),
                    Clave(col.IdCampo(i, "clave-c"), L("Col_KeyC"), c => almacen(B(c)).GetKey(i).C, (c, v) => { var k = almacen(B(c)).GetKey(i); almacen(B(c)).SetKey(i, k.A, k.B, v); }),
                    Numero(col.IdCampo(i, "valor"), L("Col_Value"), c => almacen(B(c)).GetValue(i).ToString(CultureInfo.InvariantCulture), (c, v) => almacen(B(c)).SetValue(i, ulong.Parse(v, CultureInfo.InvariantCulture)), conSigno: false),
                ]),
            ],
        };
        return new PestanaP(id, titulo, [], Coleccion: col);
    }

    /// <summary>
    /// Lo que hace «Guardar» con todos los almacenes: las marcas se reescriben como 0 o 1 y cada almacén se comprime (<c>Compress</c>: las entradas usadas suben para cerrar los huecos).
    /// </summary>
    internal static void Normalizar(ContextoP c)
    {
        var b = B(c);
        foreach (var f in new[] { b.Flags, b.Event, b.FieldItems })
        {
            for (var i = 0; i < f.Count; i++) f.SetValue(i, f.GetValue(i));
            f.Compress();
        }
        foreach (var v in new[] { b.Work, b.Quest, b.WorkMable, b.CountMable, b.CountTitle, b.WorkSpawn, b.InfiniteRank }) v.Compress();
        foreach (var k in new[] { b.Report, b.Obstruction, b.Spawner2, b.Spawner4 }) k.Compress();
        b.FieldObjectInteractable.Compress();
    }

    // ---------------- investigar ----------------
    private static void Diferencias<T>(List<string> resultado, string titulo, IEventValueStorage<T> nuevo, IEventValueStorage<T> viejo) where T : unmanaged, IEquatable<T>
    {
        // DiffBlocks de PKHeX recorre las entradas usadas del guardado NUEVO y mira la clave del NUEVO (update.GetKey(i)) dentro del bucle de las del viejo, asi que nunca lista las quitadas. «Investigar» no escribe
        // nada: aqui SI se listan (mejora declarada sobre PKHeX, ADR 0066); las añadidas y las cambiadas salen igual que en el formulario.
        var lineas = new List<string>();
        for (var i = 0; i < nuevo.Count; i++)
        {
            var hash = nuevo.GetKey(i);
            if (hash == FnvHash.HashEmpty) break;
            var u = nuevo.GetValue(i);
            var nombre = Nombre(hash);
            if (!viejo.TryGetValue(hash, out var p)) lineas.Add(IdiomaInterfaz.F("EventosZA.AnadidaValor", nombre, u));
            else if (!p.Equals(u)) lineas.Add(IdiomaInterfaz.F("EventosZA.Cambio", nombre, p, u));
        }
        for (var i = 0; i < viejo.Count; i++)
        {
            var hash = viejo.GetKey(i);
            if (hash == FnvHash.HashEmpty) break;
            if (!nuevo.TryGetValue(hash, out _)) lineas.Add(IdiomaInterfaz.F("EventosZA.QuitadaTeniaValor", Nombre(hash), viejo.GetValue(i)));
        }
        if (lineas.Count == 0) return;
        resultado.Add($"== {titulo} ({lineas.Count}) ==");
        resultado.AddRange(lineas);
        resultado.Add(string.Empty);
    }

    private static string? Resumen(ContextoP c)
    {
        var nuevo = InvestigarGuardados.Leer(c.Valor("despues") as byte[], InvestigarGuardados.MaximoBytesSwitch) as SAV9ZA;
        var viejo = InvestigarGuardados.Leer(c.Valor("antes") as byte[], InvestigarGuardados.MaximoBytesSwitch) as SAV9ZA;
        if (nuevo is null || viejo is null) return IdiomaInterfaz.T("EventosBDSP.EligeGuardadoAntesDespues");
        var u = nuevo.Blocks; var p = viejo.Blocks;
        var r = new List<string>();
        Diferencias<bool>(r, IdiomaInterfaz.T("Catalogo.Marcas"), u.Flags, p.Flags);
        Diferencias<bool>(r, IdiomaInterfaz.T("ModeloEventos.Eventos"), u.Event, p.Event);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.Trabajo"), u.Work, p.Work);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.Misiones"), u.Quest, p.Quest);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.TrabajoMable"), u.WorkMable, p.WorkMable);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.ContadoresMable"), u.CountMable, p.CountMable);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.ContadoresTitulo"), u.CountTitle, p.CountTitle);
        Diferencias<ulong>(r, IdiomaInterfaz.T("EventosZA.TrabajoAparicion"), u.WorkSpawn, p.WorkSpawn);
        Diferencias<bool>(r, IdiomaInterfaz.T("EventosZA.ObjetosCampo"), u.FieldItems, p.FieldItems);
        if (r.Count == 0) return IdiomaInterfaz.T("EventosZA.EncontradoDiferencias");
        var texto = r.Take(InvestigarGuardados.MaximoLineas).ToList();
        if (r.Count > InvestigarGuardados.MaximoLineas) texto.Add(IdiomaInterfaz.F("EventosZA.Lineas", r.Count - InvestigarGuardados.MaximoLineas));
        return string.Join("\n", texto);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV9ZA) return null;
        var investigar = new SeccionP(string.Empty,
        [
            InvestigarGuardados.Campo("antes", L("B_LoadOld"), g => g is SAV9ZA, IdiomaInterfaz.T("EventosZA.ArchivoGuardadoLeyendasPokemon"), IdiomaInterfaz.T("EventosBDSP.GuardadoANTESHacerAlgo"), maximo: InvestigarGuardados.MaximoBytesSwitch),
            InvestigarGuardados.Campo("despues", L("B_LoadNew"), g => g is SAV9ZA, IdiomaInterfaz.T("EventosZA.ArchivoGuardadoLeyendasPokemon"), IdiomaInterfaz.T("EventosBDSP.GuardadoDESPUESHacerlo"), Resumen, InvestigarGuardados.MaximoBytesSwitch),
        ]);
        return new EditorDefP("eventos9a", IdiomaInterfaz.T("Partida.Especifico.eventos9a.Titulo"),
        [
            Marcas("marcas", IdiomaInterfaz.T("Catalogo.Marcas"), b => b.Flags),
            Marcas("eventos", IdiomaInterfaz.T("ModeloEventos.Eventos"), b => b.Event),
            Valores("trabajo", IdiomaInterfaz.T("EventosZA.Trabajo"), b => b.Work),
            Valores("misiones", IdiomaInterfaz.T("EventosZA.Misiones"), b => b.Quest),
            Valores("trabajo-mable", IdiomaInterfaz.T("EventosZA.TrabajoMable"), b => b.WorkMable),
            Valores("contadores-mable", IdiomaInterfaz.T("EventosZA.ContadoresMable"), b => b.CountMable),
            Valores("contadores-titulo", IdiomaInterfaz.T("EventosZA.ContadoresTitulo"), b => b.CountTitle),
            Tuplas("informe", IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.20"), b => b.Report),
            Valores("trabajo-aparicion", IdiomaInterfaz.T("EventosZA.TrabajoAparicion"), b => b.WorkSpawn),
            Valores("rango-infinito", IdiomaInterfaz.T("EventosZA.RangoInfinito"), b => b.InfiniteRank),
            Tuplas("aparicion-2", IdiomaInterfaz.T("EventosZA.Aparicion"), b => b.Spawner2),
            Claves128("aparicion-4", IdiomaInterfaz.T("EventosZA.Aparicion2"), b => b.Spawner4),
            Tuplas("obstrucciones", IdiomaInterfaz.T("EventosZA.Obstrucciones"), b => b.Obstruction),
            Marcas("objetos-campo", IdiomaInterfaz.T("EventosZA.ObjetosCampo"), b => b.FieldItems),
            Claves192("objetos-interactivos", IdiomaInterfaz.T("EventosZA.ObjetosInteractivos"), b => b.FieldObjectInteractable),
            new PestanaP("investigar", L("GB_Research"), [investigar]),
        ], Censo, IdiomaInterfaz.T("EventosZA.MarcasVariablesMisionesContadores"))
        {
            PrepararAplicar = _ => Normalizar,
        };
    }
}
