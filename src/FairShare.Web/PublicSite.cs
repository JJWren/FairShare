namespace FairShare.Web;

/// <summary>
/// Absolute URLs for crawler-facing metadata (per-route canonical links via HeadContent).
/// The origin is deliberately the production one, matching index.html's og:url - crawlers
/// only ever see the production host, and dev/test canonicals pointing at prod are the
/// desired behavior for the nightly-reset test host anyway (it must never rank).
/// </summary>
public static class PublicSite
{
    public const string Origin = "https://easychildsupport.fyi";

    public static string Canonical(string path) => Origin + path;
}
