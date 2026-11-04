using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Menu;

namespace CustomHUD.Input;

// Synthesized activations. MenuActionContext is a public record struct and
// HandleActionAsync sits on the public interface, so this is a supported seam.
internal static class MenuActivation
{
    // Fire and forget: activation is async by contract and the component owns
    // its waiting state. A synchronous one (a plain submenu) has finished when
    // this returns. Exceptions must not escape onto the game thread.
    public static void Select(IMenuComponent component, IMenuSession session, MenuKey key,
        Func<string, string> translate, ILogger logger)
        => Run(component, session, MenuActions.Select, key, translate, logger);

    // NavigateLeft / NavigateRight: what A and D send a slider or selector.
    public static void Run(IMenuComponent component, IMenuSession session, MenuActionId action, MenuKey key,
        Func<string, string> translate, ILogger logger)
        => _ = RunAsync(component, new MenuActionContext
        {
            Action = action,
            Key = key,
            Session = session,
            Translate = translate,
        }, logger);

    public static async Task RunAsync(IMenuComponent component, MenuActionContext context, ILogger logger)
    {
        try
        {
            await component.HandleActionAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomHUD] Synthesized activation threw.");
        }
    }
}
