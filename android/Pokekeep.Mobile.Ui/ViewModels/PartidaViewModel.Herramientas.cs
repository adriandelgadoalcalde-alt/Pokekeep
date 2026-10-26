// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class PartidaViewModel
{
    public Action<string>? AbrirHerramienta { get; set; }
    public Func<int, Task<bool>>? CambiarRanura { get; set; }

    private IEnumerable<EntradaEditorVm> Herramientas(SaveFile sav)
    {
        var abrir = new RelayCommand<string>(id => AbrirEditor(id));
        EntradaEditorVm E(string id, string titulo, string descripcion, string glifo) => new(id, titulo, descripcion, glifo, null, abrir);
        yield return E("integridad", IdiomaInterfaz.T("Partida.Herramienta.integridad.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.integridad.Descripcion"), "ShieldCheck");
        if (sav is SAV6)
        {
            yield return E("pgl", IdiomaInterfaz.T("Partida.Herramienta.pgl.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.pgl.Descripcion"), "Image");
            yield return E("pasa-pasa", IdiomaInterfaz.T("Partida.Herramienta.pasa-pasa.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.pasa-pasa.Descripcion"), "ScrollText");
        }
        if (sav is SAV6XY) yield return E("zona-amigo", IdiomaInterfaz.T("Partida.Herramienta.zona-amigo.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.zona-amigo.Descripcion"), "User");
        if (sav is SAV4) yield return E("coreano", IdiomaInterfaz.T("Partida.Herramienta.coreano.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.coreano.Descripcion"), "Layers");
        if (sav is SAV2) yield return E("rtc2", IdiomaInterfaz.T("Partida.Herramienta.rtc2.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.rtc2.Descripcion"), "History");
        if (sav is SAV4BR) yield return E("ranura-br", IdiomaInterfaz.T("Partida.Herramienta.ranura-br.Titulo"), IdiomaInterfaz.T("Partida.Herramienta.ranura-br.Descripcion"), "User");
    }

    private bool AbrirHerramientaGuardado(string id)
    {
        if (_sesion is null) return false;
        if (id == "integridad") { AbrirHerramienta?.Invoke("integridad"); return AbrirHerramienta is not null; }
        if (id == "pasa-pasa" && _sesion.Guardado is SAV6 s6)
        {
            _abrirHoja(new InformeHojaViewModel(IdiomaInterfaz.T("Partida.Herramienta.pasa-pasa.Titulo"), HerramientasGuardado.Transeuntes(s6),
                copiar: async texto =>
                {
                    if (_portapapeles() is not { } p || !await p.CopiarAsync(texto)) throw new IOException(IdiomaInterfaz.T("Partida.Error.CopiarListado"));
                }));
            return true;
        }
        if (id == "ranura-br" && _sesion.Guardado is SAV4BR br)
        {
            var filas = br.SaveNames.Select((nombre, i) => new EntradaHoja(IdiomaInterfaz.F("Partida.Ranuras.Fila", i + 1, string.IsNullOrWhiteSpace(nombre) ? IdiomaInterfaz.T("Partida.Ranuras.SinNombre") : nombre),
                i == br.CurrentSlot ? IdiomaInterfaz.T("Partida.Ranuras.Activo") : IdiomaInterfaz.T("Partida.Ranuras.Cambiar"), "User",
                i == br.CurrentSlot || CambiarRanura is null ? null : () => _ = CambiarRanura(i), Elegida: i == br.CurrentSlot)).ToArray();
            _abrirHoja(new MenuHojaViewModel(IdiomaInterfaz.T("Partida.Ranuras.Titulo"), [new SeccionHoja(null, filas)]));
            return true;
        }
        if (HerramientasGuardado.Crear(id, _sesion.Guardado) is not { } def) return false;
        Editor = new EditorPartidaViewModel(new ModeloEditorPartida(_sesion, def), () => Editor = null, _abrirHoja, _informar, _documentos, () => HerramientasGuardado.Crear(id, _sesion.Guardado));
        return true;
    }
}
