// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Una fila de un bolsillo tal como la ve el usuario: el objeto, la cantidad y las marcas que ese juego guarda (favorito, nuevo, espacio libre...). Es el equivalente de una fila de la tabla de <c>SAV_Inventory</c>.</summary>
public sealed class FilaObjeto
{
    public int Id { get; set; }
    public int Cantidad { get; set; }
    public bool Libre { get; set; }
    public uint IndiceLibre { get; set; }
    public bool Favorito { get; set; }
    public bool Nuevo { get; set; }
    public bool NuevoTienda { get; set; }
    public bool Sostenido { get; set; }

    public FilaObjeto Copia() => (FilaObjeto)MemberwiseClone();
    public bool Igual(FilaObjeto o) => Id == o.Id && Cantidad == o.Cantidad && Libre == o.Libre && IndiceLibre == o.IndiceLibre && Favorito == o.Favorito && Nuevo == o.Nuevo && NuevoTienda == o.NuevoTienda && Sostenido == o.Sostenido;
}

/// <summary>Un bolsillo de la mochila (<see cref="InventoryPouch"/> del Core) con sus filas de trabajo.</summary>
public sealed class BolsilloModelo
{
    private readonly ModeloMochila _m;
    internal BolsilloModelo(ModeloMochila m, InventoryPouch pouch, int indice) { _m = m; Pouch = pouch; Indice = indice; Tipo = pouch.Type; Cargar(); }

    public InventoryPouch Pouch { get; }
    internal Sesion Sesion => _m.Sesion;
    public bool EsIngredientesCampamento => Sesion.Guardado is SAV8SWSH && Tipo == InventoryType.Candy;
    public int Indice { get; }
    public InventoryType Tipo { get; }
    public string Titulo => EsIngredientesCampamento
        ? IdiomaInterfaz.T("Mochila.Bolsillo.IngredientesCampamento") : ModeloMochila.NombreBolsillo(Tipo);
    /// <summary>Cantidad maxima por objeto en este bolsillo (la regla del juego; algunos objetos tienen una propia, ver <see cref="ModeloMochila.MaximoDe"/>).</summary>
    public int MaxCount => Pouch.MaxCount;
    public int Ranuras => Pouch.Items.Length;
    public List<FilaObjeto> Filas { get; private set; } = [];
    private List<FilaObjeto> _originales = [];
    private Dictionary<int, InventoryItem> _items = [];
    /// <summary>Objetos que el bolsillo trae pero que no le corresponden: al aplicar con este bolsillo tocado, PKHeX los quita (<c>MsgItemPouchRemoved</c>). Se avisa antes de aplicar.</summary>
    public IReadOnlyList<int> Descartados { get; private set; } = [];
    public bool Tocado { get; private set; }

    /// <summary>Este bolsillo se edita como lista fija (SV y Z-A: el objeto de cada fila no se cambia, solo la cantidad).</summary>
    public bool ObjetoFijo => _m.ObjetoFijo;

    internal void Cargar(bool reiniciar = true)
    {
        Filas = [];
        var itemsOriginales = new Dictionary<int, InventoryItem>();
        var invalidos = new List<int>();
        var validos = Pouch.GetAllItems();
        foreach (var it in Pouch.Items)
        {
            var item = it;
            if (item.Index != 0 && !validos.Contains((ushort)item.Index) && !_m.HaX) { invalidos.Add(item.Index); item = Pouch.GetEmpty(); }
            Filas.Add(DesdeItem(item));
            if (item.Index != 0) itemsOriginales.TryAdd(item.Index, item with { });
        }
        Descartados = invalidos;
        if (reiniciar) { _items = itemsOriginales; _originales = [.. Filas.Select(f => f.Copia())]; Tocado = false; }
    }

    private static FilaObjeto DesdeItem(InventoryItem item) => new()
    {
        Id = item.Index,
        Cantidad = item.Count,
        Libre = item is IItemFreeSpace { IsFreeSpace: true },
        IndiceLibre = item is IItemFreeSpaceIndex fi ? fi.FreeSpaceIndex : 0,
        Favorito = item is IItemFavorite { IsFavorite: true },
        Nuevo = item is IItemNewFlag { IsNew: true },
        NuevoTienda = item is IItemNewShopFlag { IsNewShop: true },
        Sostenido = item is IItemHeldFlag { IsHeld: true },
    };

    public bool HayCambios => Filas.Count != _originales.Count || !Filas.Zip(_originales, (a, b) => a.Igual(b)).All(x => x);

    /// <summary>Marca el bolsillo como tocado (cualquier edicion o accion masiva).</summary>
    public void Tocar() => Tocado = true;

    /// <summary>Filas con objeto o, en un bolsillo de objeto fijo, todas; con <paramref name="soloConCantidad"/>, solo las que tienen cantidad (en todos los juegos desde uso10).</summary>
    public IEnumerable<(int Fila, FilaObjeto Objeto)> Visibles(bool soloConCantidad)
    {
        for (var i = 0; i < Filas.Count; i++)
        {
            var f = Filas[i];
            if (ObjetoFijo) { if (!soloConCantidad || f.Cantidad > 0) yield return (i, f); }
            else if (f.Id != 0 && (!soloConCantidad || f.Cantidad > 0)) yield return (i, f);
        }
    }

    public int PrimeraLibre()
    {
        for (var i = 0; i < Filas.Count; i++) if ((ObjetoFijo || Filas[i].Id == 0) && Filas[i].Cantidad == 0) return i;
        return -1;
    }

    /// <summary>Objetos que el bolsillo admite, ordenados por nombre (la lista del desplegable de PKHeX: <c>GetStringsForPouch</c>).</summary>
    /// <remarks>Sin el modo avanzado se dejan fuera los objetos de relleno del texto (<see cref="ObjetosRelleno"/>): el Core los admite pero no existen en el juego.</remarks>
    public IReadOnlyList<int> Admitidos() => Admitidos(0);

    /// <param name="conservar">Objeto que ya está en la fila: se ofrece aunque sea de relleno, para que el selector pueda enseñar qué hay puesto.</param>
    public IReadOnlyList<int> Admitidos(int conservar)
        => [.. Pouch.GetAllItems().ToArray().Select(x => (int)x).Where(x => _m.HaX || x == conservar || !ObjetosRelleno.EsRelleno(_m.Sesion.Guardado, x)).OrderBy(x => _m.Nombre(x), StringComparer.CurrentCulture)];

    // ---- Equivalente de SetBag del formulario ----
    /// <summary>Vuelca las filas al bolsillo exactamente como <c>SAV_Inventory.SetBag</c>: compactacion de vacias, cantidad saneada, objetos nuevos limpios y vacias al final.</summary>
    internal void Volcar()
    {
        var bag = _m.Bag;
        var ctr = 0;
        foreach (var f in Filas)
        {
            var id = f.Id;
            if (id <= 0 && !_m.TieneNuevo) continue;   // compresion de huecos
            var count = f.Cantidad;
            if (!bag.IsQuantitySane(Tipo, id, ref count, _m.TieneNuevo, _m.HaX)) continue;
            InventoryItem item;
            if (_items.TryGetValue(id, out var origen))
            {
                // El objeto ya estaba: se parte de el (sus datos que la tabla no ensena se conservan) y se le aplica lo que ensena la fila, solo si cambio.
                item = origen with { };
                item.Count = count;
                if (item is IItemFreeSpace fs0 && fs0.IsFreeSpace != f.Libre) fs0.IsFreeSpace = f.Libre;
                if (item is IItemFreeSpaceIndex fi0 && fi0.FreeSpaceIndex != f.IndiceLibre) fi0.FreeSpaceIndex = f.IndiceLibre;
                if (item is IItemFavorite fv0 && fv0.IsFavorite != f.Favorito) fv0.IsFavorite = f.Favorito;
                if (item is IItemNewFlag n0 && n0.IsNew != f.Nuevo) n0.IsNew = f.Nuevo;
                if (item is IItemNewShopFlag ns0 && ns0.IsNewShop != f.NuevoTienda) ns0.IsNewShop = f.NuevoTienda;
                if (item is IItemHeldFlag g0 && g0.IsHeld != f.Sostenido) g0.IsHeld = f.Sostenido;
            }
            else
            {
                // Objeto nuevo en el bolsillo: datos limpios, como el formulario.
                item = Pouch.GetEmpty(id, count);
                if (item is IItemFreeSpace fs) fs.IsFreeSpace = f.Libre;
                if (item is IItemFreeSpaceIndex fi) fi.FreeSpaceIndex = f.IndiceLibre;
                if (item is IItemFavorite fv) fv.IsFavorite = f.Favorito;
                if (item is IItemNewFlag n) n.IsNew = f.Nuevo;
                if (item is IItemNewShopFlag ns) ns.IsNewShop = f.NuevoTienda;
                if (item is IItemHeldFlag g) g.IsHeld = f.Sostenido;
            }
            Pouch.Items[ctr++] = item;
        }
        for (var i = ctr; i < Pouch.Items.Length; i++) Pouch.Items[i] = Pouch.GetEmpty();
    }

    /// <summary>Equivalente de <c>ModifyPouch</c>: guarda lo editado, aplica la accion del Core y vuelve a leer.</summary>
    internal void Modificar(Action<InventoryPouch> accion)
    {
        Volcar();
        accion(Pouch);
        Cargar(reiniciar: false);
        Tocado = true;
    }
}

/// <summary>
/// Borrador de la mochila (<c>SAV_Inventory</c>): la mochila del Core leida de la partida, sus bolsillos reales con su limite real y las operaciones del formulario (dar todos, cambiar todas las cantidades, ordenar, limpiar).
/// Nada se escribe en la partida hasta <see cref="Aplicar"/>, que hace lo que el boton Guardar del formulario (<c>SetBags</c> + <c>Bag.CopyTo</c>) como UNA entrada del historial.
/// </summary>
public sealed class ModeloMochila
{
    private readonly Sesion _sesion;
    private string[] _nombres = [];

    public ModeloMochila(Sesion sesion)
    {
        _sesion = sesion;
        var sav = sesion.Guardado;
        ActualizarNombres();
        try { Bag = sav.Inventory; } catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException) { Bag = null!; _errorLectura = true; }
        var lista = new List<BolsilloModelo>();
        if (Bag is not null && Bag.Pouches.Count > 0)
        {
            var item0 = Bag.Pouches[0].Items.Length > 0 ? Bag.Pouches[0].Items[0] : null;
            ObjetoFijo = sav is SAV9ZA or SAV9SV;
            TieneLibre = item0 is IItemFreeSpace; TieneIndiceLibre = item0 is IItemFreeSpaceIndex; TieneFavorito = item0 is IItemFavorite;
            TieneNuevo = item0 is IItemNewFlag; TieneNuevoTienda = item0 is IItemNewShopFlag; TieneSostenido = item0 is IItemHeldFlag;
            for (var i = 0; i < Bag.Pouches.Count; i++) lista.Add(new BolsilloModelo(this, Bag.Pouches[i], i));
        }
        Bolsillos = lista;
    }

    public Sesion Sesion => _sesion;
    public PlayerBag Bag { get; }
    private readonly bool _errorLectura;
    public string? MotivoSinMochila => _errorLectura ? IdiomaInterfaz.T("Mochila.ErrorLeer") : null;
    public IReadOnlyList<BolsilloModelo> Bolsillos { get; }
    /// <summary>La partida no tiene mochila editable (p. ej. Battle Revolution).</summary>
    public bool SinMochila => Bolsillos.Count == 0;
    public bool HaX { get; set; } = Ajustes.Pkhex.ProgramaPkhex.HaX;   // Program.HaX al arrancar (bloque 10-1); la pantalla puede cambiarlo
    public bool ObjetoFijo { get; }
    public bool TieneLibre { get; }
    public bool TieneIndiceLibre { get; }
    public bool TieneFavorito { get; }
    public bool TieneNuevo { get; }
    public bool TieneNuevoTienda { get; }
    public bool TieneSostenido { get; }
    public bool HayCambios => Bolsillos.Any(b => b.HayCambios);

    public string Nombre(int id) => (uint)id < _nombres.Length ? _nombres[id] : IdiomaInterfaz.F("Mochila.ObjetoSinNombre", id);

    public void ActualizarNombres()
    {
        _nombres = NombresObjetos.Lista(_sesion.Textos, _sesion.Guardado);
        for (var i = 0; i < _nombres.Length; i++) if (string.IsNullOrEmpty(_nombres[i])) _nombres[i] = IdiomaInterfaz.F("Mochila.ObjetoSinNombre", i);
    }

    /// <summary>Maximo de ESTE objeto en este bolsillo (<c>PlayerBag.GetMaxCount</c>: el limite del bolsillo salvo objetos con uno propio).</summary>
    public int MaximoDe(BolsilloModelo b, int objeto) => HaX ? Bag.MaxQuantityHaX : Bag.GetMaxCount(b.Tipo, objeto);

    /// <summary>Texto con el que se rechaza una cantidad fuera de limite; null si vale.</summary>
    public string? RechazoCantidad(BolsilloModelo b, int objeto, string texto)
    {
        if (!int.TryParse(texto.Trim(), out var n) || n < 0) return IdiomaInterfaz.T("Mochila.Entero");
        var max = MaximoDe(b, objeto);
        if (n > max) return IdiomaInterfaz.F("Mochila.SuperaMaximo", Nombre(objeto), b.Titulo, max);
        return null;
    }

    /// <summary>Pone la cantidad de una fila. Devuelve el motivo del rechazo (y no cambia nada) o null.</summary>
    public string? PonerCantidad(BolsilloModelo b, int fila, string texto)
    {
        var f = b.Filas[fila];
        if (RechazoCantidad(b, f.Id, texto) is { } motivo) return motivo;
        f.Cantidad = int.Parse(texto.Trim()); b.Tocar();
        return null;
    }

    /// <summary>Cambia el objeto de una fila (un objeto recien puesto con cantidad 0 pasa a 1).</summary>
    public void PonerObjeto(BolsilloModelo b, int fila, int objeto)
    {
        var f = b.Filas[fila];
        if (b.ObjetoFijo)
        {
            if (objeto == 0) { f.Cantidad = 0; b.Tocar(); return; }
            var destino = b.Filas.FirstOrDefault(x => x.Id == objeto);
            if (destino is null || ReferenceEquals(destino, f))
            {
                if (destino is not null && destino.Cantidad == 0) { destino.Cantidad = 1; b.Tocar(); }
                return;
            }
            destino.Cantidad = Math.Min(Math.Max(1, f.Cantidad), MaximoDe(b, objeto));
            f.Cantidad = 0;
            b.Tocar();
            return;
        }
        f.Id = objeto;
        if (objeto == 0) { f.Cantidad = 0; f.Favorito = f.Nuevo = f.NuevoTienda = f.Sostenido = f.Libre = false; f.IndiceLibre = 0; }
        else
        {
            if (f.Cantidad <= 0) f.Cantidad = 1;
            f.Cantidad = Math.Min(f.Cantidad, MaximoDe(b, objeto));
        }
        b.Tocar();
    }

    public void Quitar(BolsilloModelo b, int fila) => PonerObjeto(b, fila, 0);

    // ---- Menu «Dar todos» y «Ordenar» (los mismos metodos del Core que el formulario) ----
    public bool PuedeDarTodos(BolsilloModelo b) => !(b.Tipo is InventoryType.PCItems or InventoryType.FreeSpace && _sesion.Guardado is not SAV8LA) || HaX;
    public bool Apretado(BolsilloModelo b) => b.Pouch.IsCramped;
    public int CantidadSugerida(BolsilloModelo b) => Math.Max(1, b.MaxCount - 4);

    /// <summary>«Dar todos los objetos» (<c>GiveAllItems</c>). <paramref name="barajar"/> solo importa si el bolsillo es pequeno (el formulario lo pregunta).</summary>
    public void DarTodos(BolsilloModelo b, int cantidad, bool barajar)
    {
        var items = b.Pouch.GetAllItems().ToArray();
        if (barajar) Util.Rand.Shuffle(items);
        b.Modificar(p => p.GiveAllItems(Bag, items, cantidad));
    }

    public void CambiarTodasLasCantidades(BolsilloModelo b, int cantidad) => b.Modificar(p => p.ModifyAllCount(Bag, cantidad));
    public void QuitarTodos(BolsilloModelo b) => b.Modificar(p => p.RemoveAll());
    public void OrdenarPorNombre(BolsilloModelo b, bool inverso) => b.Modificar(p => p.SortByName(_nombres, inverso));
    public void OrdenarPorCantidad(BolsilloModelo b, bool inverso) => b.Modificar(p => p.SortByCount(inverso));
    public void OrdenarPorNumero(BolsilloModelo b, bool inverso) => b.Modificar(p => p.SortByIndex(inverso));

    // ---- Aplicar ----
    /// <summary>
    /// Escribe la mochila en la partida como el boton Guardar del formulario (<c>SetBags</c> y <c>Bag.CopyTo</c>), pero SOLO con los bolsillos tocados: los demas conservan sus bytes tal cual
    /// (el formulario los vuelve a escribir todos). Una entrada del historial; sin cambios reales no queda nada.
    /// </summary>
    public InformePartida Aplicar()
    {
        var sav = _sesion.Guardado;
        if (!HayCambios) return new InformePartida(ResultadoPartida.SinCambios, string.Empty, 0);   // sin cambios no se escribe NADA (ni siquiera lo que el Core normaliza al reescribir la mochila)
        var r = EscrituraPartida.Aplicar(_sesion, IdiomaInterfaz.T("ModeloMochila.Mochila") + string.Join(", ", Bolsillos.Where(b => b.HayCambios).Select(b => b.Titulo)), () =>
        {
            foreach (var b in Bolsillos) if (b.HayCambios) b.Volcar();
            Bag.CopyTo(sav);
        });
        foreach (var b in Bolsillos) b.Cargar();   // lo escrito es ahora lo guardado: el borrador parte de ahi
        return r;
    }

    /// <summary>Vuelve a leer la mochila de la partida (tras Deshacer/Rehacer).</summary>
    public void Recargar()
    {
        var nueva = _sesion.Guardado.Inventory;
        for (var i = 0; i < Bolsillos.Count && i < nueva.Pouches.Count; i++)
        {
            Array.Copy(nueva.Pouches[i].Items, Bolsillos[i].Pouch.Items, Math.Min(nueva.Pouches[i].Items.Length, Bolsillos[i].Pouch.Items.Length));
            Bolsillos[i].Cargar();
        }
    }

    public static string NombreBolsillo(InventoryType t) => t switch
    {
        InventoryType.Items => IdiomaInterfaz.T("Mochila.Bolsillo.Items"),
        InventoryType.KeyItems => IdiomaInterfaz.T("Mochila.Bolsillo.KeyItems"),
        InventoryType.TMHMs => IdiomaInterfaz.T("Mochila.Bolsillo.TMHMs"),
        InventoryType.Medicine => IdiomaInterfaz.T("Mochila.Bolsillo.Medicine"),
        InventoryType.Berries => IdiomaInterfaz.T("Mochila.Bolsillo.Berries"),
        InventoryType.Balls => IdiomaInterfaz.T("Mochila.Bolsillo.Balls"),
        InventoryType.BattleItems => IdiomaInterfaz.T("Mochila.Bolsillo.BattleItems"),
        InventoryType.MailItems => IdiomaInterfaz.T("Mochila.Bolsillo.MailItems"),
        InventoryType.PCItems => IdiomaInterfaz.T("Mochila.Bolsillo.PCItems"),
        InventoryType.FreeSpace => IdiomaInterfaz.T("Mochila.Bolsillo.FreeSpace"),
        InventoryType.ZCrystals => IdiomaInterfaz.T("Mochila.Bolsillo.ZCrystals"),
        InventoryType.Candy => IdiomaInterfaz.T("Mochila.Bolsillo.Candy"),
        InventoryType.Treasure => IdiomaInterfaz.T("Mochila.Bolsillo.Treasure"),
        InventoryType.Ingredients => IdiomaInterfaz.T("Mochila.Bolsillo.Ingredients"),
        InventoryType.MegaStones => IdiomaInterfaz.T("Mochila.Bolsillo.MegaStones"),
        _ => t.ToString(),
    };
}
