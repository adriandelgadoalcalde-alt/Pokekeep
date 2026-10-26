// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Mas;

/// <summary>Un componente de terceros con su licencia y cómo se usa (los mismos que <c>NOTICE.md</c>).</summary>
public sealed record CreditoAcerca(string Componente, string Licencia, string Uso)
{
    public string Accesible => $"{Componente}, licencia {Licencia}. {Uso}";
}

/// <summary>Una versión del historial de novedades de Pokekeep.</summary>
public sealed record NovedadAcerca(string Version, string Fecha, string Resumen, IReadOnlyList<string> Novedades)
{
    public string Cabecera => IdiomaInterfaz.F("PuertosMas.Version", Version, Fecha);
}

/// <summary>Un dato del paquete («Pareja de versiones», «Datos», «Firma»...) que se enseña en Acerca de y se copia a mano si hace falta.</summary>
public sealed record DatoPaquete(string Etiqueta, string Valor);

/// <summary>
/// Todo lo que enseña «Acerca de Pokekeep» (los mismos textos que la ventana del escritorio: <c>AcercaDeModelo</c> del núcleo) más lo propio de Android: versión del APK, versión de código, firma y manifiesto de datos,
/// y el «Acerca de PKHeX» original (créditos y registro de cambios de PKHeX) dentro.
/// </summary>
public sealed record ContenidoAcercaDe(
    string Nombre, string Lema, string Version, string LineaVersion, string Autoria,
    IReadOnlyList<CreditoAcerca> Creditos, string Licencia, string AvisoEventos, string AvisoMarcas, string AvisoPrivacidad,
    IReadOnlyList<NovedadAcerca> Novedades, IReadOnlyList<DatoPaquete> Paquete,
    string PkhexTitulo, string PkhexAgradecimiento, string PkhexCambios)
{
    /// <summary>Todo el texto de la pantalla en orden de lectura (para las pruebas y para copiarlo).</summary>
    public string TextoCompleto()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Nombre).AppendLine(Lema).AppendLine(LineaVersion).AppendLine();
        sb.AppendLine(IdiomaInterfaz.T("PuertosMas.Autoria")).AppendLine(Autoria).AppendLine();
        sb.AppendLine(IdiomaInterfaz.T("PuertosMas.CreditosLicencias"));
        foreach (var c in Creditos) sb.Append("• ").Append(c.Componente).Append(" — ").Append(c.Licencia).Append(". ").AppendLine(c.Uso);
        sb.AppendLine().AppendLine(Licencia).AppendLine().AppendLine(AvisoEventos).AppendLine().AppendLine(AvisoMarcas).AppendLine().AppendLine(AvisoPrivacidad).AppendLine();
        if (Paquete.Count > 0)
        {
            sb.AppendLine(IdiomaInterfaz.T("PuertosMas.Instalacion"));
            foreach (var p in Paquete) sb.Append(p.Etiqueta).Append(": ").AppendLine(p.Valor);
            sb.AppendLine();
        }
        sb.AppendLine(IdiomaInterfaz.T("PuertosMas.Novedades"));
        foreach (var n in Novedades)
        {
            sb.Append(IdiomaInterfaz.T("PuertosMas.Version2")).Append(n.Version).Append("  ").AppendLine(n.Fecha).AppendLine(n.Resumen);
            foreach (var l in n.Novedades) sb.Append("+ ").AppendLine(l);
            sb.AppendLine();
        }
        sb.AppendLine(PkhexTitulo).AppendLine(PkhexAgradecimiento);
        return sb.ToString();
    }
}

/// <summary>Puerto de «Acerca de Pokekeep»: lo implementa <c>Pokekeep.Mobile.Operaciones</c> con el núcleo compartido y los datos del paquete de Android.</summary>
public interface IAcercaDe
{
    ContenidoAcercaDe Obtener();
}
