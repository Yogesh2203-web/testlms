(() => {
  "use strict";
  const DASHBOARDS = {
    BusinessHead: "/original/dashboard/business-head.html",
    Manager: "/original/dashboard/manager.html",
    Trainer: "/original/dashboard/trainer.html",
    Student: "/original/dashboard/student.html"
  };
  function goLogin(message) {
    if (message) sessionStorage.setItem("loginMessage", message);
    sessionStorage.removeItem("lms_token");
    sessionStorage.removeItem("lms_user");
    window.location.replace("/original/pages/login.html");
  }
  async function protect(allowedRoles = []) {
    const token = sessionStorage.getItem("lms_token");
    if (!token) { goLogin("Please sign in to continue."); return null; }
    try {
      const response = await fetch(`${(window.LMS_CONFIG?.API_BASE_URL || "").replace(/\/$/, "")}/api/auth/me`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!response.ok) { goLogin("Your session has expired. Please sign in again."); return null; }
      const data = await response.json();
      const user = data.user;
      if (!user || (allowedRoles.length && !allowedRoles.includes(user.role))) {
        const target = DASHBOARDS[user?.role];
        if (target) window.location.replace(target); else goLogin("Access denied.");
        return null;
      }
      return user;
    } catch {
      // Do not allow dashboard pages to render as authenticated when API validation fails.
      goLogin("Unable to verify your session. Check your connection and sign in again.");
      return null;
    }
  }
  window.AuthGuard = { protect };
})();
