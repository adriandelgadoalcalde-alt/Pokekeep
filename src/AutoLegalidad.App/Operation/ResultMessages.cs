// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Textos específicos por estado del contrato (Hito 4, punto 6). Cada estado dice QUÉ pasó y por qué, sin listas de alternativas (regla 4) y sin
/// presentar un fallo como éxito (regla 5). El detalle técnico del motor se añade tal cual al final.
/// </summary>
public static class ResultMessages
{
    public sealed record Text(OutcomeKind Kind, string Headline, string? Detail);

    public static Text ForResponse(LegalizeResponse r, PokekeepAction action, OperationOrigin origin, string species, string game, TimeSpan limit, bool homeSettingOn = false)
    {
        var reason = r.Reason;
        var library = origin == OperationOrigin.Library;
        switch (r.Status)
        {
            case ResponseStatus.AlreadyLegal:
                return new(OutcomeKind.AlreadyLegal, Textos.YaEsLegal, null);

            case ResponseStatus.AlreadyOptimal:
                // 0.6.6 (M4): bajo el ajuste de HOME, un Pokémon sin margen de mejora que sigue sin matrícula lo dice.
                return new(OutcomeKind.AlreadyOptimal, Textos.SinMargen, Join(r.Summary.Any(l => l.Contains("matrícula de HOME", StringComparison.Ordinal)) ? Textos.NecesitaHomeAviso : null, Join(Motor(r.Optimization?.LimitReason ?? reason), null)));

            case ResponseStatus.NotLegalFirst:
                return new(OutcomeKind.NotApplied, Textos.PrimeroLegalizar, AppIdioma.T("«Optimizar stats» solo mejora Pokémon que ya son legales y nunca cambia su origen.", "“Optimize stats” only improves Pokémon that are already legal and never changes their origin."));

            case ResponseStatus.NoSolution when action == PokekeepAction.Optimize:
                return new(OutcomeKind.NotApplied, AppIdioma.T("No se ha encontrado una mejora de stats", "No stat improvement was found"),
                    AppIdioma.T("La búsqueda de IV no llegó a ser exhaustiva, así que esto no prueba que no exista una mejora. El Pokémon queda como estaba.", "The IV search wasn’t exhaustive, so this doesn’t prove that no improvement exists. The Pokémon is left as it was.") + Technical(reason));

            case ResponseStatus.NoSolution:
                return new(OutcomeKind.NotApplied, library ? AppIdioma.T("No se ha podido añadir este Pokémon", "This Pokémon could not be added") : AppIdioma.T("No se ha encontrado una forma de legalizarlo", "No way to legalize it was found"),
                    (r.SearchComplete
                        ? WholeSpeciesProof(reason)
                            ? AppIdioma.F("No existe ningún origen legal para {0} en {1}.", "There is no legal origin for {0} in {1}.", species, game)
                            // 0.6.6 (M3): la prueba del motor vale para ESTE origen o ESTA forma, no para la especie entera: no se dice que la especie no tenga origen.
                            : library ? AppIdioma.F("Esta entrada concreta no se puede generar en {0}: el motor lo ha demostrado para este origen o esta forma, no para toda la especie.", "This specific entry can’t be generated in {0}: the engine has shown this for this origin or form, not for the whole species.", game)
                                : AppIdioma.F("Con este origen o esta forma no hay manera legal de hacerlo en {0}: el motor lo ha demostrado para ese caso, no para toda la especie.", "With this origin or form there is no legal way to do it in {0}: the engine has shown this for that case, not for the whole species.", game)
                        : AppIdioma.T("Ningún origen probado superó la validación oficial. La búsqueda se hace por muestreo, así que esto no prueba que sea imposible.", "No origin tried passed the official validation. The search is done by sampling, so this doesn’t prove it is impossible."))
                    + " " + AppIdioma.T("Tu Pokémon queda intacto.", "Your Pokémon is left untouched.") + Technical(reason));

            case ResponseStatus.Timeout:
                return new(OutcomeKind.NotApplied, AppIdioma.T("Tiempo agotado", "Time ran out"),
                    AppIdioma.F("Se agotó el límite de {0} sin encontrar una solución. Esto no demuestra que sea imposible: puedes volver a intentarlo o ampliar el límite en Opciones > Ajustes > Pokekeep. Tu Pokémon queda intacto.", "The limit of {0} ran out without finding a solution. This doesn’t show it is impossible: you can try again or raise the limit in Options > Settings > Pokekeep. Your Pokémon is left untouched.", Textos.Segundos(limit)));

            case ResponseStatus.UnsupportedSave:
                return new(OutcomeKind.NotApplied, AppIdioma.T("Este juego no está soportado todavía", "This game isn’t supported yet"), Join(AppIdioma.T("Pokekeep no puede construir el destino de esta partida.", "Pokekeep can’t build the target for this save."), Motor(reason)));

            case ResponseStatus.UnsupportedData when reason is not null && (reason.Contains("rastreador", StringComparison.OrdinalIgnoreCase) || reason.Contains("matrícula", StringComparison.OrdinalIgnoreCase) || reason.Contains("matricula", StringComparison.OrdinalIgnoreCase)):
                return new(OutcomeKind.NotApplied, AppIdioma.T("Este origen exige la matrícula de Pokémon HOME", "This origin requires the Pokémon HOME tracker"),
                    (homeSettingOn
                        ? AppIdioma.F("Aun con el ajuste «{0}» activado, PKHeX no da por válido ningún Pokémon con este origen sin su matrícula real (la evolución, el encuentro o la forma solo los admite tras pasar por HOME). ", "Even with the “{0}” setting on, PKHeX doesn’t consider any Pokémon with this origin valid without its real tracker (the evolution, encounter or form is only accepted after passing through HOME). ", Textos.AjusteHomeTexto)
                        : AppIdioma.F("Pokekeep nunca se inventa la matrícula (rastreador) que asigna el servidor de HOME, así que las rutas que la exigen (por ejemplo, desde Pokémon GO hacia los juegos de Switch) no están disponibles. Si aceptas el riesgo, activa «{0}» en Opciones > Ajustes > Pokekeep. ", "Pokekeep never makes up the tracker that the HOME server assigns, so the routes that require it (for example, from Pokémon GO to the Switch games) aren’t available. If you accept the risk, turn on “{0}” in Options > Settings > Pokekeep. ", Textos.AjusteHomeTexto))
                    + AppIdioma.T("Tu Pokémon queda intacto.", "Your Pokémon is left untouched."));

            case ResponseStatus.UnsupportedData:
                return new(OutcomeKind.NotApplied, AppIdioma.T("Esto no se puede hacer con los datos actuales", "This can’t be done with the current data"), Join(Motor(reason), AppIdioma.T("Tu Pokémon queda intacto.", "Your Pokémon is left untouched.")));

            case ResponseStatus.SpeciesNotInDestination:
                return new(OutcomeKind.NotApplied, AppIdioma.T("La especie no existe en este juego", "The species doesn’t exist in this game"), Textos.EspecieNoEnDestino(species, game));

            case ResponseStatus.OriginPreserved:
                return new(OutcomeKind.NotApplied, AppIdioma.T("Mejorar los stats exigiría cambiar el origen", "Improving the stats would require changing the origin"),
                    AppIdioma.T("«Optimizar stats» nunca cambia el origen del Pokémon, así que no se ha aplicado nada.", "“Optimize stats” never changes the Pokémon’s origin, so nothing was applied.") + Technical(r.Optimization?.LimitReason ?? reason));

            case ResponseStatus.VersionMismatch:
                return new(OutcomeKind.EngineFailure, AppIdioma.T("El motor no coincide con esta versión de Pokekeep", "The engine doesn’t match this version of Pokekeep"),
                    AppIdioma.T("El motor y la base de datos deben ser los de esta misma entrega. No se ha generado nada.", "The engine and the database must be those of this same release. Nothing was generated.") + Technical(reason));

            case ResponseStatus.InvalidInput:
                return new(OutcomeKind.NotApplied, AppIdioma.T("No se pudo leer el Pokémon", "The Pokémon couldn’t be read"), Join(AppIdioma.T("El motor rechazó los datos de entrada.", "The engine rejected the input data."), Motor(reason)));

            case ResponseStatus.ValidationFailed:
                return new(OutcomeKind.ValidationFailed, AppIdioma.T("El resultado no superó la comprobación final", "The result did not pass the final check"), Join(AppIdioma.T("Se ha descartado: tu Pokémon sigue como estaba.", "It was discarded: your Pokémon is as it was."), Motor(reason)));

            case ResponseStatus.Cancelled:
                return new(OutcomeKind.Cancelled, Textos.OperacionCancelada, null);

            case ResponseStatus.InternalError:
                return new(OutcomeKind.EngineFailure, AppIdioma.T("El motor no pudo completar la operación", "The engine couldn’t complete the operation"), Join(AppIdioma.T("Tu Pokémon queda intacto.", "Your Pokémon is left untouched."), Motor(reason)));

            default:
                return new(OutcomeKind.NotApplied, Motor(r.Message) ?? r.Message, Join(AppIdioma.T("Tu Pokémon queda intacto.", "Your Pokémon is left untouched."), Motor(reason)));
        }
    }

    public static Text ForStability(Stability s) => s switch
    {
        Stability.SaveChanged => new(OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.PartidaCambio),
        Stability.EditorChanged => new(OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.EditorCambio),
        Stability.SlotOccupied => new(OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.CasillaOcupada),
        _ => new(OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.CasillaCambio),
    };

    /// <summary>El motor solo demuestra «la especie no tiene ningún origen» cuando el Core no enumera ni un solo encuentro de la especie en el destino (<c>LegalizationService.NoSolution</c>); el resto de pruebas completas son de un origen, una forma o una rama concretos.</summary>
    private static bool WholeSpeciesProof(string? reason) => reason is not null && reason.StartsWith("PKHeX no enumera ningún encuentro de la especie", StringComparison.Ordinal);

    private static string Technical(string? reason) => string.IsNullOrWhiteSpace(reason) ? string.Empty : AppIdioma.T(" Detalle técnico: ", " Technical detail: ") + Motor(reason);

    /// <summary>Los textos del motor llegan en castellano (son datos del contrato): en inglés se muestran con su plantilla inglesa. La lógica de esta clase sigue mirando el texto ORIGINAL.</summary>
    private static string? Motor(string? texto) => TextosMotor.Traducir(texto);

    private static string? Join(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b;
        return string.IsNullOrWhiteSpace(b) ? a : a + " " + b;
    }
}
