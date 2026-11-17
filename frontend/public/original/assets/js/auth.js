(() => {
  "use strict";
  const API_BASE = (window.LMS_CONFIG && window.LMS_CONFIG.API_BASE_URL || "").replace(/\/$/, "");
  const ROLE_DASHBOARDS = { BusinessHead: "../dashboard/business-head.html", Manager: "../dashboard/manager.html", Trainer: "../dashboard/trainer.html", Student: "../dashboard/student.html" };
  document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("loginForm"); if (!form) return;
    const $ = id => document.getElementById(id);
    const button = $("loginButton"), message = $("loginMessage");
    const show = (text, type="error") => { if (message) { message.textContent=text; message.className=`login-message ${type}`; } else alert(text); };
    form.addEventListener("submit", async e => {
      e.preventDefault();
      const email = $("email")?.value.trim(), password = $("password")?.value, selected = $("role")?.value || $("selectedRole")?.value;
      if (!email || !password) return show("Please enter your email and password.");
      if (button) { button.disabled=true; button.textContent="Signing in..."; }
      try {
        const response = await fetch(`${API_BASE}/api/auth/login`, { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({email,password}) });
        const data = await response.json().catch(()=>({}));
        if (!response.ok || !data.token || !data.user) throw new Error(data.message || data.error || "Invalid email or password.");
        const role = data.user.role;
        if (selected && selected.toLowerCase().replace(/[_ -]/g,"") !== role.toLowerCase().replace(/[_ -]/g,"")) throw new Error("Selected role does not match your account.");
        const target = ROLE_DASHBOARDS[role]; if (!target) throw new Error("Your account has no valid dashboard.");
        sessionStorage.setItem("lms_token", data.token);
        sessionStorage.setItem("lms_user", JSON.stringify(data.user));
        location.href = data.mustChangePassword ? "./change-password.html" : target;
      } catch (err) { show(err.message || "Unable to sign in."); }
      finally { if (button) { button.disabled=false; button.textContent="Sign In"; } }
    });
  });
})();
