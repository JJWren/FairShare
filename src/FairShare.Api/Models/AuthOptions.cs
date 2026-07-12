namespace FairShare.Api.Models;

public class AuthOptions
{
    // Off by default: a freshly deployed instance should not accept strangers'
    // sign-ups until the operator opts in (Auth:AllowSelfRegistration).
    public bool AllowSelfRegistration { get; set; } = false;
}
