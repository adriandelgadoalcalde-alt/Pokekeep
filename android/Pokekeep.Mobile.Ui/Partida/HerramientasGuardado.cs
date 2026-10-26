// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Botones SAV de PKHeX pendientes de F10-13: API original, escritura por el historial único.</summary>
public static class HerramientasGuardado
{
    public static EditorDefP? Crear(string id, SaveFile sav)
    {
        CampoP[] campos;
        string titulo;
        string[] censo;
        switch (id)
        {
            case "pgl" when sav is SAV6:
                titulo = IdiomaInterfaz.T("Partida.Herramienta.pgl.Titulo");
                censo = ["PK-BOX-030", "PK-IO-043"];
                campos = [P.Archivo("jpeg", IdiomaInterfaz.T("HerramientasGuardado.ExportarJPEGOriginal"), null,
                    exporta: c => ((SAV6)c.Sav).GetJPEGData() is var datos && datos.Length > 0 ? ("PGL.jpeg", datos.ToArray()) : null,
                    extension: ".jpeg", motivo: c => ((SAV6)c.Sav).GetJPEGData().Length == 0 ? IdiomaInterfaz.T("HerramientasGuardado.PartidaContieneImagenPGL") : null,
                    ayuda: IdiomaInterfaz.T("HerramientasGuardado.ExportaBytesOriginalesJPEG"))];
                break;
            case "coreano" when sav is SAV4:
                titulo = IdiomaInterfaz.T("HerramientasGuardado.ConversionCoreanaCuartaGeneracion");
                censo = ["PK-BOX-031"];
                campos = [P.Info("region", IdiomaInterfaz.T("HerramientasGuardado.FormatoActual"), c => ((SAV4)c.Sav).Magic == SAV4.MAGIC_KOREAN ? IdiomaInterfaz.T("ModeloForzarCarga.Coreano") : IdiomaInterfaz.T("HerramientasGuardado.JaponesInternacional")),
                    P.Accion("convertir", IdiomaInterfaz.T("HerramientasGuardado.CambiarFormatoRegional"), c =>
                    {
                        var s = (SAV4)c.Sav;
                        s.Magic = s.Magic == SAV4.MAGIC_KOREAN ? SAV4.MAGIC_JAPAN_INTL : SAV4.MAGIC_KOREAN;
                    }, textoBoton: IdiomaInterfaz.T("HerramientasGuardado.Cambiar"), confirmacion: IdiomaInterfaz.T("HerramientasGuardado.AlternaIdentificadorGuardadoEntre"),
                    ayuda: IdiomaInterfaz.T("HerramientasGuardado.AccionQuedaPendienteHasta"))];
                break;
            case "zona-amigo" when sav is SAV6XY:
                titulo = IdiomaInterfaz.T("Partida.Herramienta.zona-amigo.Titulo");
                censo = ["PK-BOX-053"];
                campos = [P.Accion("desbloquear", IdiomaInterfaz.T("HerramientasGuardado.DesbloquearTodasRanuras"), c => ((SAV6XY)c.Sav).UnlockAllFriendSafariSlots(),
                    textoBoton: IdiomaInterfaz.T("HerramientasGuardado.Desbloquear"), confirmacion: IdiomaInterfaz.T("HerramientasGuardado.DesbloqueaRanurasZonaAmigo"),
                    ayuda: IdiomaInterfaz.T("HerramientasGuardado.AplicaCopiaSesionSolo"))];
                break;
            case "rtc2" when sav is SAV2:
                titulo = IdiomaInterfaz.T("HerramientasGuardado.ReiniciarRelojSegundaGeneracion");
                censo = ["PK-BOX-054"];
                campos = [P.Info("clave", IdiomaInterfaz.T("HerramientasGuardado.ClaveReinicio"), c => ((SAV2)c.Sav).Japanese ? IdiomaInterfaz.T("HerramientasGuardado.PartidasJaponesasUsanClave") : ((SAV2)c.Sav).ResetKey.ToString()),
                    P.Accion("reiniciar", IdiomaInterfaz.T("HerramientasGuardado.PermitirReiniciarRelojJuego"), c => ((SAV2)c.Sav).ResetRTC(), textoBoton: IdiomaInterfaz.T("Form.Activar"),
                        confirmacion: IdiomaInterfaz.T("HerramientasGuardado.ActivaMarcaReinicioReloj"))];
                break;
            default: return null;
        }
        return new EditorDefP(id, titulo, [new PestanaP("herramienta", titulo, [new SeccionP(string.Empty, campos)])], censo);
    }

    public static string Transeuntes(SAV6 sav) => string.Join(Environment.NewLine, PSS6.GetPSSParse(sav));

    /// <summary>El cambio de perfil BR modifica el clon completo del contenedor, nunca el original ni el perfil de la sesión anterior.</summary>
    public static Sesion CambiarRanura(Sesion anterior, int ranura)
    {
        if (anterior.Guardado is not SAV4BR br) throw new ArgumentException(IdiomaInterfaz.T("HerramientasGuardado.EsteGuardadoNoEs"), nameof(anterior));
        if ((uint)ranura >= (uint)br.SaveNames.Count) throw new ArgumentOutOfRangeException(nameof(ranura));
        var copia = (SAV4BR)br.Clone();
        copia.CurrentSlot = ranura;
        return new Sesion(copia, anterior.NombreArchivo)
        {
            Documento = anterior.Documento, Sha256Original = anterior.Sha256Original,
            CarpetaPrivada = anterior.CarpetaPrivada, Envoltorio = anterior.Envoltorio,
            CambiosPendientes = anterior.CambiosPendientes,
        };
    }
}
