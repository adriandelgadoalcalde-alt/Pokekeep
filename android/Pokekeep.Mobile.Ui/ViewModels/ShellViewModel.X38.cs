// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.Banco;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>X38: el banco propio de Pokémon. Aparte para no tocar la Shell de los demás bloques.</summary>
public sealed partial class ShellViewModel
{
    private UbicacionBanco? _ubicacionBanco;

    /// <summary>
    /// Dónde vive el banco: la carpeta privada de la app (o la que el usuario elija con el selector del sistema). Los anfitriones sin carpeta privada (pruebas) lo guardan en memoria mientras dura la ventana.
    /// </summary>
    internal UbicacionBanco UbicacionDelBanco => _ubicacionBanco ??= _servicios?.CarpetaPrivada is { } carpeta
        ? new UbicacionBanco(carpeta, _servicios.Carpetas, _servicios.Documentos)
        : new UbicacionBanco(new BancoPokemon(new DepositoBancoMemoria()));

    /// <summary>Cambia el banco (pruebas y capturas).</summary>
    internal void FijarUbicacionDelBanco(UbicacionBanco ubicacion) => _ubicacionBanco = ubicacion;

    [RelayCommand]
    public void AbrirBanco()
    {
        // El banco es una pantalla de «Más»: si se abre desde el aviso de otra pestaña, la selección de las cajas no debe quedarse con su barra de acciones encima.
        Cajas?.CancelarSeleccion();
        Destino = Destino.Mas;
        Pantalla = new BancoViewModel(ContextoDeHerramientas(), CerrarPantalla, UbicacionDelBanco);
    }

    /// <summary>Añade «Banco de Pokémon» a «Más», justo tras «Herramientas»: el banco se usa con o sin partida abierta.</summary>
    private IReadOnlyList<EntradaMenu> ConEntradaBanco(IReadOnlyList<EntradaMenu> entradas)
    {
        var lista = entradas.ToList();
        var indice = lista.FindIndex(e => e.ClaveTitulo == "Menu.Mas.Herramientas.Titulo");   // por clave: el titulo cambia con el idioma
        lista.Insert(indice < 0 ? Math.Min(3, lista.Count) : indice + 1, EntradaMenu.Localizada("Menu.Mas.Banco.Titulo", "Menu.Mas.Banco.Descripcion", "Package", "X38", AbrirBancoCommand));
        return lista;
    }

    /// <summary>Navegación de pruebas y capturas: <c>banco</c> abre el banco.</summary>
    private bool NavegarX38(string pantalla, out bool resultado)
    {
        resultado = false;
        if (pantalla != "banco") return false;
        Destino = Destino.Mas; AbrirBanco(); resultado = Pantalla is BancoViewModel;
        return true;
    }

    // ---------------------------------------------------------------------------------------------------------------- guardar en el banco
    string? IAnfitrionCajas.MotivoGuardarEnBanco => Sesion is null ? TextosBanco.SacarSinPartida : null;

    void IAnfitrionCajas.GuardarEnBanco(IReadOnlyList<CasillaRef> casillas, bool mover)
    {
        if (!mover) { _ = GuardarEnBancoAsync(casillas, false); return; }
        var n = casillas.Count;
        AbrirHoja(new ConfirmarHojaViewModel(TextosBanco.MoverTitulo, [TextosBanco.MoverTexto(n), TextosBanco.MoverAyuda], TextosBanco.MoverConfirmar, () => _ = GuardarEnBancoAsync(casillas, true), destructiva: true));
    }

    /// <summary>
    /// Guarda en el banco una copia de lo que hay en las casillas (en segundo plano: cada Pokémon pasa por el análisis de legalidad del Core) y, con <paramref name="mover"/>, quita de la partida lo que quedó a salvo
    /// (un Deshacer). Avisa con el resultado y un acceso al banco.
    /// </summary>
    public async Task GuardarEnBancoAsync(IReadOnlyList<CasillaRef> casillas, bool mover)
    {
        if (Sesion is not { } sesion) return;
        var banco = UbicacionDelBanco.Banco;
        // Las copias se leen aquí, en el hilo de la interfaz; guardarlas (análisis de legalidad y archivos) va a otro hilo.
        var origen = GuardadoEnBanco.Leer(sesion, casillas);
        if (origen.Count == 0) { Informar(TextosBanco.NadaQueGuardar, "error", "TriangleAlert"); return; }
        var pks = origen.Select(o => o.Pokemon).ToList();
        var resultados = await Task.Run(() => banco.Guardar(pks)).ConfigureAwait(true);
        var informe = GuardadoEnBanco.Concluir(sesion, origen, resultados, mover);
        Informar(TextosBanco.ResumenGuardado(informe, mover), informe.Limpio ? "ok" : "aviso", informe.Limpio ? "CircleCheck" : "TriangleAlert", TextosBanco.AbrirElBanco, AbrirBancoCommand);
    }
}