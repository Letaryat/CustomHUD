namespace CustomHUD.Integrations;

// A provider older than this build hands over a contract without the members an integration
// reads. Checked once at bind, so it is a log line rather than a MissingMethodException mid-draw.
internal static class ApiProbe
{
    public static void Require(Type contract, string since, params string[] members)
    {
        foreach (var member in members)
            if (contract.GetMember(member).Length == 0)
                throw new MissingMemberException($"{contract.Name}.{member} needs {since} or later");
    }
}
