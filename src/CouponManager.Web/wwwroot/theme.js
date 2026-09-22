(() => {
  const apply = (theme) => {
    if (theme === "light" || theme === "dark")
      document.documentElement.dataset.theme = theme;
    else delete document.documentElement.dataset.theme;
  };
  const getSaved = () => localStorage.getItem("coupon-theme") || "system";
  const applySaved = () => apply(getSaved());
  applySaved();
  window.couponTheme = {
    set: (theme) => {
      localStorage.setItem("coupon-theme", theme);
      apply(theme);
    },
    get: getSaved,
  };

  const registerEnhancedNavigation = () => {
    if (!window.Blazor?.addEventListener) {
      setTimeout(registerEnhancedNavigation, 25);
      return;
    }
    window.Blazor.addEventListener("enhancedload", applySaved);
  };
  registerEnhancedNavigation();
  addEventListener("pageshow", applySaved);

  if ("serviceWorker" in navigator && isSecureContext) {
    addEventListener("load", () =>
      navigator.serviceWorker.register("/service-worker.js").catch(() => {}),
    );
  }
})();
