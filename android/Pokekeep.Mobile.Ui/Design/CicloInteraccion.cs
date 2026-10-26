namespace Pokekeep.Mobile.Ui.Design;

/// <summary>La plataforma avisa cuando el sistema interrumpe una interacción táctil.</summary>
public static class CicloInteraccion
{
    public static event Action? Interrumpida;
    public static void Interrumpir() => Interrumpida?.Invoke();
}
