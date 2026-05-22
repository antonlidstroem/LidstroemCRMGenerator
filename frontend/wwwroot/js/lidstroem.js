/**
 * lidstroem.js
 * JavaScript interop called from C# via IJSRuntime.
 * Keeps all DOM manipulation and browser API access out of Blazor components.
 */

window.lidstroem = {

  /**
   * Applies CSS custom properties to :root and loads Google Fonts.
   * Called by SkinService before first render to prevent FOUC.
   *
   * @param {Record<string, string>} tokens  - CSS custom property map
   * @param {string}                 theme   - theme name ("Clarity" | "Momentum" | "Focus")
   * @param {boolean}                isDark  - whether dark mode is active
   */
  applySkin(tokens, theme, isDark) {
    const root = document.documentElement;

    // Apply all CSS custom properties
    for (const [key, value] of Object.entries(tokens)) {
      root.style.setProperty(key, value);
    }

    // Set theme data attribute (used by layout selectors if needed)
    root.setAttribute('data-theme', theme.toLowerCase());
    root.setAttribute('data-dark', isDark ? 'true' : 'false');

    // Load Google Fonts based on the font tokens
    const headingFont = tokens['--font-heading'];
    const bodyFont    = tokens['--font-body'];
    const fontsToLoad = [...new Set([headingFont, bodyFont].filter(Boolean))];

    if (fontsToLoad.length > 0) {
      const families = fontsToLoad
        .map(f => `family=${encodeURIComponent(f)}:wght@400;500;600;700;800`)
        .join('&');

      const link = document.getElementById('google-fonts-link');
      if (link) {
        link.href = `https://fonts.googleapis.com/css2?${families}&display=swap`;
      }
    }
  },

  /**
   * Reads the system color scheme preference.
   * Used to initialise dark mode when set to "system".
   */
  prefersColorSchemeDark() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  },

  /**
   * Watches for system color scheme changes and calls back into Blazor.
   * @param {DotNetObjectReference} dotNetRef - reference to a C# object with UpdateDarkMode(bool)
   */
  watchColorScheme(dotNetRef) {
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    mq.addEventListener('change', e => {
      dotNetRef.invokeMethodAsync('UpdateDarkMode', e.matches);
    });
  },

  /**
   * Sets the document title and favicon.
   */
  setSiteMetadata(title, faviconUrl) {
    if (title)      document.title = title;
    if (faviconUrl) {
      let link = document.querySelector("link[rel~='icon']");
      if (!link) {
        link = document.createElement('link');
        link.rel = 'icon';
        document.head.appendChild(link);
      }
      link.href = faviconUrl;
    }
  },

  /**
   * Smooth-scrolls to the top of the page after navigation.
   * Called from App.razor on route change.
   */
  scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  },

  /**
   * Copies text to the clipboard.
   * @returns {Promise<boolean>} success
   */
  async copyToClipboard(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  },

  /**
   * Opens a URL in a new tab.
   */
  openInNewTab(url) {
    window.open(url, '_blank', 'noopener,noreferrer');
  },

  /**
   * Triggers a file download from a blob URL or data URL.
   */
  downloadFile(filename, url) {
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
  },

  /**
   * Focuses an element by CSS selector.
   */
  focusElement(selector) {
    const el = document.querySelector(selector);
    if (el) el.focus();
  },
};
