// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Integridad;

/// <summary>
/// «Partida &gt; Integridad» (PK-BOX-027 y PK-BOX-028, PK-IO-058): comprobar las sumas de verificación del guardado y verificar la legalidad de TODOS los Pokémon guardados (<c>BulkAnalysis</c>). Solo lee: no modifica la
/// partida ni abre ningún diálogo (regla 4: cero diálogos de alternativas). Donde PKHeX pregunta «¿Copiar el informe al portapapeles?», aquí el informe está en pantalla con un botón «Copiar el informe».
/// Los ajustes de PKHeX <c>Legality.Bulk</c> se leen en el momento de verificar (nunca una copia vieja).
/// </summary>
public sealed partial class IntegridadViewModel : HerramientaViewModel
{
    public IntegridadViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        Tabla = new TablaResultadosViewModel(
        [
            new ColumnaTabla("texto", IdiomaInterfaz.T("Integridad.Pasa"), o => ((HallazgoBulk)o).Texto),
            new ColumnaTabla("donde", IdiomaInterfaz.T("Informe.Donde"), o => ((HallazgoBulk)o).Donde),
        ]);
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
    }

    public override string Titulo => IdiomaInterfaz.T("Partida.Herramienta.integridad.Titulo");
    public string Subtitulo => IdiomaInterfaz.T("Integridad.CompruebaPartidaEnteraPokemon");
    public bool HayPartida => Sesion is not null;
    public TablaResultadosViewModel Tabla { get; }

    // ---- Sumas de verificación ----
    [ObservableProperty] private string _textoSumas = string.Empty;
    [ObservableProperty] private string _claseSumas = string.Empty;
    [ObservableProperty] private bool _hayDetalleSumas;
    private ResultadoSumas? _sumas;
    public bool HayResultadoSumas => !string.IsNullOrEmpty(TextoSumas);
    public ResultadoSumas? Sumas => _sumas;
    partial void OnTextoSumasChanged(string value) => OnPropertyChanged(nameof(HayResultadoSumas));
    partial void OnClaseSumasChanged(string value) { OnPropertyChanged(nameof(SumasOk)); OnPropertyChanged(nameof(SumasAviso)); OnPropertyChanged(nameof(SumasError)); }
    public bool SumasOk => ClaseSumas == "ok";
    public bool SumasAviso => ClaseSumas == "aviso";
    public bool SumasError => ClaseSumas == "error";

    [RelayCommand]
    private void ComprobarSumas()
    {
        if (Sesion is null) return;
        _sumas = ModeloIntegridad.ComprobarSumas(Sesion.Guardado, Sesion.CambiosPendientes > 0);
        TextoSumas = _sumas.Mensaje;
        ClaseSumas = _sumas.Estado switch { EstadoSumas.Validas => "ok", EstadoSumas.PartidaEditada => "aviso", _ => "error" };
        HayDetalleSumas = _sumas.Detalle is not null;
        OnPropertyChanged(nameof(Sumas));
    }

    [ObservableProperty] private string _copiado = string.Empty;

    [RelayCommand]
    private async Task CopiarDetalleSumas()
    {
        if (_sumas?.Detalle is not { } detalle || Contexto.Portapapeles() is not { } clip) return;
        Copiado = await clip.CopiarAsync(detalle) ? IdiomaInterfaz.T("Integridad.DetalleCopiadoPortapapeles") : IdiomaInterfaz.T("Informe.PudoCopiar");
    }

    // ---- Verificar todos los Pokémon ----
    [ObservableProperty] private string _textoVerificacion = string.Empty;
    [ObservableProperty] private string _claseVerificacion = string.Empty;
    [ObservableProperty] private bool _hayInforme;
    public bool HayResultadoVerificacion => !string.IsNullOrEmpty(TextoVerificacion);
    public ResultadoVerificacion? Verificacion { get; private set; }
    partial void OnTextoVerificacionChanged(string value) => OnPropertyChanged(nameof(HayResultadoVerificacion));
    partial void OnClaseVerificacionChanged(string value) { OnPropertyChanged(nameof(VerificacionOk)); OnPropertyChanged(nameof(VerificacionError)); }
    public bool VerificacionOk => ClaseVerificacion == "ok";
    public bool VerificacionError => ClaseVerificacion == "error";
    public bool HayTabla => Verificacion is { Limpio: false };

    [RelayCommand]
    public async Task VerificarTodos()
    {
        if (Sesion is null) return;
        var sav = Sesion.Guardado;
        var ajustes = Ajustes.Legality.Bulk;
        var r = await EjecutarAsync(IdiomaInterfaz.T("Integridad.ComprobandoPokemonGuardados"), (cancelar, avance) =>
        {
            avance.Report(new AvanceTrabajo(IdiomaInterfaz.T("Integridad.ComprobandoPokemonGuardados"), null));
            return ModeloIntegridad.VerificarTodos(sav, ajustes, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, cancelar);
        });
        if (r is null) return;
        MostrarVerificacion(r);
    }

    /// <summary>Deja en pantalla el resultado de una verificación (también lo usa el gate visual para dibujar el estado «con resultado» sin esperar a un trabajo).</summary>
    public void MostrarVerificacion(ResultadoVerificacion r)
    {
        Verificacion = r;
        Tabla.Establecer(r.Hallazgos);
        HayInforme = !r.Limpio;
        TextoVerificacion = r.Limpio ? IdiomaInterfaz.F("Integridad.LimpioEncontradoNingunProblema", r.Analizados) : IdiomaInterfaz.F("Integridad.EncontradoProblemasPokemon", r.Hallazgos.Count, r.Analizados);
        ClaseVerificacion = r.Limpio ? "ok" : "error";
        Estado = TextoVerificacion;
        OnPropertyChanged(nameof(Verificacion)); OnPropertyChanged(nameof(HayTabla));
    }

    /// <summary>Comprueba las sumas y deja el resultado en pantalla (síncrono; el gate visual lo usa para el estado «sumas»).</summary>
    public void MostrarSumas() => ComprobarSumasCommand.Execute(null);

    /// <summary>Verifica ahora y en este hilo (sin trabajo en segundo plano): solo para dejar la pantalla en el estado «verificado» en el gate visual y las pruebas.</summary>
    public void VerificarSincrono() { if (Sesion is not null) MostrarVerificacion(ModeloIntegridad.VerificarTodos(Sesion.Guardado, Ajustes.Legality.Bulk)); }

    [RelayCommand]
    private async Task CopiarInforme()
    {
        if (Verificacion is not { Limpio: false } v || Contexto.Portapapeles() is not { } clip) return;
        Copiado = await clip.CopiarAsync(v.Informe) ? IdiomaInterfaz.T("Integridad.InformeCopiadoPortapapeles") : IdiomaInterfaz.T("Informe.PudoCopiar");
    }
}
