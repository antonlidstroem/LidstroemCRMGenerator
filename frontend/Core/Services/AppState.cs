using Lidstroem.Frontend.Core.Models;

namespace Lidstroem.Frontend.Core.Services;

public class AppState
{
    public SiteConfig? SiteConfig { get; private set; }
    public string? TenantSlug { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsBootstrapped { get; private set; }

    public event Action? StateChanged;

    public void SetSiteConfig(SiteConfig config)
    {
        SiteConfig = config;
        NotifyStateChanged();
    }

    public void SetTenantSlug(string slug)
    {
        TenantSlug = slug;
        NotifyStateChanged();
    }

    public void SetLoading(bool loading)
    {
        IsLoading = loading;
        NotifyStateChanged();
    }

    public void SetBootstrapped()
    {
        IsBootstrapped = true;
        IsLoading = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
